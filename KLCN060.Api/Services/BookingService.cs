using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Finance;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class BookingService : IBookingService
{
    private const int PageSizeToiDa = 100;

    /// <summary>Một phiếu đặt phòng giữ phòng tối đa chừng này đêm (chặn đặt "55 năm" làm kẹt phòng); lưu trú dài hơn thì gia hạn dần hoặc làm hợp đồng riêng.</summary>
    public const int SoDemToiDa = 90;

    internal static void KiemTraSoDem(DateOnly don, DateOnly tra, string truong)
    {
        if (tra.DayNumber - don.DayNumber > SoDemToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_DEM_QUA_DAI", $"Một phiếu đặt phòng tối đa {SoDemToiDa} đêm.", truong);
    }

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;

    public BookingService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CurrentUser user)
    {
        if (!EnumParser.TryParse<LoaiDatPhong>(request.LoaiDatPhong, out var loaiDatPhong))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DAT_PHONG_KHONG_HOP_LE", "LoaiDatPhong không hợp lệ. Chỉ chấp nhận: CA_NHAN, DOAN.", "loaiDatPhong");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (request.NgayDonDuKien < homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_DON_KHONG_HOP_LE", "Ngày đón dự kiến không được ở quá khứ.", "ngayDonDuKien");
        if (request.NgayTraDuKien <= request.NgayDonDuKien)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày đón.", "ngayTraDuKien");
        KiemTraSoDem(request.NgayDonDuKien, request.NgayTraDuKien, "ngayTraDuKien");

        if (request.PhongCanDat is null || request.PhongCanDat.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_CAN_DAT", "Cần chọn ít nhất 1 loại phòng.", "phongCanDat");
        for (var i = 0; i < request.PhongCanDat.Count; i++)
        {
            var dong = request.PhongCanDat[i];
            if (string.IsNullOrWhiteSpace(dong.MaLoai))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_LOAI", "Thiếu mã loại phòng.", $"phongCanDat[{i}].maLoai");
            if (dong.SoLuong < 1)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", "Số lượng phòng phải lớn hơn 0.", $"phongCanDat[{i}].soLuong");
        }

        // Giá đoàn chỉ dành cho đặt từ 2 phòng trở lên: không cho khai "DOAN" với 1 phòng để hưởng giá đoàn (client tự khai nên phải kiểm lại).
        if (loaiDatPhong == LoaiDatPhong.DOAN && request.PhongCanDat.Sum(x => (long)x.SoLuong) < 2)
            throw new ApiException(StatusCodes.Status400BadRequest, "DOAN_CAN_TU_2_PHONG", "Đặt phòng theo đoàn cần từ 2 phòng trở lên. Đặt 1 phòng vui lòng chọn loại cá nhân.", "loaiDatPhong");

        // KH: luôn dùng maKhach trong JWT, bỏ qua giá trị client gửi để không đặt hộ người khác.
        var maKhach = user.LaKhachHang ? user.MaKhach : request.MaKhach;
        if (string.IsNullOrWhiteSpace(maKhach))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_KHACH", "Lễ tân cần chỉ định khách hàng đặt phòng (tạo khách bằng POST /guests nếu chưa có).", "maKhach");
        var khach = await _context.Khachs.AsNoTracking().FirstOrDefaultAsync(x => x.MaKhach == maKhach);
        if (khach is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHACH_HANG", "Không tìm thấy khách hàng.", "maKhach");

        // Gộp các dòng trùng loại; sắp theo mã loại để mọi request khóa theo cùng thứ tự (tránh deadlock).
        var yeuCau = request.PhongCanDat
            .GroupBy(x => x.MaLoai.Trim())
            .Select(g => new { MaLoai = g.Key, SoLuong = (int)Math.Min(g.Sum(x => (long)x.SoLuong), int.MaxValue), ViTri = request.PhongCanDat.FindIndex(x => x.MaLoai.Trim() == g.Key) })
            .OrderBy(x => x.MaLoai, StringComparer.Ordinal)
            .ToList();

        var soDem = request.NgayTraDuKien.DayNumber - request.NgayDonDuKien.DayNumber;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Bảng giá (Quản lý): đơn giá áp dụng của từng đêm theo quy tắc giá đang bật; không có quy tắc thì đúng bằng đơn giá loại phòng.
        var quyTacGia = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);

        var chiTiets = new List<ChiTietPhieuDat>();
        var giaChotTheoPhong = new Dictionary<string, List<GiaDemDatPhong>>();
        var loaiCuaPhong = new Dictionary<string, string>();
        foreach (var dong in yeuCau)
        {
            var loaiPhong = await _context.LoaiPhongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaLoai == dong.MaLoai);
            if (loaiPhong is null)
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", $"Không tìm thấy loại phòng {dong.MaLoai}.", $"phongCanDat[{dong.ViTri}].maLoai");

            // Khóa các phòng của loại này tới hết transaction: request đặt cùng loại phòng đến sau phải chờ,
            // rồi thấy đúng kết quả đã commit (không thể cùng giữ 1 phòng cuối cùng).
            var phongCuaLoai = await _context.Phongs
                .FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaLoai = {dong.MaLoai}")
                .AsNoTracking()
                .ToListAsync();

            var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, request.NgayDonDuKien, request.NgayTraDuKien);
            var daChonTrongRequest = chiTiets.Select(x => x.MaPhong).ToHashSet();

            var phongTrong = phongCuaLoai
                .Where(p => p.TinhTrang != TinhTrangPhong.OOO && !daChiem.Contains(p.MaPhong) && !daChonTrongRequest.Contains(p.MaPhong))
                .OrderBy(p => p.MaPhong, StringComparer.Ordinal)
                .ToList();

            if (phongTrong.Count < dong.SoLuong)
                throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                    $"Loại phòng {dong.MaLoai} chỉ còn {phongTrong.Count} phòng trống trong khoảng ngày đã chọn, không đủ {dong.SoLuong} phòng yêu cầu",
                    $"phongCanDat[{dong.ViTri}].soLuong");

            var gia = GiaPhongCalculator.Tinh(loaiPhong, request.NgayDonDuKien, request.NgayTraDuKien, loaiDatPhong, quyTacGia);
            foreach (var phong in phongTrong.Take(dong.SoLuong))
            {
                chiTiets.Add(new ChiTietPhieuDat
                {
                    MaPhong = phong.MaPhong,
                    SoLuong = 1,
                    DonGiaApDung = gia.DonGiaBinhQuan,
                    ThanhTien = gia.TongTien
                });
                giaChotTheoPhong[phong.MaPhong] = GiaPhongCalculator.TaoGiaChot("", phong.MaPhong, loaiPhong, gia);
                loaiCuaPhong[phong.MaPhong] = dong.MaLoai;
            }
        }

        var tongTien = chiTiets.Sum(x => x.ThanhTien);
        var tongKhuyenMai = 0m;
        var khuyenMaiApDung = new List<(KhuyenMai Km, decimal Tien)>();

        var maKhuyenMais = DanhSachMaKhuyenMai(request);
        if (maKhuyenMais.Count > 0)
        {
            var dongTheoLoai = chiTiets.Select(ct => (MaLoai: loaiCuaPhong[ct.MaPhong], ct.ThanhTien)).ToList();
            var conLai = tongTien;   // tổng các khuyến mãi không vượt tiền phòng
            foreach (var maKm in maKhuyenMais)
            {
                var km = await _context.KhuyenMais.AsNoTracking().FirstOrDefaultAsync(x => x.MaKM == maKm);
                if (km is null)
                    throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHUYEN_MAI", $"Không tìm thấy khuyến mãi {maKm}.", "maKhuyenMais");
                var tenLoaiKm = km.MaLoaiApDung is null ? null : await _context.LoaiPhongs.AsNoTracking().Where(x => x.MaLoai == km.MaLoaiApDung).Select(x => x.TenLoai).FirstOrDefaultAsync();
                var thieu = PromotionRules.DanhGia(km, homNay, soDem, dongTheoLoai, tinhLuot: false, tenLoaiKm).Where(c => !c.Dat).ToList();
                if (thieu.Count > 0)
                {
                    var loi = thieu[0];
                    throw new ApiException(StatusCodes.Status409Conflict,
                        loi.Ma switch { "HOAT_DONG" => "KHUYEN_MAI_TAM_NGUNG", "THOI_GIAN" => "KHUYEN_MAI_HET_HAN", _ => "KHUYEN_MAI_KHONG_DU_DIEU_KIEN" },
                        loi.Ma == "THOI_GIAN" ? $"Khuyến mãi {km.TenKM} không nằm trong thời gian áp dụng." : loi.Ma == "HOAT_DONG" ? $"Khuyến mãi {km.TenKM}: {loi.ChiTiet}" : $"Khuyến mãi {km.TenKM} - {loi.Ten.ToLowerInvariant()}: {loi.ChiTiet}", "maKhuyenMais");
                }

                var tien = Math.Min(PromotionRules.TinhTien(km, dongTheoLoai), conLai);
                conLai -= tien;
                tongKhuyenMai += tien;
                khuyenMaiApDung.Add((km, tien));

                // Tăng lượt dùng nguyên tử, chỉ khi còn lượt — tránh vượt SoLuongGioiHan khi có request đồng thời.
                var daCapNhat = await _context.KhuyenMais
                    .Where(x => x.MaKM == km.MaKM && (x.SoLuongGioiHan == null || x.SoLuongDaSuDung < x.SoLuongGioiHan))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung + 1));
                if (daCapNhat == 0)
                    throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_HET_LUOT", $"Khuyến mãi {km.TenKM} đã hết lượt sử dụng.", "maKhuyenMais");
            }
        }

        var tienCoc = Math.Round(tongTien * 0.5m, 0, MidpointRounding.AwayFromZero);

        var tienTo = $"BK{request.NgayDonDuKien:yyyyMMdd}-";
        // [Khối 8 - P1-4] Khóa phòng ở trên chỉ theo TỪNG loại phòng → 2 đặt phòng khác loại cùng ngày đón không chờ nhau
        // nhưng cùng sinh mã BKyyyyMMdd-NNNN; khóa theo tiền tố tới hết transaction (sau khóa dòng Phong).
        var maPhieuDat = await MaCodeGenerator.GenerateNextAsync(_context,
            _context.PhieuDatPhongs.Where(x => x.MaPhieuDat.StartsWith(tienTo)).Select(x => x.MaPhieuDat), tienTo, 4);

        var phieuDat = new PhieuDatPhong
        {
            MaPhieuDat = maPhieuDat,
            MaKhach = khach.MaKhach,
            NgayDat = DateTime.Now,
            NgayDonDuKien = request.NgayDonDuKien,
            NgayTraDuKien = request.NgayTraDuKien,
            TienCoc = tienCoc,
            TrangThai = TrangThaiPhieuDat.CHO_XAC_NHAN,
            LoaiDatPhong = loaiDatPhong,
            NguonDat = user.LaKhachHang ? NguonDatPhong.WEBSITE : NguonDatPhong.QUAY_LE_TAN
        };
        foreach (var ct in chiTiets)
        {
            ct.MaPhieuDat = phieuDat.MaPhieuDat;
            // Giá chốt từng đêm (snapshot): đổi bảng giá sau này không làm đổi giá phiếu đã đặt.
            foreach (var dem in giaChotTheoPhong[ct.MaPhong]) { dem.MaPhieuDat = phieuDat.MaPhieuDat; ct.GiaTheoDems.Add(dem); }
            phieuDat.ChiTietPhieuDats.Add(ct);
        }
        foreach (var (km, tien) in khuyenMaiApDung)
            phieuDat.ChiTietKhuyenMais.Add(new ChiTietKhuyenMai { MaPhieuDat = phieuDat.MaPhieuDat, MaKM = km.MaKM, TongKhuyenMai = tien });

        _context.PhieuDatPhongs.Add(phieuDat);
        _audit.Ghi("TAO_DAT_PHONG", maPhieuDat,
            $"khach={khach.MaKhach}; nguon={phieuDat.NguonDat}; loai={loaiDatPhong}; ngay={request.NgayDonDuKien:yyyy-MM-dd}..{request.NgayTraDuKien:yyyy-MM-dd}; " +
            $"soPhong={chiTiets.Count}; tongTien={Math.Round(tongTien, 0)}; tienCoc={tienCoc}; khuyenMai={string.Join(",", khuyenMaiApDung.Select(x => x.Km.MaKM))}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo đặt phòng, vui lòng thử lại.");
        }

        await transaction.CommitAsync();

        return await LayChiTietAsync(phieuDat.MaPhieuDat);
    }

    public async Task<(List<BookingSummaryDto> Items, int TotalItems)> SearchAsync(BookingListQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, PageSizeToiDa);

        var q = _context.PhieuDatPhongs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.TrangThai))
        {
            if (!EnumParser.TryParse<TrangThaiPhieuDat>(query.TrangThai, out var trangThai))
                throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE", "TrangThai không hợp lệ.", "trangThai");
            q = q.Where(x => x.TrangThai == trangThai);
        }
        if (!string.IsNullOrWhiteSpace(query.Nguon))
        {
            if (!EnumParser.TryParse<NguonDatPhong>(query.Nguon, out var nguon))
                throw new ApiException(StatusCodes.Status400BadRequest, "NGUON_KHONG_HOP_LE", "Nguon chỉ chấp nhận: WEBSITE, QUAY_LE_TAN.", "nguon");
            q = q.Where(x => x.NguonDat == nguon);
        }
        if (!string.IsNullOrWhiteSpace(query.LoaiDatPhong))
        {
            if (!EnumParser.TryParse<LoaiDatPhong>(query.LoaiDatPhong, out var loai))
                throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DAT_PHONG_KHONG_HOP_LE", "LoaiDatPhong chỉ chấp nhận: CA_NHAN, DOAN.", "loaiDatPhong");
            q = q.Where(x => x.LoaiDatPhong == loai);
        }
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (query.QuaHanChuaNhan == true)
            q = q.Where(x => (x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
                             && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null);

        if (query.TuNgay is { } tu0 && query.DenNgay is { } den0 && tu0 > den0)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "tuNgay");
        var cotNgay = (query.LocTheoNgay ?? "NGAY_DON").Trim().ToUpperInvariant();
        if (cotNgay is not ("NGAY_DON" or "NGAY_DAT" or "NGAY_HUY"))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOC_THEO_NGAY_KHONG_HOP_LE", "LocTheoNgay chỉ chấp nhận: NGAY_DON, NGAY_DAT, NGAY_HUY.", "locTheoNgay");
        if (query.TuNgay is { } tuNgay)
        {
            var tuLuc = tuNgay.ToDateTime(TimeOnly.MinValue);
            q = cotNgay switch
            {
                "NGAY_DAT" => q.Where(x => x.NgayDat >= tuLuc),
                "NGAY_HUY" => q.Where(x => x.NgayHuy != null && x.NgayHuy >= tuLuc),
                _ => q.Where(x => x.NgayDonDuKien >= tuNgay)
            };
        }
        if (query.DenNgay is { } denNgay)
        {
            var denLuc = denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = cotNgay switch
            {
                "NGAY_DAT" => q.Where(x => x.NgayDat < denLuc),
                "NGAY_HUY" => q.Where(x => x.NgayHuy != null && x.NgayHuy < denLuc),
                _ => q.Where(x => x.NgayDonDuKien <= denNgay)
            };
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            // Không phân biệt dấu/hoa thường: "nguyen thi" tìm được "Nguyễn Thị".
            var khongDau = VanBanHelper.KhongDau(tuKhoa);
            q = q.Where(x => EF.Functions.Collate(x.Khach.HoTen, "Latin1_General_100_CI_AI").Contains(khongDau) || x.Khach.SoDT.Contains(tuKhoa) || x.MaPhieuDat.Contains(tuKhoa));
        }

        var total = await q.CountAsync();
        var items = await ChieuSangTomTat(q.OrderByDescending(x => x.NgayDat).ThenByDescending(x => x.MaPhieuDat).Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize), homNay).ToListAsync();
        // Người hủy hiển thị "Họ tên (Mã NV)" thay vì tên đăng nhập.
        var tenNguoiHuy = await TenNguoiDung.HienThiAsync(_context, items.Select(x => x.HuyBoi));
        foreach (var it in items) if (it.HuyBoi is not null) it.HuyBoi = tenNguoiHuy.GetValueOrDefault(it.HuyBoi, it.HuyBoi);

        return (items, total);
    }

    public async Task<List<BookingSummaryDto>> GetMineAsync(string? maKhach)
    {
        if (string.IsNullOrEmpty(maKhach))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Tài khoản không gắn với khách hàng nào.");

        return await ChieuSangTomTat(_context.PhieuDatPhongs.Where(x => x.MaKhach == maKhach).OrderByDescending(x => x.NgayDat), DateOnly.FromDateTime(DateTime.Now)).ToListAsync();
    }

    public async Task<BookingDto> GetByIdAsync(string maPhieuDat, CurrentUser user)
    {
        var dto = await LayChiTietAsync(maPhieuDat);
        KiemTraQuyenSoHuu(dto.MaKhach, user);
        return dto;
    }

    public async Task<BookingDto> ConfirmDepositAsync(string maPhieuDat, CurrentUser user)
    {
        // Hai lễ tân bấm "xác nhận cọc" cùng lúc (hoặc khách vừa chuyển khoản QR đúng lúc): khóa phiếu để chỉ MỘT lần ghi nhận thu cọc,
        // người sau nhận 409 và không ghi đè người thu/thời điểm/hình thức của lần đầu.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var phieuDat = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        KiemTraQuyenSoHuu(phieuDat.MaKhach, user);

        if (phieuDat.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE",
                phieuDat.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN
                    ? $"Phiếu {maPhieuDat} đã được xác nhận thu cọc{(phieuDat.NguoiNhanCoc is null ? "" : $" bởi {phieuDat.NguoiNhanCoc}")}{(phieuDat.NgayNhanCoc is null ? "" : $" lúc {phieuDat.NgayNhanCoc:dd/MM/yyyy HH:mm}")}. Không thu cọc lần hai."
                    : $"Chỉ xác nhận đặt cọc được khi phiếu đang ở trạng thái CHO_XAC_NHAN (hiện tại: {phieuDat.TrangThai}).");

        if (phieuDat.NgayDonDuKien < DateOnly.FromDateTime(DateTime.Now))
            throw new ApiException(StatusCodes.Status409Conflict, "PHIEU_QUA_NGAY_DON",
                $"Phiếu {maPhieuDat} đã quá ngày đón dự kiến ({phieuDat.NgayDonDuKien:dd/MM/yyyy}), không thu cọc để giữ phòng nữa: hãy sửa ngày đón hoặc đánh dấu không đến/hủy phiếu.");

        phieuDat.TrangThai = TrangThaiPhieuDat.DA_XAC_NHAN;
        phieuDat.NgayNhanCoc = DateTime.Now;
        phieuDat.HinhThucCoc = HinhThucThanhToan.TIEN_MAT;
        phieuDat.NguoiNhanCoc = user.TenDN;
        _audit.Ghi("XAC_NHAN_COC", maPhieuDat, $"tienCoc={Math.Round(phieuDat.TienCoc, 0)}; hinhThuc=TIEN_MAT; thuBoi={user.TenDN}; trangThai=CHO_XAC_NHAN->DA_XAC_NHAN");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Cọc đã được xác nhận (lễ tân thu tiền mặt): hủy các mã QR chuyển khoản đang chờ để không ai chuyển tiền cho một khoản đã trả.
        await HuyChuyenKhoanDangChoAsync(maPhieuDat);

        return await LayChiTietAsync(maPhieuDat);
    }

    /// <summary>
    /// Sửa phiếu đặt phòng CHƯA nhận phòng (lễ tân hoặc Quản lý): đổi ngày đón/trả, đổi phòng (chỉ định phòng khác) hoặc đổi loại phòng (hệ thống chọn phòng trống của loại mới) cho từng dòng.
    /// Phòng giữ nguyên phải còn trống trong khoảng ngày mới; đơn giá tính lại theo bảng giá hiện hành (dòng bị đổi phòng luôn tính theo phòng mới), khuyến mãi tính lại theo tổng mới.
    /// Phiếu chờ xác nhận cọc: tiền cọc tính lại (50%) và mã QR đang chờ bị hủy; phiếu đã thu cọc: giữ nguyên số cọc đã thu. Bắt buộc ghi lý do; nhật ký lưu ngày/phòng/tổng tiền trước và sau.
    /// </summary>
    public async Task<BookingDto> UpdateAsync(string maPhieuDat, UpdateBookingRequest request, CurrentUser user)
    {
        await SuaPhieuAsync(maPhieuDat, request, user, xemTruoc: false);
        return await LayChiTietAsync(maPhieuDat);
    }

    /// <summary>Xem trước thay đổi: chạy đúng các bước kiểm tra và tính tiền của sửa thật trong một transaction rồi hủy transaction (không lưu gì, không ghi nhật ký).</summary>
    public Task<BookingChangePreviewDto> PreviewUpdateAsync(string maPhieuDat, UpdateBookingRequest request, CurrentUser user)
        => SuaPhieuAsync(maPhieuDat, request, user, xemTruoc: true);

    private async Task<BookingChangePreviewDto> SuaPhieuAsync(string maPhieuDat, UpdateBookingRequest request, CurrentUser user, bool xemTruoc)
    {
        if (user.LaKhachHang
            || !(await _permissions.CoQuyenAsync(user.TenDN, Permissions.ThaoTacLeTan) || await _permissions.CoQuyenAsync(user.TenDN, Permissions.DuyetNghiepVu)))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_SUA", "Chỉ lễ tân hoặc Quản lý mới sửa được phiếu đặt phòng.");
        if (!xemTruoc && string.IsNullOrWhiteSpace(request.LyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do khi sửa phiếu đặt phòng (thay đổi ảnh hưởng giá tiền, có ghi nhật ký).", "lyDo");

        var doiPhongs = request.DoiPhong ?? new List<DoiPhongDatRequest>();
        for (var i = 0; i < doiPhongs.Count; i++)
        {
            var d = doiPhongs[i];
            if (string.IsNullOrWhiteSpace(d.MaPhongCu))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_CU", "Cần chỉ định phòng đang giữ muốn đổi.", $"doiPhong[{i}].maPhongCu");
            var coPhong = !string.IsNullOrWhiteSpace(d.MaPhongMoi);
            var coLoai = !string.IsNullOrWhiteSpace(d.MaLoaiMoi);
            if (coPhong == coLoai)
                throw new ApiException(StatusCodes.Status400BadRequest, "CHON_PHONG_HOAC_LOAI", "Mỗi dòng đổi phòng phải chỉ định đúng một trong hai: phòng mới hoặc loại phòng mới.", $"doiPhong[{i}]");
            d.MaPhongCu = d.MaPhongCu.Trim();
            d.MaPhongMoi = d.MaPhongMoi?.Trim();
            d.MaLoaiMoi = d.MaLoaiMoi?.Trim();
        }
        if (doiPhongs.Select(x => x.MaPhongCu).Distinct().Count() != doiPhongs.Count)
            throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_CU_TRUNG_LAP", "Mỗi phòng đang giữ chỉ được đổi một lần trong một yêu cầu.", "doiPhong");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var phieuDat = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.ChiTietKhuyenMais).ThenInclude(k => k.KhuyenMai)
            .Include(x => x.PhieuNhanPhong)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        if (phieuDat.TrangThai == TrangThaiPhieuDat.DA_NHAN_PHONG || phieuDat.PhieuNhanPhong is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "KHONG_THE_SUA_DA_NHAN_PHONG", "Phiếu đặt phòng đã nhận phòng: dùng Đổi phòng / Gia hạn ở lượt lưu trú thay vì sửa phiếu.");
        if (phieuDat.TrangThai is not (TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN))
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Không thể sửa phiếu đặt phòng đang ở trạng thái {phieuDat.TrangThai}.");

        var don = request.NgayDonDuKien ?? phieuDat.NgayDonDuKien;
        var tra = request.NgayTraDuKien ?? phieuDat.NgayTraDuKien;
        var doiNgay = don != phieuDat.NgayDonDuKien || tra != phieuDat.NgayTraDuKien;
        if (request.NgayDonDuKien is { } ngayDonMoi && ngayDonMoi != phieuDat.NgayDonDuKien && ngayDonMoi < homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_DON_KHONG_HOP_LE", "Ngày đón dự kiến không được ở quá khứ.", "ngayDonDuKien");
        if (tra <= don)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày đón.", "ngayTraDuKien");
        if (doiNgay) KiemTraSoDem(don, tra, "ngayTraDuKien");
        if (!doiNgay && doiPhongs.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_CO_THAY_DOI", "Ngày đón, ngày trả và phòng không thay đổi so với phiếu hiện tại.");

        var hienCo = phieuDat.ChiTietPhieuDats.ToDictionary(x => x.MaPhong);
        var giaCu = phieuDat.ChiTietPhieuDats.ToDictionary(x => x.MaPhong, x => (x.DonGiaApDung, x.ThanhTien));
        var khuyenMaiCu = phieuDat.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai);
        foreach (var d in doiPhongs)
            if (!hienCo.ContainsKey(d.MaPhongCu))
                throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_KHONG_THUOC_PHIEU_DAT", $"Phòng {d.MaPhongCu} không thuộc phiếu đặt phòng này.", "doiPhong");
        // Đổi phòng chỉ trong CÙNG loại phòng khách đã đặt: đổi sang loại khác là nâng/hạ hạng (giá, cọc, khuyến mãi khác) phải làm khi khách nhận phòng hoặc đặt lại.
        foreach (var d in doiPhongs.Where(x => x.MaLoaiMoi is not null))
            if (d.MaLoaiMoi != hienCo[d.MaPhongCu].Phong.MaLoai)
                throw new ApiException(StatusCodes.Status409Conflict, "DOI_KHAC_LOAI_PHONG",
                    $"Phòng {d.MaPhongCu} thuộc loại {hienCo[d.MaPhongCu].Phong.MaLoai}: chỉ đổi được sang phòng cùng loại khách đã đặt.", "doiPhong");

        // Khóa mọi phòng liên quan (đang giữ + phòng mới chỉ định + các phòng của loại được chọn) theo thứ tự cố định để không deadlock với đặt phòng/nhận phòng khác.
        var canKhoa = hienCo.Keys.Concat(doiPhongs.Where(x => x.MaPhongMoi is not null).Select(x => x.MaPhongMoi!)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        foreach (var maPhong in canKhoa)
            await _context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}").AsNoTracking().ToListAsync();
        foreach (var maLoai in doiPhongs.Where(x => x.MaLoaiMoi is not null).Select(x => x.MaLoaiMoi!).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == maLoai))
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", $"Không tìm thấy loại phòng {maLoai}.", "doiPhong");
            await _context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaLoai = {maLoai}").AsNoTracking().ToListAsync();
        }

        var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, don, tra, maPhieuDat);
        var phongBiDoi = doiPhongs.Select(x => x.MaPhongCu).ToHashSet();
        var giuNguyen = phieuDat.ChiTietPhieuDats.Where(ct => !phongBiDoi.Contains(ct.MaPhong)).ToList();

        // Phòng giữ nguyên chỉ cần kiểm tra lại khi ngày thay đổi (nếu ngày không đổi thì phiếu vẫn giữ đúng như đã được nhận).
        if (doiNgay)
        {
            var khongTrong = giuNguyen.Where(ct => daChiem.Contains(ct.MaPhong) || ct.Phong.TinhTrang == TinhTrangPhong.OOO).Select(ct => ct.MaPhong).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (khongTrong.Count > 0)
                throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                    $"Phòng {string.Join(", ", khongTrong)} không trống trong khoảng ngày mới ({don:dd/MM/yyyy} - {tra:dd/MM/yyyy}).", "ngayDonDuKien");
        }

        var daChon = giuNguyen.Select(x => x.MaPhong).ToHashSet();
        var phongMoiTheoCu = new List<(string Cu, Phong Moi)>();
        foreach (var d in doiPhongs)
        {
            Phong? phongMoi;
            if (d.MaPhongMoi is not null)
            {
                phongMoi = await _context.Phongs.Include(x => x.LoaiPhong).FirstOrDefaultAsync(x => x.MaPhong == d.MaPhongMoi)
                    ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", $"Không tìm thấy phòng {d.MaPhongMoi}.", "doiPhong");
                if (phongMoi.MaLoai != hienCo[d.MaPhongCu].Phong.MaLoai)
                    throw new ApiException(StatusCodes.Status409Conflict, "DOI_KHAC_LOAI_PHONG",
                        $"Phòng {phongMoi.MaPhong} thuộc loại {phongMoi.MaLoai}, khác loại {hienCo[d.MaPhongCu].Phong.MaLoai} khách đã đặt: chỉ đổi được sang phòng cùng loại.", "doiPhong");
                if (daChon.Contains(phongMoi.MaPhong) || hienCo.ContainsKey(phongMoi.MaPhong))
                    throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TRONG_PHIEU", $"Phòng {phongMoi.MaPhong} đã thuộc phiếu đặt phòng này.", "doiPhong");
                if (phongMoi.TinhTrang == TinhTrangPhong.OOO || daChiem.Contains(phongMoi.MaPhong))
                    throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                        $"Phòng {phongMoi.MaPhong} không trống trong khoảng ngày {don:dd/MM/yyyy} - {tra:dd/MM/yyyy} (hoặc đang bảo trì).", "doiPhong");
            }
            else
            {
                var ungVien = await _context.Phongs.Include(x => x.LoaiPhong)
                    .Where(x => x.MaLoai == d.MaLoaiMoi && x.TinhTrang != TinhTrangPhong.OOO).OrderBy(x => x.MaPhong).ToListAsync();
                phongMoi = ungVien.FirstOrDefault(p => !daChiem.Contains(p.MaPhong) && !daChon.Contains(p.MaPhong) && !hienCo.ContainsKey(p.MaPhong))
                    ?? throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                        $"Loại phòng {d.MaLoaiMoi} không còn phòng trống trong khoảng ngày {don:dd/MM/yyyy} - {tra:dd/MM/yyyy}.", "doiPhong");
            }
            daChon.Add(phongMoi.MaPhong);
            phongMoiTheoCu.Add((d.MaPhongCu, phongMoi));
        }

        var quyTacGia = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
        var tongCu = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien);
        if (doiNgay)
        {
            // Đổi ngày ở → TÍNH LẠI giá từng đêm theo bảng giá hiện hành và chốt lại (đây là một trong hai lúc duy nhất giá được tính lại).
            var maGiu = giuNguyen.Select(c => c.MaPhong).ToList();
            await _context.GiaDemDatPhongs.Where(x => x.MaPhieuDat == maPhieuDat && maGiu.Contains(x.MaPhong)).ExecuteDeleteAsync();
            foreach (var ct in giuNguyen)
            {
                var gia = GiaPhongCalculator.Tinh(ct.Phong.LoaiPhong, don, tra, phieuDat.LoaiDatPhong, quyTacGia);
                ct.DonGiaApDung = gia.DonGiaBinhQuan;
                ct.ThanhTien = gia.TongTien;
                _context.GiaDemDatPhongs.AddRange(GiaPhongCalculator.TaoGiaChot(maPhieuDat, ct.MaPhong, ct.Phong.LoaiPhong, gia));
            }
        }
        var tongMoi = giuNguyen.Sum(x => x.ThanhTien);
        var dongMoi = giuNguyen.Select(ct => (MaLoai: ct.Phong.MaLoai, ct.ThanhTien)).ToList();
        foreach (var (cu, moi) in phongMoiTheoCu)
        {
            var gia = GiaPhongCalculator.Tinh(moi.LoaiPhong, don, tra, phieuDat.LoaiDatPhong, quyTacGia);
            _context.ChiTietPhieuDats.Remove(hienCo[cu]);
            _context.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = maPhieuDat, MaPhong = moi.MaPhong, SoLuong = 1, DonGiaApDung = gia.DonGiaBinhQuan, ThanhTien = gia.TongTien });
            _context.GiaDemDatPhongs.AddRange(GiaPhongCalculator.TaoGiaChot(maPhieuDat, moi.MaPhong, moi.LoaiPhong, gia));
            tongMoi += gia.TongTien;
            dongMoi.Add((moi.MaLoai, gia.TongTien));
            // Khách đi cùng đã xếp vào phòng cũ thì chuyển theo sang phòng mới.
            await _context.KhachDiCungs.Where(x => x.MaPhieuDat == maPhieuDat && x.MaPhong == cu).ExecuteUpdateAsync(s => s.SetProperty(x => x.MaPhong, moi.MaPhong));
        }
        foreach (var km in phieuDat.ChiTietKhuyenMais)
        {
            // Khuyến mãi đã áp dụng phải còn đúng điều kiện (số đêm / giá trị / phạm vi loại phòng) sau khi đổi ngày - phòng; không thì phải xử lý có chủ đích.
            var soDemMoi = Math.Max(tra.DayNumber - don.DayNumber, 0);
            var khongDat = PromotionRules.DanhGia(km.KhuyenMai, DateOnly.FromDateTime(phieuDat.NgayDat), soDemMoi, dongMoi, tinhLuot: false)
                .Where(c => !c.Dat && c.Ma is "PHAM_VI" or "SO_DEM" or "GIA_TRI").ToList();
            if (khongDat.Count > 0)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_KHONG_CON_DU_DIEU_KIEN",
                    $"Sau thay đổi này đặt phòng không còn đủ điều kiện khuyến mãi {km.KhuyenMai.TenKM} ({km.MaKM}): {khongDat[0].Ten} - {khongDat[0].ChiTiet} Hãy giữ điều kiện hoặc hủy đặt phòng và tạo lại.");
            km.TongKhuyenMai = PromotionRules.TinhTien(km.KhuyenMai, dongMoi);
        }

        var tienCocCu = phieuDat.TienCoc;
        var ngayCu = $"{phieuDat.NgayDonDuKien:yyyy-MM-dd}..{phieuDat.NgayTraDuKien:yyyy-MM-dd}";
        var choXacNhan = phieuDat.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN;
        var donCu = phieuDat.NgayDonDuKien;
        var traCu = phieuDat.NgayTraDuKien;
        phieuDat.NgayDonDuKien = don;
        phieuDat.NgayTraDuKien = tra;
        if (choXacNhan) phieuDat.TienCoc = Math.Round(tongMoi * 0.5m, 0, MidpointRounding.AwayFromZero);   // chưa thu cọc: cọc theo tổng mới

        if (xemTruoc)
        {
            var coc50 = Math.Round(tongMoi * 0.5m, 0, MidpointRounding.AwayFromZero);
            var xem = new BookingChangePreviewDto
            {
                MaPhieuDat = maPhieuDat, TrangThai = phieuDat.TrangThai.ToString(),
                NgayDonCu = donCu, NgayTraCu = traCu, NgayDonMoi = don, NgayTraMoi = tra,
                SoDemCu = traCu.DayNumber - donCu.DayNumber, SoDemMoi = tra.DayNumber - don.DayNumber,
                TongCu = tongCu, TongMoi = tongMoi, ChenhLech = tongMoi - tongCu,
                KhuyenMaiCu = khuyenMaiCu, KhuyenMaiMoi = phieuDat.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai),
                TienCocCu = tienCocCu, TienCocMoi = phieuDat.TienCoc, DaThuCoc = !choXacNhan,
                CocLechSoVoiChuan = choXacNhan ? 0m : coc50 - phieuDat.TienCoc,
                HuyMaQrChoCoc = choXacNhan && phieuDat.TienCoc != tienCocCu
            };
            foreach (var ct in giuNguyen)
            {
                var (donGia, thanhTien) = giaCu[ct.MaPhong];
                xem.Phongs.Add(new BookingChangeRoomDto { MaPhong = ct.MaPhong, MaLoai = ct.Phong.MaLoai, DonGiaCu = donGia, DonGiaMoi = ct.DonGiaApDung, ThanhTienCu = thanhTien, ThanhTienMoi = ct.ThanhTien });
            }
            foreach (var (cu, moi) in phongMoiTheoCu)
            {
                var gia = GiaPhongCalculator.Tinh(moi.LoaiPhong, don, tra, phieuDat.LoaiDatPhong, quyTacGia);
                var (donGia, thanhTien) = giaCu[cu];
                xem.Phongs.Add(new BookingChangeRoomDto { MaPhong = moi.MaPhong, MaLoai = moi.MaLoai, DoiTuPhong = cu, DonGiaCu = donGia, DonGiaMoi = gia.DonGiaBinhQuan, ThanhTienCu = thanhTien, ThanhTienMoi = gia.TongTien });
            }
            xem.Phongs = xem.Phongs.OrderBy(x => x.MaPhong, StringComparer.Ordinal).ToList();
            await transaction.RollbackAsync();
            return xem;
        }

        var chiTietDoiPhong = phongMoiTheoCu.Count == 0 ? ""
            : "; phong=" + string.Join(",", phongMoiTheoCu.Select(x => $"{x.Cu}->{x.Moi.MaPhong}"))
              + "; loai=" + string.Join(",", phongMoiTheoCu.Select(x => $"{hienCo[x.Cu].Phong.MaLoai}->{x.Moi.MaLoai}"));
        _audit.Ghi("SUA_DAT_PHONG", maPhieuDat,
            $"ngayCu={ngayCu}; ngayMoi={don:yyyy-MM-dd}..{tra:yyyy-MM-dd}; tongCu={Math.Round(tongCu, 0)}; tongMoi={Math.Round(tongMoi, 0)}; " +
            $"tienCoc={Math.Round(tienCocCu, 0)}->{Math.Round(phieuDat.TienCoc, 0)}; trangThai={phieuDat.TrangThai}{chiTietDoiPhong}; lyDo={request.LyDo.Trim()}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi sửa đặt phòng, vui lòng thử lại.");
        }

        // Số cọc phải chuyển đã đổi: mã QR cũ (số tiền cũ) không còn dùng được.
        if (choXacNhan) await HuyChuyenKhoanDangChoAsync(maPhieuDat);

        await transaction.CommitAsync();
        return new BookingChangePreviewDto { MaPhieuDat = maPhieuDat, TrangThai = phieuDat.TrangThai.ToString() };
    }

    /// <summary>Danh sách phòng có thể đổi cho một phòng đang giữ: cùng loại, còn trống trong khoảng ngày (không tính chính phiếu này), không bảo trì, không phải phòng phiếu đang giữ.</summary>
    public async Task<List<KLCN060.Api.DTOs.FrontDesk.VacantRoomDto>> GetSwapRoomsAsync(string maPhieuDat, string maPhongCu, DateOnly? nhan, DateOnly? tra)
    {
        var phieu = await _context.PhieuDatPhongs.AsNoTracking().Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");
        var cu = phieu.ChiTietPhieuDats.FirstOrDefault(x => x.MaPhong == maPhongCu)
            ?? throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_KHONG_THUOC_PHIEU_DAT", $"Phòng {maPhongCu} không thuộc phiếu đặt phòng này.", "maPhongCu");
        var don = nhan ?? phieu.NgayDonDuKien;
        var den = tra ?? phieu.NgayTraDuKien;
        if (den <= don)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày đón.", "tra");

        var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, don, den, maPhieuDat);
        var giu = phieu.ChiTietPhieuDats.Select(x => x.MaPhong).ToHashSet();
        var phongs = await _context.Phongs.AsNoTracking()
            .Where(x => x.MaLoai == cu.Phong.MaLoai && x.TinhTrang != TinhTrangPhong.OOO)
            .OrderBy(x => x.Tang).ThenBy(x => x.MaPhong)
            .Select(x => new { x.MaPhong, x.TenPhong, x.Tang, x.MaLoai, TenLoai = x.LoaiPhong.TenLoai, x.LoaiPhong.DonGia, x.TinhTrang }).ToListAsync();
        return phongs.Where(x => !daChiem.Contains(x.MaPhong) && !giu.Contains(x.MaPhong)).Select(x => new KLCN060.Api.DTOs.FrontDesk.VacantRoomDto
        {
            MaPhong = x.MaPhong, TenPhong = x.TenPhong, Tang = x.Tang, MaLoai = x.MaLoai, TenLoai = x.TenLoai, DonGia = x.DonGia, TinhTrang = x.TinhTrang.ToString()
        }).ToList();
    }

    public async Task<CancelBookingResultDto> CancelAsync(string maPhieuDat, CancelBookingRequest request, CurrentUser user)
    {
        // Phân nhánh theo QUYỀN (D15) chứ không theo tên vai trò: hoàn cọc luôn theo chính sách (hủy trước 30 ngày thì hoàn);
        // người hủy mà không phải lễ tân (Quản lý) bắt buộc ghi lý do để nhật ký truy vết được.
        var laNhanVien = !user.LaKhachHang;
        var coQuyenDuyet = (laNhanVien && await _permissions.CoQuyenAsync(user.TenDN, Permissions.DuyetNghiepVu));
        var coQuyenLeTan = laNhanVien && await _permissions.CoQuyenAsync(user.TenDN, Permissions.ThaoTacLeTan);
        if (laNhanVien && !coQuyenDuyet && !coQuyenLeTan)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_HUY", "Vai trò của bạn không được hủy phiếu đặt phòng.");
        if (laNhanVien && !coQuyenLeTan && string.IsNullOrWhiteSpace(request.LyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do khi Quản lý hủy phiếu đặt phòng (xử lý ngoại lệ).", "lyDo");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var phieuDat = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .Include(x => x.ChiTietKhuyenMais)
            .Include(x => x.PhieuNhanPhong)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        KiemTraQuyenSoHuu(phieuDat.MaKhach, user);

        // Nhận phòng một phần vẫn để trạng thái DA_XAC_NHAN nên phải kiểm tra thêm sự tồn tại của phiếu nhận.
        if (phieuDat.TrangThai == TrangThaiPhieuDat.DA_NHAN_PHONG || phieuDat.PhieuNhanPhong is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "KHONG_THE_HUY_DA_NHAN_PHONG", "Phiếu đặt phòng đã nhận phòng, vui lòng liên hệ trực tiếp lễ tân để được hỗ trợ.");
        if (phieuDat.TrangThai is not (TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN))
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Không thể hủy phiếu đặt phòng đang ở trạng thái {phieuDat.TrangThai}.");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var soNgayConLai = phieuDat.NgayDonDuKien.DayNumber - homNay.DayNumber;
        var hoanCoc = soNgayConLai >= 30;
        var daDatCoc = phieuDat.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN;

        phieuDat.TrangThai = TrangThaiPhieuDat.DA_HUY;
        phieuDat.NgayHuy = DateTime.Now;
        phieuDat.LyDoHuy = request.LyDo?.Trim();
        phieuDat.HuyBoi = user.TenDN;

        // Phiếu đã hủy thì không còn gì để thanh toán: hủy mã QR chuyển khoản đang chờ (nếu có).
        await HuyChuyenKhoanDangChoAsync(maPhieuDat);

        // Trả lại lượt sử dụng khuyến mãi mà phiếu này đã chiếm.
        foreach (var km in phieuDat.ChiTietKhuyenMais)
        {
            var maKM = km.MaKM;
            await _context.KhuyenMais
                .Where(x => x.MaKM == maKM && x.SoLuongDaSuDung > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung - 1));
        }

        var soTienHoan = hoanCoc && daDatCoc ? phieuDat.TienCoc : 0m;
        phieuDat.SoTienHoanCoc = soTienHoan;
        phieuDat.TienCocGiuLai = daDatCoc && !hoanCoc ? phieuDat.TienCoc : 0m;

        _audit.Ghi("HUY_DAT_PHONG", maPhieuDat,
            $"hoanCoc={hoanCoc}; soTienHoan={Math.Round(soTienHoan, 0)}; soNgayConLai={soNgayConLai}; " +
            $"nguoiHuy={(user.LaKhachHang ? "khach" : "nhan vien")}; tienCoc={Math.Round(phieuDat.TienCoc, 0)}; coHoanTien={soTienHoan > 0}; lyDo={request.LyDo}");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CancelBookingResultDto
        {
            MaPhieuDat = maPhieuDat,
            TrangThai = nameof(TrangThaiPhieuDat.DA_HUY),
            HoanCoc = hoanCoc && daDatCoc,   // phiếu chưa thu cọc thì không có cọc nào để hoàn
            SoNgayConLai = soNgayConLai,
            SoTienHoan = soTienHoan
        };
    }

    public async Task<BookingDto> MarkNoShowAsync(string maPhieuDat, NoShowRequest request, CurrentUser user)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var phieuDat = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .Include(x => x.PhieuNhanPhong)
            .Include(x => x.ChiTietKhuyenMais)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        if (phieuDat.PhieuNhanPhong is not null || phieuDat.TrangThai == TrangThaiPhieuDat.DA_NHAN_PHONG)
            throw new ApiException(StatusCodes.Status409Conflict, "KHONG_THE_DANH_DAU_KHONG_DEN", "Khách đã nhận phòng nên không thể đánh dấu không đến.");
        if (phieuDat.TrangThai is not (TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN))
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Chỉ đánh dấu không đến được với phiếu đang giữ phòng (hiện tại: {phieuDat.TrangThai}).");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (phieuDat.NgayDonDuKien >= homNay)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_QUA_NGAY_DON",
                $"Chỉ đánh dấu không đến sau ngày đón dự kiến ({phieuDat.NgayDonDuKien:dd/MM/yyyy}); khách vẫn có thể đến trong hôm nay.");

        var daDatCoc = phieuDat.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN;
        phieuDat.TrangThai = TrangThaiPhieuDat.KHONG_DEN;
        phieuDat.NgayHuy = DateTime.Now;
        phieuDat.LyDoHuy = string.IsNullOrWhiteSpace(request.LyDo) ? "Khách không đến nhận phòng" : request.LyDo.Trim();
        phieuDat.HuyBoi = user.TenDN;
        phieuDat.SoTienHoanCoc = 0m; // không đến = mất cọc (khớp chính sách hủy đã công bố)
        phieuDat.TienCocGiuLai = daDatCoc ? phieuDat.TienCoc : 0m;
        await HuyChuyenKhoanDangChoAsync(maPhieuDat);

        // Phòng được giải phóng nên lượt khuyến mãi cũng được trả lại, giống khi hủy phiếu.
        foreach (var km in phieuDat.ChiTietKhuyenMais)
        {
            var maKM = km.MaKM;
            await _context.KhuyenMais
                .Where(x => x.MaKM == maKM && x.SoLuongDaSuDung > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung - 1));
        }

        _audit.Ghi("KHONG_DEN", maPhieuDat, $"matCoc={(daDatCoc ? Math.Round(phieuDat.TienCoc, 0) : 0)}; ngayDonDuKien={phieuDat.NgayDonDuKien:yyyy-MM-dd}; lyDo={phieuDat.LyDoHuy}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LayChiTietAsync(maPhieuDat);
    }

    private Task<int> HuyChuyenKhoanDangChoAsync(string maPhieuDat)
        => _context.GiaoDichChuyenKhoans
            .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC && x.MaThamChieu == maPhieuDat
                        && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.DA_HUY));

    private static void KiemTraQuyenSoHuu(string maKhachCuaPhieu, CurrentUser user)
    {
        if (user.LaKhachHang && user.MaKhach != maKhachCuaPhieu)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Bạn không có quyền truy cập phiếu đặt phòng này.");
    }

    private async Task<BookingDto> LayChiTietAsync(string maPhieuDat)
    {
        var phieuDat = await _context.PhieuDatPhongs
            .AsNoTracking()
            .Include(x => x.Khach)
            .Include(x => x.ChiTietKhuyenMais)
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.KhachDiCungs)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        return new BookingDto
        {
            MaPhieuDat = phieuDat.MaPhieuDat,
            MaKhach = phieuDat.MaKhach,
            HoTenKhach = phieuDat.Khach.HoTen,
            LoaiDatPhong = phieuDat.LoaiDatPhong.ToString(),
            TrangThai = phieuDat.TrangThai.ToString(),
            NguonDat = phieuDat.NguonDat.ToString(),
            NgayHuy = phieuDat.NgayHuy,
            LyDoHuy = phieuDat.LyDoHuy,
            HuyBoi = phieuDat.HuyBoi,
            SoTienHoanCoc = phieuDat.SoTienHoanCoc,
            TienCocGiuLai = phieuDat.TienCocGiuLai,
            NgayDat = phieuDat.NgayDat,
            NgayDonDuKien = phieuDat.NgayDonDuKien,
            NgayTraDuKien = phieuDat.NgayTraDuKien,
            TienCoc = phieuDat.TienCoc,
            TongTienDuKien = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien),
            TongKhuyenMai = phieuDat.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai),
            PhongDaGiu = phieuDat.ChiTietPhieuDats
                .OrderBy(x => x.MaPhong, StringComparer.Ordinal)
                .Select(x => new HeldRoomDto { MaPhong = x.MaPhong, MaLoai = x.Phong.MaLoai, TenLoai = x.Phong.LoaiPhong.TenLoai, DonGiaApDung = x.DonGiaApDung, ThanhTien = x.ThanhTien })
                .ToList(),
            KhachDiCung = phieuDat.KhachDiCungs.OrderBy(x => x.MaKhachDiCung).Select(ToCompanionDto).ToList()
        };
    }

    private static IQueryable<BookingSummaryDto> ChieuSangTomTat(IQueryable<PhieuDatPhong> q, DateOnly homNay) => q.Select(x => new BookingSummaryDto
    {
        MaPhieuDat = x.MaPhieuDat,
        MaKhach = x.MaKhach,
        HoTenKhach = x.Khach.HoTen,
        SoDTKhach = x.Khach.SoDT,
        LoaiDatPhong = x.LoaiDatPhong.ToString(),
        TrangThai = x.TrangThai.ToString(),
        NguonDat = x.NguonDat.ToString(),
        NgayDat = x.NgayDat,
        NgayDonDuKien = x.NgayDonDuKien,
        NgayTraDuKien = x.NgayTraDuKien,
        SoPhong = x.ChiTietPhieuDats.Count,
        TienCoc = x.TienCoc,
        TongTienDuKien = x.ChiTietPhieuDats.Sum(ct => (decimal?)ct.ThanhTien) ?? 0,
        NgayHuy = x.NgayHuy,
        LyDoHuy = x.LyDoHuy,
        HuyBoi = x.HuyBoi,
        SoTienHoanCoc = x.SoTienHoanCoc,
        TienCocGiuLai = x.TienCocGiuLai,
        QuaHanChuaNhan = (x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
                         && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null,
        DaThuCoc = x.NgayNhanCoc != null
    });

    /// <summary>Số khuyến mãi áp dụng cùng lúc cho một đặt phòng.</summary>
    public const int SoKhuyenMaiToiDa = 3;

    /// <summary>Gộp <c>MaKhuyenMais</c> và <c>MaKhuyenMai</c> (cũ): bỏ trống / trùng, tối đa <see cref="SoKhuyenMaiToiDa"/>.</summary>
    internal static List<string> DanhSachMaKhuyenMai(CreateBookingRequest request)
    {
        var ds = (request.MaKhuyenMais ?? new List<string>()).Append(request.MaKhuyenMai ?? "")
            .Select(x => x?.Trim() ?? "").Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ds.Count > SoKhuyenMaiToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUA_NHIEU_KHUYEN_MAI", $"Mỗi đặt phòng áp dụng tối đa {SoKhuyenMaiToiDa} khuyến mãi.", "maKhuyenMais");
        return ds;
    }

    internal static decimal TinhKhuyenMai(KhuyenMai km, decimal tongTien)
    {
        var giam = km.LoaiKM switch
        {
            LoaiKhuyenMai.PHAN_TRAM => tongTien * (km.PhanTramKM ?? km.GiaTri ?? 0) / 100m,
            LoaiKhuyenMai.SO_TIEN_CO_DINH => km.GiaTri ?? 0,
            _ => 0m
        };
        // [Khối 7b - K7-7] Phòng thủ nhiều lớp: dữ liệu khuyến mãi cũ (tạo trước khi PromotionService chặn giá trị âm)
        // không bao giờ được làm tiền khuyến mãi âm (tức tăng tiền khách trả). Chặn cả sàn 0 lẫn trần tongTien.
        return Math.Round(Math.Clamp(giam, 0m, Math.Max(tongTien, 0m)), 0, MidpointRounding.AwayFromZero);
    }
}
