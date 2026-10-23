using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class CheckInService : ICheckInService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    /// <summary>Số người được ở thêm trên sức chứa tiêu chuẩn của loại phòng (tính phụ thu theo người ở thêm, xem FolioCalculator). Vượt mức này không nhận.</summary>
    public const int NguoiThemToiDa = 1;

    public CheckInService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<CheckInResultDto> CheckInBookingAsync(string maPhieuDat, BookingCheckInRequest request)
    {
        // [FIX P0 — 24/09/2026] Mở transaction + khóa dòng PhieuDatPhong tới hết transaction:
        // 2 yêu cầu check-in gần như đồng thời cho cùng 1 phiếu đặt (ví dụ đoàn khách check-in
        // rải rác cùng lúc) phải chờ nhau tuần tự, tránh trường hợp cả 2 cùng thấy
        // PhieuNhanPhong = null rồi cùng tạo 2 PhieuNhanPhong cho cùng 1 phiếu đặt (trước đây
        // hàm này không có transaction/khóa dòng, khác WalkInAsync bên dưới đang làm đúng).
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var phieuDat = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .Include(x => x.Khach)
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.ChiTietKhuyenMais).ThenInclude(k => k.KhuyenMai)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p!.ChiTietPhieuNhans)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        if (phieuDat.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_XAC_NHAN_COC", "Phiếu đặt phòng chưa xác nhận đặt cọc, không thể nhận phòng.");
        if (phieuDat.TrangThai != TrangThaiPhieuDat.DA_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Không thể nhận phòng khi phiếu đặt đang ở trạng thái {phieuDat.TrangThai}.");

        // Trong ngày đón dự kiến, nhận phòng lúc nào cũng được (hệ thống không quy định giờ nhận chuẩn) nên "nhận phòng sớm trong ngày" không cần thủ tục riêng.
        // Nhận phòng SỚM từ một ngày trước ngày đón dự kiến: tiền phòng tính từ ngày nhận thực tế và phòng bị chiếm thêm những đêm đó, nên phải chọn "nhận sớm"
        // (kèm lý do, nhật ký ghi lại), phòng phải trống từ hôm nay, và ngày đón của phiếu được dời về hôm nay.
        var homNayNhan = DateOnly.FromDateTime(DateTime.Now);
        if (phieuDat.NgayTraDuKien <= homNayNhan)
            throw new ApiException(StatusCodes.Status409Conflict, "PHIEU_QUA_HAN_TRA",
                $"Phiếu đã quá ngày trả dự kiến ({phieuDat.NgayTraDuKien:dd/MM/yyyy}), không thể nhận phòng. Hãy đánh dấu không đến hoặc tạo phiếu mới.");
        var nhanSom = homNayNhan < phieuDat.NgayDonDuKien;
        if (nhanSom)
        {
            if (!request.NhanSom)
                throw new ApiException(StatusCodes.Status409Conflict, "CHUA_DEN_NGAY_NHAN",
                    $"Chưa đến ngày nhận phòng dự kiến ({phieuDat.NgayDonDuKien:dd/MM/yyyy}). Muốn nhận phòng sớm hãy chọn \"nhận sớm\" và ghi lý do.");
            if (string.IsNullOrWhiteSpace(request.LyDo))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần ghi lý do khi nhận phòng sớm (ảnh hưởng số đêm tính tiền, có ghi nhật ký).", "lyDo");
            if (phieuDat.PhieuNhanPhong is not null)
                throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", "Phiếu đã nhận một phần phòng, không nhận sớm thêm được.");
        }

        var phongNhan = (request.PhongNhan ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (phongNhan.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_NHAN", "Cần chỉ định ít nhất 1 phòng nhận.", "phongNhan");

        var daNhan = phieuDat.PhieuNhanPhong?.ChiTietPhieuNhans.Select(x => x.MaPhong).ToHashSet() ?? new HashSet<string>();
        foreach (var maPhong in phongNhan)
        {
            if (phieuDat.ChiTietPhieuDats.All(x => x.MaPhong != maPhong))
                throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_KHONG_THUOC_PHIEU_DAT", $"Phòng {maPhong} không thuộc phiếu đặt phòng này (dùng Đổi phòng nếu cần phòng khác).", "phongNhan");
            if (daNhan.Contains(maPhong))
                throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_NHAN", $"Phòng {maPhong} đã được nhận trước đó.", "phongNhan");
        }

        var thongTinKhach = LayThongTinKhach(request.Khach, phongNhan);

        // [FIX P0] Khóa từng dòng Phong (sắp theo MaPhong tăng dần để tránh deadlock giữa các
        // yêu cầu check-in/đặt phòng/walk-in khác đang khóa nhiều phòng cùng lúc) — đúng mẫu
        // đã dùng ở WalkInAsync, thay cho truy vấn không khóa trước đây.
        var phongs = new List<Phong>();
        foreach (var maPhong in phongNhan.OrderBy(x => x, StringComparer.Ordinal))
        {
            var phong = await _context.Phongs
                .FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}")
                .Include(x => x.LoaiPhong)
                .FirstOrDefaultAsync()
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", $"Không tìm thấy phòng {maPhong}.", "phongNhan");
            RoomAvailability.EnsureReady(phong);
            phongs.Add(phong);
        }

        await CapNhatCccdAsync(phieuDat.Khach, thongTinKhach);

        var ngayDonCu = phieuDat.NgayDonDuKien;
        var tongCu = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien);
        if (nhanSom)
        {
            // Mọi phòng của phiếu (không chỉ phòng nhận hôm nay) phải trống từ hôm nay tới ngày đón cũ vì ngày đón của cả phiếu được dời về hôm nay.
            foreach (var maPhong in phieuDat.ChiTietPhieuDats.Select(x => x.MaPhong).Except(phongNhan).OrderBy(x => x, StringComparer.Ordinal))
                await _context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}").AsNoTracking().ToListAsync();
            var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, homNayNhan, ngayDonCu, maPhieuDat);
            var vuong = phieuDat.ChiTietPhieuDats.Where(x => daChiem.Contains(x.MaPhong) || x.Phong.TinhTrang == TinhTrangPhong.OOO).Select(x => x.MaPhong).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (vuong.Count > 0)
                throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                    $"Không nhận sớm được: phòng {string.Join(", ", vuong)} đang có khách/đã có đặt phòng khác trong {homNayNhan:dd/MM/yyyy} - {ngayDonCu:dd/MM/yyyy}.", "phongNhan");

            var quyTac = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
            // Nhận sớm → tính lại giá từng đêm theo bảng giá hiện hành và chốt lại (lúc thứ hai giá được tính lại).
            await _context.GiaDemDatPhongs.Where(x => x.MaPhieuDat == maPhieuDat).ExecuteDeleteAsync();
            foreach (var ct in phieuDat.ChiTietPhieuDats)
            {
                var gia = GiaPhongCalculator.Tinh(ct.Phong.LoaiPhong, homNayNhan, phieuDat.NgayTraDuKien, phieuDat.LoaiDatPhong, quyTac);
                ct.DonGiaApDung = gia.DonGiaBinhQuan;
                ct.ThanhTien = gia.TongTien;
                _context.GiaDemDatPhongs.AddRange(GiaPhongCalculator.TaoGiaChot(maPhieuDat, ct.MaPhong, ct.Phong.LoaiPhong, gia));
            }
            var tongSom = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien);
            foreach (var km in phieuDat.ChiTietKhuyenMais)
                km.TongKhuyenMai = PromotionRules.TinhTien(km.KhuyenMai, phieuDat.ChiTietPhieuDats.Select(c => (c.Phong.MaLoai, c.ThanhTien)));
            phieuDat.NgayDonDuKien = homNayNhan;
        }

        var bayGio = DateTime.Now;
        var phieuNhan = phieuDat.PhieuNhanPhong;
        if (phieuNhan is null)
        {
            phieuNhan = new PhieuNhanPhong
            {
                MaPhieuNhan = await SinhMaPhieuNhanAsync(bayGio),
                MaPhieuDat = phieuDat.MaPhieuDat,
                // [FIX P1 — 24/09/2026] Luu thang MaKhach tren PhieuNhanPhong (xem ghi chu tai
                // KLCN060.Domain.PhieuNhanPhong.MaKhach) - truoc day chi suy ra khach qua PhieuDatPhong.Khach,
                // khien InvoiceService phai di vong 2 buoc va khong hoat dong voi walk-in (khong co PhieuDatPhong).
                MaKhach = phieuDat.MaKhach,
                NgayNhan = bayGio
            };
            _context.PhieuNhanPhongs.Add(phieuNhan);
        }

        var ketQua = new List<CheckedInRoomDto>();
        foreach (var phong in phongs.OrderBy(x => x.MaPhong, StringComparer.Ordinal))
        {
            var chiTietDat = phieuDat.ChiTietPhieuDats.First(x => x.MaPhong == phong.MaPhong);
            var soNguoi = LaySoNguoi(thongTinKhach, phong);

            _context.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
            {
                MaPhieuNhan = phieuNhan.MaPhieuNhan,
                MaPhong = phong.MaPhong,
                NgayNhan = DateOnly.FromDateTime(bayGio),
                NgayTra = null,
                NgayTraDuKien = phieuDat.NgayTraDuKien,
                SoNguoi = soNguoi,
                DonGia = chiTietDat.DonGiaApDung,
                TienPhuThu = 0,
                SoNguoiTieuChuanChot = phong.LoaiPhong.SoNguoiTieuChuan,
                PhuThuNguoiThemChot = phong.LoaiPhong.PhuThu,
                TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
            });

            phong.TinhTrang = TinhTrangPhong.OC;
            ketQua.Add(new CheckedInRoomDto { MaPhong = phong.MaPhong, SoNguoi = soNguoi, DonGia = chiTietDat.DonGiaApDung, TinhTrangPhong = nameof(TinhTrangPhong.OC) });
        }

        if (phieuDat.ChiTietPhieuDats.All(x => daNhan.Contains(x.MaPhong) || phongNhan.Contains(x.MaPhong)))
            phieuDat.TrangThai = TrangThaiPhieuDat.DA_NHAN_PHONG;

        _audit.LyDoTinhTrangPhong = $"Nhận phòng theo phiếu {maPhieuDat}";
        _audit.Ghi("NHAN_PHONG", maPhieuDat, $"phieuNhan={phieuNhan.MaPhieuNhan}; phong={string.Join(",", phongs.Select(p => p.MaPhong))}; khach={phieuDat.MaKhach}");
        if (nhanSom)
            _audit.Ghi("NHAN_PHONG_SOM", maPhieuDat,
                $"ngayDonCu={ngayDonCu:yyyy-MM-dd}; ngayNhan={homNayNhan:yyyy-MM-dd}; tongCu={Math.Round(tongCu, 0)}; tongMoi={Math.Round(phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien), 0)}; lyDo={request.LyDo!.Trim()}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi nhận phòng (có thể phòng vừa được nhận ở nơi khác), vui lòng thử lại.");
        }

        // [FIX P0] Commit transaction đã mở ở đầu hàm — trước đây hàm này không có transaction nên
        // không có bước commit tương ứng.
        await transaction.CommitAsync();

        return new CheckInResultDto
        {
            MaPhieuNhan = phieuNhan.MaPhieuNhan,
            MaPhieuDat = phieuDat.MaPhieuDat,
            TrangThaiPhieuDat = phieuDat.TrangThai.ToString(),
            NgayNhan = phieuNhan.NgayNhan,
            NguoiThucHien = _audit.TenDN,
            PhongDaNhan = ketQua
        };
    }

    public async Task<CheckInResultDto> WalkInAsync(WalkInCheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MaKhach))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_KHACH", "Cần chỉ định khách lưu trú (tạo bằng POST /guests nếu chưa có).", "maKhach");
        var khach = await _context.Khachs.FirstOrDefaultAsync(x => x.MaKhach == request.MaKhach)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHACH_HANG", "Không tìm thấy khách hàng.", "maKhach");

        var phongNhan = (request.Khach ?? new()).Select(x => x.MaPhong).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (phongNhan.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_NHAN", "Cần chỉ định ít nhất 1 phòng nhận.", "khach");
        if (phongNhan.Distinct().Count() != phongNhan.Count)
            throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_TRUNG_LAP", "Mỗi phòng chỉ được xuất hiện 1 lần.", "khach");

        var thongTinKhach = LayThongTinKhach(request.Khach!, phongNhan);
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        // Khách lưu trú bắt buộc có ngày trả cụ thể (không để "chưa khai"): cần để biết phòng sắp trả, quá hạn trả và kiểm tra khi gia hạn.
        if (request.NgayTraDuKien is not { } ngayTraDuKien)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_NGAY_TRA", "Khách lưu trú phải có ngày trả phòng dự kiến cụ thể.", "ngayTraDuKien");
        if (ngayTraDuKien > homNay) BookingService.KiemTraSoDem(homNay, ngayTraDuKien, "ngayTraDuKien");
        if (ngayTraDuKien <= homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_TRA_KHONG_HOP_LE", "Ngày trả dự kiến phải sau hôm nay.", "ngayTraDuKien");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // [Khối 8 - P1-4] Khóa + kiểm tra TẤT CẢ phòng TRƯỚC, sinh MaPhieuNhan SAU — cùng thứ tự khóa với CheckInBookingAsync
        // (khóa dòng Phong → khóa sinh mã CI). Trước đây walk-in sinh mã trước rồi mới khóa phòng: khi đã có khóa sinh mã,
        // thứ tự ngược nhau giữa 2 luồng sẽ tạo vòng chờ (deadlock).
        var phongs = new List<Phong>();
        foreach (var maPhong in phongNhan.OrderBy(x => x, StringComparer.Ordinal))
        {
            // Khóa dòng phòng tới hết transaction: 2 lễ tân cùng nhận 1 phòng thì người sau phải chờ rồi thấy phòng đã OC.
            var phong = await _context.Phongs
                .FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}")
                .Include(x => x.LoaiPhong)
                .FirstOrDefaultAsync()
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", $"Không tìm thấy phòng {maPhong}.", "khach");

            RoomAvailability.EnsureReady(phong);

            await RoomAvailability.EnsureFreeTodayAsync(_context, maPhong, homNay, "khach", ngayTraDuKien);
            phongs.Add(phong);
        }

        // Khách vãng lai chưa biết số đêm ở nên tính đơn giá theo bảng giá của hôm nay (không có quy tắc nào thì là đơn giá loại phòng).
        var quyTacGia = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);

        var ketQua = new List<CheckedInRoomDto>();
        var phieuNhan = new PhieuNhanPhong
        {
            MaPhieuNhan = await SinhMaPhieuNhanAsync(bayGio),
            MaPhieuDat = null,
            // [FIX P1 — 24/09/2026] Phat hien qua Khoi 7 dot 2 Integration Testing: walk-in truoc day KHONG
            // luu lai da nhan phong cho Khach nao, khien hoa don tu dong lap khi tra phong luon co
            // MaKhach/HoTenKhach = null va khach vang lai khong bao gio xem duoc hoa don cua chinh minh
            // (KiemTraQuyenXem trong InvoiceService luon tra 403). Xem chi tiet tai
            // KLCN060.Domain.PhieuNhanPhong.MaKhach.
            MaKhach = khach.MaKhach,
            NgayNhan = bayGio
        };
        _context.PhieuNhanPhongs.Add(phieuNhan);

        foreach (var phong in phongs)
        {
            var soNguoi = LaySoNguoi(thongTinKhach, phong);
            var donGia = GiaPhongCalculator.GiaDem(phong.LoaiPhong, homNay, LoaiDatPhong.CA_NHAN, quyTacGia, out _);
            _context.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
            {
                MaPhieuNhan = phieuNhan.MaPhieuNhan,
                MaPhong = phong.MaPhong,
                NgayNhan = homNay,
                NgayTra = null,
                NgayTraDuKien = request.NgayTraDuKien,
                SoNguoi = soNguoi,
                DonGia = donGia,
                TienPhuThu = 0,
                SoNguoiTieuChuanChot = phong.LoaiPhong.SoNguoiTieuChuan,
                PhuThuNguoiThemChot = phong.LoaiPhong.PhuThu,
                TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
            });

            phong.TinhTrang = TinhTrangPhong.OC;
            ketQua.Add(new CheckedInRoomDto { MaPhong = phong.MaPhong, SoNguoi = soNguoi, DonGia = donGia, TinhTrangPhong = nameof(TinhTrangPhong.OC) });
        }

        await CapNhatCccdAsync(khach, thongTinKhach);

        _audit.LyDoTinhTrangPhong = "Nhận phòng cho khách vãng lai";
        _audit.Ghi("NHAN_PHONG_VANG_LAI", phieuNhan.MaPhieuNhan, $"phong={string.Join(",", phongs.Select(p => p.MaPhong))}; khach={khach.MaKhach}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi nhận phòng, vui lòng thử lại.");
        }

        await transaction.CommitAsync();

        return new CheckInResultDto
        {
            MaPhieuNhan = phieuNhan.MaPhieuNhan,
            MaPhieuDat = null,
            TrangThaiPhieuDat = null,
            NgayNhan = phieuNhan.NgayNhan,
            NguoiThucHien = _audit.TenDN,
            PhongDaNhan = ketQua
        };
    }

    private static Dictionary<string, CheckInGuestInfo> LayThongTinKhach(List<CheckInGuestInfo>? danhSach, List<string> phongNhan)
    {
        var ketQua = new Dictionary<string, CheckInGuestInfo>();
        foreach (var info in danhSach ?? new())
        {
            if (!phongNhan.Contains(info.MaPhong))
                throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_KHONG_TRONG_DANH_SACH_NHAN", $"Thông tin khách của phòng {info.MaPhong} không khớp danh sách phòng nhận.", "khach");
            if (info.SoNguoi < 0)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_NGUOI_KHONG_HOP_LE", $"Số người của phòng {info.MaPhong} không được âm.", "khach");
            info.CCCD = ChuanHoaCccd(info.CCCD);
            if (!ketQua.TryAdd(info.MaPhong, info))
                throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_TRUNG_LAP", $"Phòng {info.MaPhong} khai báo khách nhiều lần.", "khach");
        }
        return ketQua;
    }

    private static int LaySoNguoi(Dictionary<string, CheckInGuestInfo> thongTinKhach, Phong phong)
    {
        var soNguoi = thongTinKhach.TryGetValue(phong.MaPhong, out var info) && info.SoNguoi > 0 ? info.SoNguoi : phong.LoaiPhong.SoNguoiTieuChuan;
        var toiDa = phong.LoaiPhong.SoNguoiTieuChuan + NguoiThemToiDa;
        if (soNguoi > toiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "VUOT_SUC_CHUA",
                $"Phòng {phong.MaPhong} ({phong.LoaiPhong.TenLoai}) tiêu chuẩn {phong.LoaiPhong.SoNguoiTieuChuan} người, tối đa {toiDa} người (tính phụ thu người ở thêm); đang khai {soNguoi} người.", "khach");
        return soNguoi;
    }

    /// <summary>Số giấy tờ chỉ gồm chữ và số (6-20 ký tự), cùng quy tắc với hồ sơ khách.</summary>
    private static string? ChuanHoaCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return null;
        var s = cccd.Trim();
        if (s.Length is < 6 or > 20 || !s.All(char.IsLetterOrDigit))
            throw new ApiException(StatusCodes.Status400BadRequest, "CCCD_KHONG_HOP_LE", "Số giấy tờ chỉ gồm chữ và số (6-20 ký tự).", "khach");
        return s;
    }

    private async Task CapNhatCccdAsync(Khach khach, Dictionary<string, CheckInGuestInfo> thongTinKhach)
    {
        if (!string.IsNullOrWhiteSpace(khach.CCCD)) return;
        var cccd = thongTinKhach.Values.Select(x => x.CCCD).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(cccd))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_CCCD", "Khách chưa có CCCD, cần nhập CCCD khi nhận phòng.", "khach");
        // Không để một số giấy tờ gắn cho hai hồ sơ khách (tạo khách trùng không kiểm soát).
        var trung = await _context.Khachs.AsNoTracking().Where(x => x.CCCD == cccd && x.MaKhach != khach.MaKhach).Select(x => new { x.MaKhach, x.HoTen }).FirstOrDefaultAsync();
        if (trung is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "CCCD_DA_TON_TAI",
                $"Số giấy tờ này đã thuộc hồ sơ khách {trung.HoTen} ({trung.MaKhach}). Hãy kiểm tra lại số hoặc dùng đúng hồ sơ khách đó.", "khach");
        khach.CCCD = cccd;
        _audit.Ghi("CAP_NHAT_CCCD_KHI_NHAN_PHONG", khach.MaKhach, $"cccd={GuestService.CheCccd(cccd)}");
    }

    public async Task<CheckInInfoDto> LayThongTinNhanPhongAsync(string maPhieuDat)
    {
        var phieuDat = await _context.PhieuDatPhongs.AsNoTracking()
            .Include(x => x.Khach)
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p!.ChiTietPhieuNhans)
            .Include(x => x.KhachDiCungs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var tongTien = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien);
        var daThuCoc = phieuDat.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN && phieuDat.TienCoc > 0;
        var thoiDiem = phieuDat.NgayTraDuKien <= homNay ? "QUA_HAN"
            : homNay < phieuDat.NgayDonDuKien ? "SOM"
            : homNay > phieuDat.NgayDonDuKien ? "TRE" : "DUNG_NGAY";

        string? lyDoPhieu = phieuDat.TrangThai switch
        {
            TrangThaiPhieuDat.CHO_XAC_NHAN => "Phiếu chưa xác nhận thu cọc – thu cọc ở màn Thông tin đặt phòng trước.",
            TrangThaiPhieuDat.DA_NHAN_PHONG => "Phiếu đã nhận phòng.",
            TrangThaiPhieuDat.DA_HUY => "Phiếu đã hủy.",
            TrangThaiPhieuDat.KHONG_DEN => "Phiếu đã đánh dấu không đến.",
            TrangThaiPhieuDat.DA_TRA_PHONG => "Phiếu đã trả phòng.",
            _ => null
        };
        if (lyDoPhieu is null && thoiDiem == "QUA_HAN")
            lyDoPhieu = $"Phiếu đã quá ngày trả dự kiến ({phieuDat.NgayTraDuKien:dd/MM/yyyy}).";

        var daNhan = phieuDat.PhieuNhanPhong?.ChiTietPhieuNhans.Select(x => x.MaPhong).ToHashSet() ?? new HashSet<string>();
        HashSet<string> daChiemSom = new();
        if (lyDoPhieu is null && thoiDiem == "SOM")
            daChiemSom = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, homNay, phieuDat.NgayDonDuKien, maPhieuDat);

        var canhBao = new List<string>();
        if (thoiDiem == "SOM")
            canhBao.Add($"Khách đến SỚM ({phieuDat.NgayDonDuKien.DayNumber - homNay.DayNumber} ngày trước ngày đón {phieuDat.NgayDonDuKien:dd/MM/yyyy}): phải ghi lý do, phòng phải trống từ hôm nay, tiền phòng tính lại từ hôm nay.");
        if (thoiDiem == "TRE")
            canhBao.Add($"Khách đến TRỄ ({homNay.DayNumber - phieuDat.NgayDonDuKien.DayNumber} ngày sau ngày đón {phieuDat.NgayDonDuKien:dd/MM/yyyy}): phiếu vẫn còn hiệu lực, tiền phòng giữ nguyên theo phiếu.");
        if (string.IsNullOrWhiteSpace(phieuDat.Khach.CCCD))
            canhBao.Add("Hồ sơ khách chưa có số giấy tờ (CCCD/hộ chiếu): bắt buộc nhập khi nhận phòng.");

        var phong = phieuDat.ChiTietPhieuDats.OrderBy(x => x.MaPhong, StringComparer.Ordinal).Select(ct =>
        {
            var p = ct.Phong;
            string? lyDo = null;
            if (daNhan.Contains(p.MaPhong)) lyDo = "Đã nhận";
            else if (lyDoPhieu is not null) lyDo = lyDoPhieu;
            else if (p.TinhTrang is not (TinhTrangPhong.VC or TinhTrangPhong.VI))
                lyDo = p.TinhTrang switch
                {
                    TinhTrangPhong.VD => "Chưa dọn xong – chờ buồng phòng hoặc chọn phòng khác (Đổi phòng).",
                    TinhTrangPhong.OD => "Phòng còn khách đang ở – chờ trả phòng.",
                    TinhTrangPhong.OC => "Phòng đang có khách.",
                    TinhTrangPhong.OOO => "Phòng đang bảo trì/ngừng bán.",
                    _ => $"Phòng đang {p.TinhTrang}."
                };
            else if (daChiemSom.Contains(p.MaPhong)) lyDo = "Phòng đã có khách/đặt chỗ khác trong những ngày nhận sớm.";
            return new CheckInRoomInfoDto
            {
                MaPhong = p.MaPhong, MaLoai = p.MaLoai, TenLoai = p.LoaiPhong.TenLoai, TinhTrang = p.TinhTrang.ToString(),
                DaNhan = daNhan.Contains(p.MaPhong), NhanDuoc = lyDo is null, LyDoKhongNhanDuoc = lyDo,
                SucChuaTieuChuan = p.LoaiPhong.SoNguoiTieuChuan, SucChuaToiDa = p.LoaiPhong.SoNguoiTieuChuan + NguoiThemToiDa,
                DonGiaApDung = ct.DonGiaApDung, ThanhTien = ct.ThanhTien
            };
        }).ToList();

        if (lyDoPhieu is null && phong.Any(x => !x.DaNhan && !x.NhanDuoc))
            canhBao.Add("Có phòng chưa nhận được ngay – chỉ nhận phần phòng đã sẵn sàng (đoàn đến rải rác) hoặc xử lý phòng đó trước.");

        return new CheckInInfoDto
        {
            MaPhieuDat = phieuDat.MaPhieuDat, TrangThai = phieuDat.TrangThai.ToString(),
            MaKhach = phieuDat.MaKhach, HoTenKhach = phieuDat.Khach.HoTen, SoDTKhach = phieuDat.Khach.SoDT,
            CccdDaCo = GuestService.CheCccd(phieuDat.Khach.CCCD), ThieuCccd = string.IsNullOrWhiteSpace(phieuDat.Khach.CCCD),
            LoaiDatPhong = phieuDat.LoaiDatPhong.ToString(),
            NgayDonDuKien = phieuDat.NgayDonDuKien, NgayTraDuKien = phieuDat.NgayTraDuKien,
            SoDem = Math.Max(phieuDat.NgayTraDuKien.DayNumber - phieuDat.NgayDonDuKien.DayNumber, 0),
            HomNay = homNay, ThoiDiem = thoiDiem, TongTienDuKien = tongTien, TienCoc = phieuDat.TienCoc, DaThuCoc = daThuCoc,
            ConLaiDuKien = Math.Max(tongTien - (daThuCoc ? phieuDat.TienCoc : 0), 0),
            SoKhachDiCung = phieuDat.KhachDiCungs.Count, MaPhieuNhan = phieuDat.PhieuNhanPhong?.MaPhieuNhan,
            NhanDuoc = lyDoPhieu is null && phong.Any(x => x.NhanDuoc),
            LyDoKhongNhanDuoc = lyDoPhieu ?? (phong.Any(x => x.NhanDuoc) ? null : "Chưa có phòng nào sẵn sàng để nhận."),
            CanhBao = canhBao, Phong = phong
        };
    }

    private async Task<string> SinhMaPhieuNhanAsync(DateTime bayGio)
    {
        var tienTo = $"CI{bayGio:yyyyMMdd}-";
        // [Khối 8 - P1-4] Khóa theo tiền tố tới hết transaction — xem MaCodeGenerator.GenerateNextAsync.
        return await MaCodeGenerator.GenerateNextAsync(_context,
            _context.PhieuNhanPhongs.Where(x => x.MaPhieuNhan.StartsWith(tienTo)).Select(x => x.MaPhieuNhan), tienTo, 4);
    }
}
