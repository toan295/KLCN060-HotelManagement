using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class BookingService : IBookingService
{
    private const int PageSizeToiDa = 100;

    private readonly KLCN060DbContext _context;

    public BookingService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CurrentUser user)
    {
        if (!Enum.TryParse<LoaiDatPhong>(request.LoaiDatPhong, ignoreCase: true, out var loaiDatPhong))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DAT_PHONG_KHONG_HOP_LE", "LoaiDatPhong không hợp lệ. Chỉ chấp nhận: CA_NHAN, DOAN.", "loaiDatPhong");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (request.NgayDonDuKien < homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_DON_KHONG_HOP_LE", "Ngày đón dự kiến không được ở quá khứ.", "ngayDonDuKien");
        if (request.NgayTraDuKien <= request.NgayDonDuKien)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày đón.", "ngayTraDuKien");

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
            .Select(g => new { MaLoai = g.Key, SoLuong = g.Sum(x => x.SoLuong), ViTri = request.PhongCanDat.FindIndex(x => x.MaLoai.Trim() == g.Key) })
            .OrderBy(x => x.MaLoai, StringComparer.Ordinal)
            .ToList();

        var soDem = request.NgayTraDuKien.DayNumber - request.NgayDonDuKien.DayNumber;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var chiTiets = new List<ChiTietPhieuDat>();
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

            foreach (var phong in phongTrong.Take(dong.SoLuong))
            {
                chiTiets.Add(new ChiTietPhieuDat
                {
                    MaPhong = phong.MaPhong,
                    SoLuong = 1,
                    DonGiaApDung = loaiPhong.DonGia,
                    ThanhTien = loaiPhong.DonGia * soDem
                });
            }
        }

        var tongTien = chiTiets.Sum(x => x.ThanhTien);
        var tongKhuyenMai = 0m;
        KhuyenMai? khuyenMai = null;

        if (!string.IsNullOrWhiteSpace(request.MaKhuyenMai))
        {
            khuyenMai = await _context.KhuyenMais.AsNoTracking().FirstOrDefaultAsync(x => x.MaKM == request.MaKhuyenMai);
            if (khuyenMai is null)
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHUYEN_MAI", "Không tìm thấy khuyến mãi.", "maKhuyenMai");
            if (homNay < khuyenMai.NgayBatDau || homNay > khuyenMai.NgayKetThuc)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_HET_HAN", "Khuyến mãi không nằm trong thời gian áp dụng.", "maKhuyenMai");

            tongKhuyenMai = TinhKhuyenMai(khuyenMai, tongTien);

            // Tăng lượt dùng nguyên tử, chỉ khi còn lượt — tránh vượt SoLuongGioiHan khi có request đồng thời.
            var daCapNhat = await _context.KhuyenMais
                .Where(x => x.MaKM == khuyenMai.MaKM && (x.SoLuongGioiHan == null || x.SoLuongDaSuDung < x.SoLuongGioiHan))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung + 1));
            if (daCapNhat == 0)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_HET_LUOT", "Khuyến mãi đã hết lượt sử dụng.", "maKhuyenMai");
        }

        var tienCoc = Math.Round(tongTien * 0.5m, 0, MidpointRounding.AwayFromZero);

        var tienTo = $"BK{request.NgayDonDuKien:yyyyMMdd}-";
        var maHienCo = await _context.PhieuDatPhongs.Where(x => x.MaPhieuDat.StartsWith(tienTo)).Select(x => x.MaPhieuDat).ToListAsync();

        var phieuDat = new PhieuDatPhong
        {
            MaPhieuDat = MaCodeGenerator.GenerateNext(maHienCo, tienTo, 4),
            MaKhach = khach.MaKhach,
            NgayDat = DateTime.Now,
            NgayDonDuKien = request.NgayDonDuKien,
            NgayTraDuKien = request.NgayTraDuKien,
            TienCoc = tienCoc,
            TrangThai = TrangThaiPhieuDat.CHO_XAC_NHAN,
            LoaiDatPhong = loaiDatPhong
        };
        foreach (var ct in chiTiets)
        {
            ct.MaPhieuDat = phieuDat.MaPhieuDat;
            phieuDat.ChiTietPhieuDats.Add(ct);
        }
        if (khuyenMai is not null)
        {
            phieuDat.ChiTietKhuyenMais.Add(new ChiTietKhuyenMai { MaPhieuDat = phieuDat.MaPhieuDat, MaKM = khuyenMai.MaKM, TongKhuyenMai = tongKhuyenMai });
        }

        _context.PhieuDatPhongs.Add(phieuDat);

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
            if (!Enum.TryParse<TrangThaiPhieuDat>(query.TrangThai, ignoreCase: true, out var trangThai))
                throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE", "TrangThai không hợp lệ.", "trangThai");
            q = q.Where(x => x.TrangThai == trangThai);
        }
        if (query.TuNgay is { } tuNgay)
            q = q.Where(x => x.NgayDonDuKien >= tuNgay);
        if (query.DenNgay is { } denNgay)
            q = q.Where(x => x.NgayDonDuKien <= denNgay);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            q = q.Where(x => x.Khach.HoTen.Contains(tuKhoa) || x.Khach.SoDT.Contains(tuKhoa) || x.MaPhieuDat.Contains(tuKhoa));
        }

        var total = await q.CountAsync();
        var items = await ChieuSangTomTat(q.OrderByDescending(x => x.NgayDat).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();

        return (items, total);
    }

    public async Task<List<BookingSummaryDto>> GetMineAsync(string? maKhach)
    {
        if (string.IsNullOrEmpty(maKhach))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Tài khoản không gắn với khách hàng nào.");

        return await ChieuSangTomTat(_context.PhieuDatPhongs.Where(x => x.MaKhach == maKhach).OrderByDescending(x => x.NgayDat)).ToListAsync();
    }

    public async Task<BookingDto> GetByIdAsync(string maPhieuDat, CurrentUser user)
    {
        var dto = await LayChiTietAsync(maPhieuDat);
        KiemTraQuyenSoHuu(dto.MaKhach, user);
        return dto;
    }

    public async Task<BookingDto> ConfirmDepositAsync(string maPhieuDat, CurrentUser user)
    {
        var phieuDat = await _context.PhieuDatPhongs.FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        KiemTraQuyenSoHuu(phieuDat.MaKhach, user);

        if (phieuDat.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Chỉ xác nhận đặt cọc được khi phiếu đang ở trạng thái CHO_XAC_NHAN (hiện tại: {phieuDat.TrangThai}).");

        phieuDat.TrangThai = TrangThaiPhieuDat.DA_XAC_NHAN;
        await _context.SaveChangesAsync();

        return await LayChiTietAsync(maPhieuDat);
    }

    public async Task<CancelBookingResultDto> CancelAsync(string maPhieuDat, CancelBookingRequest request, CurrentUser user)
    {
        if (request.GhiDeChinhSach)
        {
            if (user.LaKhachHang)
                throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_GHI_DE", "Chỉ lễ tân được ghi đè chính sách hoàn cọc.");
            if (string.IsNullOrWhiteSpace(request.LyDo))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do khi ghi đè chính sách hoàn cọc.", "lyDo");
            if (request.HoanCoc is null)
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_HOAN_COC", "Cần chỉ định hoanCoc (true/false) khi ghi đè chính sách.", "hoanCoc");
        }

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
        var hoanCoc = request.GhiDeChinhSach ? request.HoanCoc!.Value : soNgayConLai >= 30;
        var daDatCoc = phieuDat.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN;

        phieuDat.TrangThai = TrangThaiPhieuDat.DA_HUY;

        // Trả lại lượt sử dụng khuyến mãi mà phiếu này đã chiếm.
        foreach (var km in phieuDat.ChiTietKhuyenMais)
        {
            var maKM = km.MaKM;
            await _context.KhuyenMais
                .Where(x => x.MaKM == maKM && x.SoLuongDaSuDung > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung - 1));
        }

        var soTienHoan = hoanCoc && daDatCoc ? phieuDat.TienCoc : 0m;

        if (!string.IsNullOrEmpty(user.TenDN))
        {
            _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaTaiKhoan = user.TenDN,
                HanhDong = "HUY_DAT_PHONG",
                DoiTuongTacDong = maPhieuDat,
                ThoiGian = DateTime.Now,
                ChiTiet = $"hoanCoc={hoanCoc}; soNgayConLai={soNgayConLai}; ghiDeChinhSach={request.GhiDeChinhSach}; lyDo={request.LyDo}"
            });
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CancelBookingResultDto
        {
            MaPhieuDat = maPhieuDat,
            TrangThai = nameof(TrangThaiPhieuDat.DA_HUY),
            HoanCoc = hoanCoc,
            GhiDeChinhSach = request.GhiDeChinhSach,
            SoNgayConLai = soNgayConLai,
            SoTienHoan = soTienHoan
        };
    }

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
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        return new BookingDto
        {
            MaPhieuDat = phieuDat.MaPhieuDat,
            MaKhach = phieuDat.MaKhach,
            HoTenKhach = phieuDat.Khach.HoTen,
            LoaiDatPhong = phieuDat.LoaiDatPhong.ToString(),
            TrangThai = phieuDat.TrangThai.ToString(),
            NgayDat = phieuDat.NgayDat,
            NgayDonDuKien = phieuDat.NgayDonDuKien,
            NgayTraDuKien = phieuDat.NgayTraDuKien,
            TienCoc = phieuDat.TienCoc,
            TongTienDuKien = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien),
            TongKhuyenMai = phieuDat.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai),
            PhongDaGiu = phieuDat.ChiTietPhieuDats
                .OrderBy(x => x.MaPhong, StringComparer.Ordinal)
                .Select(x => new HeldRoomDto { MaPhong = x.MaPhong, MaLoai = x.Phong.MaLoai, DonGiaApDung = x.DonGiaApDung, ThanhTien = x.ThanhTien })
                .ToList()
        };
    }

    private static IQueryable<BookingSummaryDto> ChieuSangTomTat(IQueryable<PhieuDatPhong> q) => q.Select(x => new BookingSummaryDto
    {
        MaPhieuDat = x.MaPhieuDat,
        MaKhach = x.MaKhach,
        HoTenKhach = x.Khach.HoTen,
        SoDTKhach = x.Khach.SoDT,
        LoaiDatPhong = x.LoaiDatPhong.ToString(),
        TrangThai = x.TrangThai.ToString(),
        NgayDat = x.NgayDat,
        NgayDonDuKien = x.NgayDonDuKien,
        NgayTraDuKien = x.NgayTraDuKien,
        SoPhong = x.ChiTietPhieuDats.Count,
        TienCoc = x.TienCoc,
        TongTienDuKien = x.ChiTietPhieuDats.Sum(ct => (decimal?)ct.ThanhTien) ?? 0
    });

    private static decimal TinhKhuyenMai(KhuyenMai km, decimal tongTien)
    {
        var giam = km.LoaiKM switch
        {
            LoaiKhuyenMai.PHAN_TRAM => tongTien * (km.PhanTramKM ?? km.GiaTri ?? 0) / 100m,
            LoaiKhuyenMai.SO_TIEN_CO_DINH => km.GiaTri ?? 0,
            _ => 0m
        };
        return Math.Round(Math.Min(giam, tongTien), 0, MidpointRounding.AwayFromZero);
    }
}
