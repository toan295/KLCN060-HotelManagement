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

    public CheckInService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<CheckInResultDto> CheckInBookingAsync(string maPhieuDat, BookingCheckInRequest request)
    {
        var phieuDat = await _context.PhieuDatPhongs
            .Include(x => x.Khach)
            .Include(x => x.ChiTietPhieuDats)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p!.ChiTietPhieuNhans)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        if (phieuDat.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_XAC_NHAN_COC", "Phiếu đặt phòng chưa xác nhận đặt cọc, không thể nhận phòng.");
        if (phieuDat.TrangThai != TrangThaiPhieuDat.DA_XAC_NHAN)
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Không thể nhận phòng khi phiếu đặt đang ở trạng thái {phieuDat.TrangThai}.");

        // Không nhận phòng trước ngày đón dự kiến: tiền phòng được tính từ ngày nhận thực tế nên nhận sớm sẽ làm sai hóa đơn
        // và chiếm phòng của các đặt phòng khác. Muốn ở sớm hơn, cần đặt lại với ngày đón mới.
        if (DateOnly.FromDateTime(DateTime.Now) < phieuDat.NgayDonDuKien)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_DEN_NGAY_NHAN", $"Chưa đến ngày nhận phòng dự kiến ({phieuDat.NgayDonDuKien:dd/MM/yyyy}).");

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

        var phongs = await _context.Phongs.Include(x => x.LoaiPhong).Where(x => phongNhan.Contains(x.MaPhong)).ToListAsync();
        foreach (var phong in phongs)
            RoomAvailability.EnsureReady(phong);

        CapNhatCccd(phieuDat.Khach, thongTinKhach);

        var bayGio = DateTime.Now;
        var phieuNhan = phieuDat.PhieuNhanPhong;
        if (phieuNhan is null)
        {
            phieuNhan = new PhieuNhanPhong
            {
                MaPhieuNhan = await SinhMaPhieuNhanAsync(bayGio),
                MaPhieuDat = phieuDat.MaPhieuDat,
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
                SoNguoi = soNguoi,
                DonGia = chiTietDat.DonGiaApDung,
                TienPhuThu = 0,
                TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
            });

            phong.TinhTrang = TinhTrangPhong.OC;
            ketQua.Add(new CheckedInRoomDto { MaPhong = phong.MaPhong, SoNguoi = soNguoi, DonGia = chiTietDat.DonGiaApDung, TinhTrangPhong = nameof(TinhTrangPhong.OC) });
        }

        if (phieuDat.ChiTietPhieuDats.All(x => daNhan.Contains(x.MaPhong) || phongNhan.Contains(x.MaPhong)))
            phieuDat.TrangThai = TrangThaiPhieuDat.DA_NHAN_PHONG;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi nhận phòng (có thể phòng vừa được nhận ở nơi khác), vui lòng thử lại.");
        }

        return new CheckInResultDto
        {
            MaPhieuNhan = phieuNhan.MaPhieuNhan,
            MaPhieuDat = phieuDat.MaPhieuDat,
            TrangThaiPhieuDat = phieuDat.TrangThai.ToString(),
            NgayNhan = phieuNhan.NgayNhan,
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

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var ketQua = new List<CheckedInRoomDto>();
        var phieuNhan = new PhieuNhanPhong
        {
            MaPhieuNhan = await SinhMaPhieuNhanAsync(bayGio),
            MaPhieuDat = null,
            NgayNhan = bayGio
        };
        _context.PhieuNhanPhongs.Add(phieuNhan);

        foreach (var maPhong in phongNhan.OrderBy(x => x, StringComparer.Ordinal))
        {
            // Khóa dòng phòng tới hết transaction: 2 lễ tân cùng nhận 1 phòng thì người sau phải chờ rồi thấy phòng đã OC.
            var phong = await _context.Phongs
                .FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}")
                .Include(x => x.LoaiPhong)
                .FirstOrDefaultAsync()
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", $"Không tìm thấy phòng {maPhong}.", "khach");

            RoomAvailability.EnsureReady(phong);

            await RoomAvailability.EnsureFreeTodayAsync(_context, maPhong, homNay, "khach");

            var soNguoi = LaySoNguoi(thongTinKhach, phong);
            _context.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
            {
                MaPhieuNhan = phieuNhan.MaPhieuNhan,
                MaPhong = maPhong,
                NgayNhan = homNay,
                NgayTra = null,
                SoNguoi = soNguoi,
                DonGia = phong.LoaiPhong.DonGia,
                TienPhuThu = 0,
                TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
            });

            phong.TinhTrang = TinhTrangPhong.OC;
            ketQua.Add(new CheckedInRoomDto { MaPhong = maPhong, SoNguoi = soNguoi, DonGia = phong.LoaiPhong.DonGia, TinhTrangPhong = nameof(TinhTrangPhong.OC) });
        }

        CapNhatCccd(khach, thongTinKhach);

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
            if (!ketQua.TryAdd(info.MaPhong, info))
                throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_TRUNG_LAP", $"Phòng {info.MaPhong} khai báo khách nhiều lần.", "khach");
        }
        return ketQua;
    }

    private static int LaySoNguoi(Dictionary<string, CheckInGuestInfo> thongTinKhach, Phong phong)
        => thongTinKhach.TryGetValue(phong.MaPhong, out var info) && info.SoNguoi > 0 ? info.SoNguoi : phong.LoaiPhong.SoNguoiTieuChuan;

    private static void CapNhatCccd(Khach khach, Dictionary<string, CheckInGuestInfo> thongTinKhach)
    {
        if (string.IsNullOrWhiteSpace(khach.CCCD))
        {
            var cccd = thongTinKhach.Values.Select(x => x.CCCD).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (string.IsNullOrWhiteSpace(cccd))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_CCCD", "Khách chưa có CCCD, cần nhập CCCD khi nhận phòng.", "khach");
            khach.CCCD = cccd.Trim();
        }
    }

    private async Task<string> SinhMaPhieuNhanAsync(DateTime bayGio)
    {
        var tienTo = $"CI{bayGio:yyyyMMdd}-";
        var maHienCo = await _context.PhieuNhanPhongs.Where(x => x.MaPhieuNhan.StartsWith(tienTo)).Select(x => x.MaPhieuNhan).ToListAsync();
        return MaCodeGenerator.GenerateNext(maHienCo, tienTo, 4);
    }
}
