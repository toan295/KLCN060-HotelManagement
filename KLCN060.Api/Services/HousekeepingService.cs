using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

/// <summary>Tra họ tên nhân viên theo tên đăng nhập (một truy vấn cho cả danh sách) - dùng chung cho các màn hình buồng phòng.</summary>
public static class TenNhanVien
{
    public static async Task<Dictionary<string, string?>> TraCuuAsync(KLCN060DbContext context, IEnumerable<string?> tenDNs)
    {
        var ds = tenDNs.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        if (ds.Count == 0) return new Dictionary<string, string?>();
        return await context.TaiKhoans.AsNoTracking().Where(x => ds.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : null })
            .ToDictionaryAsync(x => x.TenDN, x => x.HoTen);
    }
}

/// <summary>Tên người thực hiện để hiển thị cho người xem: "Họ tên (Mã NV)" hoặc "Họ tên (Mã KH)" - không lộ tên đăng nhập.</summary>
public static class TenNguoiDung
{
    public static async Task<Dictionary<string, string>> HienThiAsync(KLCN060DbContext context, IEnumerable<string?> tenDNs)
    {
        var ds = tenDNs.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        if (ds.Count == 0) return new Dictionary<string, string>();
        var rows = await context.TaiKhoans.AsNoTracking().Where(x => ds.Contains(x.TenDN))
            .Select(x => new
            {
                x.TenDN,
                HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null,
                Ma = x.NhanVien != null ? x.NhanVien.MaNV : x.Khach != null ? x.Khach.MaKhach : null
            }).ToListAsync();
        return rows.ToDictionary(x => x.TenDN, x => string.IsNullOrEmpty(x.HoTen) ? x.TenDN : string.IsNullOrEmpty(x.Ma) ? x.HoTen : $"{x.HoTen} ({x.Ma})");
    }

    public static async Task<string?> HienThiAsync(KLCN060DbContext context, string? tenDN)
        => string.IsNullOrEmpty(tenDN) ? tenDN : (await HienThiAsync(context, new[] { tenDN })).GetValueOrDefault(tenDN, tenDN);
}

public interface IHousekeepingService
{
    Task<HousekeepingBoardDto> GetBoardAsync(string tenDN);
    Task<HousekeepingTaskDto> ClaimAsync(string maPhong, string tenDN);
    Task ReleaseAsync(string maPhong, string tenDN);
    Task<CleaningReportDto> GetCleaningReportAsync(DateOnly? tuNgay, DateOnly? denNgay);
    Task<HousekeepingOverviewDto> GetOverviewAsync(string tenDN);
    Task<HousekeepingTaskDto> AssignAsync(string maPhong, string nguoiDon, string tenQuanLy);
    Task<List<HousekeepingActivityDto>> GetActivityAsync(string? maPhong, DateOnly? tuNgay, DateOnly? denNgay, int toiDa);
}

/// <summary>
/// Việc dọn phòng của buồng phòng: phòng bẩn (VD/OD) tự là việc cần làm, xếp theo mức ưu tiên (phòng giữ cho khách đến hôm nay trước, rồi phòng chờ lâu);
/// nhân viên "nhận dọn" để đồng nghiệp biết ai đang dọn phòng nào, và khi phòng được xác nhận sạch (RoomService) việc tự đóng lại kèm ai dọn, mất bao lâu.
/// </summary>
public partial class HousekeepingService : IHousekeepingService
{
    private const int SoNgayBaoCaoMacDinh = 7;
    private const int SoNgayBaoCaoToiDa = 92;

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;
    private readonly AlertOptions _alert;

    public HousekeepingService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions, IOptions<AlertOptions> alert)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
        _alert = alert.Value;
    }

    public async Task<HousekeepingBoardDto> GetBoardAsync(string tenDN)
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var dauNgay = bayGio.Date;

        var phongBan = await _context.Phongs.AsNoTracking()
            .Where(x => x.TinhTrang == TinhTrangPhong.VD || x.TinhTrang == TinhTrangPhong.OD)
            .Select(x => new
            {
                x.MaPhong, x.TenPhong, x.Tang, TenLoai = x.LoaiPhong.TenLoai, x.TinhTrang,
                TuLuc = _context.LichSuTinhTrangPhongs.Where(h => h.MaPhong == x.MaPhong && h.DenTinhTrang == x.TinhTrang)
                    .OrderByDescending(h => h.ThoiGian).ThenByDescending(h => h.MaLichSu).Select(h => (DateTime?)h.ThoiGian).FirstOrDefault()
            }).ToListAsync();
        var maPhongs = phongBan.Select(x => x.MaPhong).ToList();

        // Phòng đang giữ cho khách đến hôm nay (hoặc quá ngày đón mà chưa nhận): cần dọn gấp.
        var giu = (await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => maPhongs.Contains(ct.MaPhong)
                         && (ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                         && ct.PhieuDatPhong.NgayDonDuKien <= homNay && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .Select(ct => ct.MaPhong).Distinct().ToListAsync()).ToHashSet();

        var dangDon = await _context.PhieuDonPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDon.DANG_DON).Select(x => new { x.MaPhong, x.NguoiDon, x.ThoiGianBatDau, x.NguoiPhanCong, x.ThoiGianPhanCong }).ToListAsync();
        var suCoMo = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => x.TrangThai != TrangThaiSuCo.DA_XU_LY && x.TrangThai != TrangThaiSuCo.DA_HUY)
            .GroupBy(x => x.MaPhong).Select(g => new { MaPhong = g.Key, So = g.Count() }).ToDictionaryAsync(x => x.MaPhong, x => x.So);
        var ten = await TenNhanVien.TraCuuAsync(_context, dangDon.Select(x => x.NguoiDon));

        var viec = phongBan.Select(x =>
        {
            var don = dangDon.FirstOrDefault(d => d.MaPhong == x.MaPhong);
            var soGio = x.TuLuc is null ? (double?)null : Math.Round((bayGio - x.TuLuc.Value).TotalHours, 1);
            var khan = x.TinhTrang == TinhTrangPhong.VD && giu.Contains(x.MaPhong);
            var cao = !khan && soGio >= _alert.DirtyRoomHours;
            return new HousekeepingTaskDto
            {
                MaPhong = x.MaPhong, TenPhong = x.TenPhong, Tang = x.Tang, TenLoaiPhong = x.TenLoai, TinhTrang = x.TinhTrang.ToString(),
                TuLuc = x.TuLuc, SoGioCho = soGio,
                MucUuTien = khan ? "KHAN" : cao ? "CAO" : "BINH_THUONG",
                LyDoUuTien = khan ? "Phòng đang giữ cho khách đến hôm nay" : cao ? $"Chờ dọn quá {_alert.DirtyRoomHours} giờ" : null,
                KhachDenHomNay = giu.Contains(x.MaPhong),
                NguoiDon = don?.NguoiDon,
                HoTenNguoiDon = don is null ? null : ten.GetValueOrDefault(don.NguoiDon),
                BatDauDonLuc = don?.ThoiGianBatDau,
                NguoiPhanCong = don?.NguoiPhanCong,
                GiaoLuc = don?.ThoiGianPhanCong,
                SuCoDangMo = suCoMo.GetValueOrDefault(x.MaPhong)
            };
        })
        .OrderByDescending(x => x.MucUuTien == "KHAN").ThenByDescending(x => x.MucUuTien == "CAO")
        .ThenByDescending(x => x.SoGioCho ?? 0).ThenBy(x => x.MaPhong, StringComparer.Ordinal)
        .ToList();

        var xongHomNay = await _context.PhieuDonPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDon.DA_XONG && x.ThoiGianXong >= dauNgay).Select(x => x.NguoiDon).ToListAsync();

        var theoTinhTrang = await _context.Phongs.AsNoTracking().GroupBy(x => x.TinhTrang).Select(g => new { g.Key, So = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.So);
        int So(TinhTrangPhong t) => theoTinhTrang.GetValueOrDefault(t);

        return new HousekeepingBoardDto
        {
            CapNhatLuc = bayGio,
            TongPhong = theoTinhTrang.Values.Sum(),
            PhongSanSang = So(TinhTrangPhong.VC) + So(TinhTrangPhong.VI),
            PhongDaKiemTra = So(TinhTrangPhong.VI),
            PhongCoKhach = So(TinhTrangPhong.OC),
            PhongBaoTri = So(TinhTrangPhong.OOO),
            TongCanDon = viec.Count,
            PhongTrongCanDon = viec.Count(x => x.TinhTrang == nameof(TinhTrangPhong.VD)),
            PhongCoKhachCanDon = viec.Count(x => x.TinhTrang == nameof(TinhTrangPhong.OD)),
            DangDon = viec.Count(x => x.NguoiDon is not null),
            PhongKhan = viec.Count(x => x.MucUuTien == "KHAN"),
            DaDonHomNay = xongHomNay.Count,
            DaDonHomNayCuaToi = xongHomNay.Count(x => x == tenDN),
            Viec = viec
        };
    }

    public async Task<HousekeepingTaskDto> ClaimAsync(string maPhong, string tenDN)
    {
        // Quản lý điều hành: giao việc và kiểm tra chất lượng, không tự nhận dọn phòng (người dọn và người kiểm tra phải là hai người khác nhau).
        if (await _permissions.CoQuyenAsync(tenDN, Permissions.DuyetNghiepVu))
            throw new ApiException(StatusCodes.Status403Forbidden, "QUAN_LY_KHONG_TU_DON_PHONG",
                "Quản lý không tự nhận dọn phòng: hãy giao việc cho nhân viên buồng phòng rồi kiểm tra kết quả.");
        var phong = await _context.Phongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        if (phong.TinhTrang is not (TinhTrangPhong.VD or TinhTrangPhong.OD))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_KHONG_CAN_DON",
                $"Phòng {maPhong} đang ở tình trạng {phong.TinhTrang}, không có việc dọn để nhận (chỉ nhận được phòng VD hoặc OD).");

        var dangMo = await _context.PhieuDonPhongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiPhieuDon.DANG_DON);
        if (dangMo is not null)
        {
            if (dangMo.NguoiDon == tenDN)
            {
                // Việc Quản lý đã giao cho chính mình: bấm "nhận dọn" = bắt đầu dọn (ghi giờ bắt đầu). Việc đã bắt đầu thì bấm lại không lỗi.
                if (dangMo.ThoiGianBatDau is null)
                {
                    dangMo.ThoiGianBatDau = DateTime.Now;
                    _audit.Ghi("NHAN_DON_PHONG", maPhong, $"tinhTrang={phong.TinhTrang}; batDauViecDuocGiao=true; nguoiGiao={dangMo.NguoiPhanCong}");
                    await _context.SaveChangesAsync();
                }
                return await LayViecAsync(maPhong, tenDN);
            }
            var ten = (await TenNhanVien.TraCuuAsync(_context, new[] { dangMo.NguoiDon })).GetValueOrDefault(dangMo.NguoiDon) ?? dangMo.NguoiDon;
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_DON",
                dangMo.ThoiGianBatDau is null ? $"Phòng {maPhong} đã được giao cho {ten}." : $"Phòng {maPhong} đang được {ten} dọn.");
        }

        _context.PhieuDonPhongs.Add(new PhieuDonPhong
        {
            MaPhong = maPhong, NguoiDon = tenDN, TinhTrangBanDau = phong.TinhTrang, ThoiGianBatDau = DateTime.Now, TrangThai = TrangThaiPhieuDon.DANG_DON
        });
        _audit.Ghi("NHAN_DON_PHONG", maPhong, $"tinhTrang={phong.TinhTrang}");
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hai người cùng nhận một lúc: chỉ số duy nhất trên phiếu đang mở chặn người đến sau.
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_DON", $"Phòng {maPhong} vừa được người khác nhận dọn.");
        }
        return await LayViecAsync(maPhong, tenDN);
    }

    public async Task ReleaseAsync(string maPhong, string tenDN)
    {
        var dangMo = await _context.PhieuDonPhongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiPhieuDon.DANG_DON)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_CO_VIEC_DON", $"Phòng {maPhong} chưa có ai nhận dọn.");
        if (dangMo.NguoiDon != tenDN && !await _permissions.CoQuyenAsync(tenDN, Permissions.DuyetNghiepVu))
            throw new ApiException(StatusCodes.Status403Forbidden, "CHI_NGUOI_NHAN_MOI_DUOC_NHA", "Chỉ người đã nhận dọn phòng này (hoặc quản lý) mới bỏ nhận được.");

        dangMo.TrangThai = TrangThaiPhieuDon.DA_HUY;
        dangMo.ThoiGianXong = DateTime.Now;
        _audit.Ghi("BO_NHAN_DON_PHONG", maPhong, $"nguoiDon={dangMo.NguoiDon}");
        await _context.SaveChangesAsync();
    }

    public async Task<CleaningReportDto> GetCleaningReportAsync(DateOnly? tuNgay, DateOnly? denNgay)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var den = denNgay ?? homNay;
        var tu = tuNgay ?? den.AddDays(-(SoNgayBaoCaoMacDinh - 1));
        if (tu > den)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải trước hoặc bằng đến ngày.", "tuNgay");
        if (den.DayNumber - tu.DayNumber + 1 > SoNgayBaoCaoToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Chỉ xem tối đa {SoNgayBaoCaoToiDa} ngày một lần.", "tuNgay");

        var tuLuc = tu.ToDateTime(TimeOnly.MinValue);
        var denLuc = den.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var dong = await _context.PhieuDonPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDon.DA_XONG && x.ThoiGianXong >= tuLuc && x.ThoiGianXong < denLuc)
            .Select(x => new { x.NguoiDon, x.ThoiGianBatDau, x.ThoiGianXong }).ToListAsync();
        var ten = await TenNhanVien.TraCuuAsync(_context, dong.Select(x => x.NguoiDon));

        double? TrungBinh(IEnumerable<double> xs) { var l = xs.ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 1); }
        double Phut(DateTime? bat, DateTime? xong) => (xong!.Value - bat!.Value).TotalMinutes;

        return new CleaningReportDto
        {
            TuNgay = tu, DenNgay = den,
            TongPhongDaDon = dong.Count,
            ThoiGianTrungBinhPhut = TrungBinh(dong.Where(x => x.ThoiGianBatDau != null).Select(x => Phut(x.ThoiGianBatDau, x.ThoiGianXong))),
            Dong = dong.GroupBy(x => x.NguoiDon).Select(g => new CleaningReportRowDto
            {
                NguoiDon = g.Key,
                HoTen = ten.GetValueOrDefault(g.Key),
                SoPhongDaDon = g.Count(),
                SoPhongCoNhanViec = g.Count(x => x.ThoiGianBatDau != null),
                ThoiGianTrungBinhPhut = TrungBinh(g.Where(x => x.ThoiGianBatDau != null).Select(x => Phut(x.ThoiGianBatDau, x.ThoiGianXong)))
            }).OrderByDescending(x => x.SoPhongDaDon).ThenBy(x => x.NguoiDon, StringComparer.Ordinal).ToList()
        };
    }

    private async Task<HousekeepingTaskDto> LayViecAsync(string maPhong, string tenDN)
    {
        var bang = await GetBoardAsync(tenDN);
        return bang.Viec.First(x => x.MaPhong == maPhong);
    }
}
