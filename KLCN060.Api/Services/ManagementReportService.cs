using System.Globalization;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Services.Finance;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IManagementReportService
{
    Task<OverviewReportDto> GetOverviewAsync(DateOnly from, DateOnly to, string? nhomTheo);
    Task<RevenueBreakdownDto> GetRevenueBreakdownAsync(DateOnly from, DateOnly to);
    Task<BookingReportDto> GetBookingReportAsync(DateOnly from, DateOnly to);
    Task<DebtReportDto> GetDebtsAsync();
}

/// <summary>
/// Báo cáo cho Quản lý: KPI theo kỳ (Occupancy / ADR / RevPAR + so kỳ trước), doanh thu theo nhiều chiều,
/// đặt phòng, công nợ. Chỉ đọc dữ liệu (không ghi gì).
/// Quy ước: "đêm phòng bán" tính theo đêm ở thực tế của lượt lưu trú (giống báo cáo công suất); doanh thu phòng = đơn giá áp dụng x đêm ở;
/// "doanh thu" theo hóa đơn giữ nguyên định nghĩa của báo cáo doanh thu cũ (TongTien + TienDaCoc của hóa đơn đã thu được một phần, theo ngày lập).
/// </summary>
public partial class ManagementReportService : IManagementReportService
{
    private const int SoNgayToiDa = 366;
    private const int SoNgayNhomThang = 93;
    private const int TuoiNoNhomDau = 7;

    private readonly KLCN060DbContext _context;
    private readonly FinanceOptions _finance;

    /// <summary>Số ngày sau khi khách trả phòng mà hóa đơn còn nợ thì bị coi là quá hạn (cùng cấu hình với tab Hóa đơn và cảnh báo Điều hành).</summary>
    private int NgayQuaHan => Math.Max(_finance.HoaDonQuaHanNgay, 0);

    /// <summary>Ngày thêm vào danh mục của những phòng có ghi nhận (phòng cũ không có ngày được coi là có từ trước mọi kỳ báo cáo).</summary>
    private List<DateOnly> _ngayTaoPhong = new();

    /// <summary>Các dòng phòng được NHẬN thật (loại dòng mở ra do đổi phòng giữa chừng - khách không check-in thêm).</summary>
    private IQueryable<ChiTietPhieuNhan> LuotNhanThat()
        => _context.ChiTietPhieuNhans.Where(c => !_context.PhieuDoiPhongs.Any(d => d.MaPhieuNhan == c.MaPhieuNhan && d.MaPhongMoi == c.MaPhong));

    private static List<DateOnly> DanhSachNgayTao(IEnumerable<DateTime?> ngay)
        => ngay.Where(n => n.HasValue).Select(n => DateOnly.FromDateTime(n!.Value)).ToList();

    /// <summary>Tổng số phòng hiện có; đồng thời nạp ngày tạo phòng để tính số đêm khả dụng theo từng ngày.</summary>
    private async Task<int> TongPhongAsync()
    {
        var ngay = await _context.Phongs.AsNoTracking().Select(p => p.NgayTao).ToListAsync();
        _ngayTaoPhong = DanhSachNgayTao(ngay);
        return ngay.Count;
    }

    /// <summary>
    /// Số đêm phòng khả dụng của [tu, tu+soNgay): mỗi ngày chỉ tính các phòng ĐÃ có trong danh mục từ ngày đó - thêm phòng hôm nay
    /// không làm đổi công suất/RevPAR của các ngày trước (trước đây mẫu số luôn là tổng số phòng hiện tại).
    /// </summary>
    private static int KhaDung(int tongPhong, List<DateOnly> ngayTaoPhong, DateOnly tu, int soNgay)
    {
        var khaDung = tongPhong * soNgay;
        foreach (var ngayTao in ngayTaoPhong)
            khaDung -= Math.Clamp(ngayTao.DayNumber - tu.DayNumber, 0, soNgay);
        return khaDung;
    }

    public ManagementReportService(KLCN060DbContext context, Microsoft.Extensions.Options.IOptions<FinanceOptions>? finance = null)
    {
        _context = context;
        _finance = finance?.Value ?? new FinanceOptions();
    }

    private static DateOnly HomNay => DateOnly.FromDateTime(DateTime.Now);
    private static DateTime DauNgay(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);
    private static decimal PhanTram(decimal tu, decimal mau) => mau == 0 ? 0 : Math.Round(tu * 100m / mau, 2);
    private static decimal? ThayDoi(decimal nay, decimal truoc) => truoc == 0 ? null : Math.Round((nay - truoc) * 100m / truoc, 2);

    private static void KiemTraKhoang(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "to phải sau hoặc bằng from.", "to");
        if (to.DayNumber - from.DayNumber + 1 > SoNgayToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Khoảng báo cáo tối đa {SoNgayToiDa} ngày.", "to");
    }

    // =====================================================================================================
    // Dữ liệu nền: đêm phòng đã bán, hóa đơn
    // =====================================================================================================

    private sealed record DemPhong(DateOnly Ngay, string MaPhong, string MaLoai, decimal DonGia, string? MaPhieuDat, NguonDatPhong? Nguon, string? MaPhieuNhan = null);

    /// <summary>
    /// Mỗi phần tử = một phòng có khách ở qua một đêm trong [from, to]. Một lượt ở chiếm các đêm [NgayNhan, NgayTra); lượt đang ở chiếm tới hết hôm nay;
    /// nhận-trả trong cùng ngày tính 1 đêm (trừ dòng phòng đã bị đổi đi ngay trong ngày) - cùng quy ước với báo cáo công suất và cách tính tiền phòng.
    /// </summary>
    private async Task<List<DemPhong>> LayDemPhongAsync(DateOnly from, DateOnly to)
    {
        var homNay = HomNay;
        var luot = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.NgayNhan <= to && (x.NgayTra == null || x.NgayTra >= from))
            .Select(x => new
            {
                x.MaPhieuNhan, x.MaPhong, x.NgayNhan, x.NgayTra, x.DonGia,
                MaLoai = x.Phong.MaLoai,
                MaPhieuDat = x.PhieuNhanPhong.MaPhieuDat,
                Nguon = x.PhieuNhanPhong.PhieuDatPhong == null ? (NguonDatPhong?)null : x.PhieuNhanPhong.PhieuDatPhong.NguonDat
            })
            .ToListAsync();
        // Giá chốt từng đêm của phiếu đặt (nếu có): doanh thu phòng cộng đúng giá của từng đêm, không dùng đơn giá bình quân.
        var giaChot = (await _context.GiaDemDatPhongs.AsNoTracking().Where(x => x.Dem >= from && x.Dem <= to).Select(x => new { x.MaPhieuDat, x.MaPhong, x.Dem, x.Gia }).ToListAsync())
            .ToDictionary(x => (x.MaPhieuDat, x.MaPhong, x.Dem), x => x.Gia);
        var daDoiDi = (await _context.PhieuDoiPhongs.AsNoTracking().Select(x => new { x.MaPhieuNhan, x.MaPhongCu }).ToListAsync())
            .Select(x => (x.MaPhieuNhan, x.MaPhongCu)).ToHashSet();

        var ketQua = new List<DemPhong>();
        foreach (var l in luot)
        {
            var ketThuc = l.NgayTra ?? homNay.AddDays(1);
            if (l.NgayTra is not null && ketThuc <= l.NgayNhan && !daDoiDi.Contains((l.MaPhieuNhan, l.MaPhong)))
                ketThuc = l.NgayNhan.AddDays(1);

            var batDau = l.NgayNhan > from ? l.NgayNhan : from;
            var cuoi = ketThuc.AddDays(-1) < to ? ketThuc.AddDays(-1) : to;
            for (var d = batDau; d <= cuoi; d = d.AddDays(1))
                ketQua.Add(new DemPhong(d, l.MaPhong, l.MaLoai,
                    l.MaPhieuDat is not null && giaChot.TryGetValue((l.MaPhieuDat, l.MaPhong, d), out var gia) ? gia : l.DonGia, l.MaPhieuDat, l.Nguon, l.MaPhieuNhan));
        }

        // Một phòng chỉ bán được một đêm cho mỗi ngày: các lượt ở trong cùng ngày của một phòng (VD khách trả rồi phòng lại được nhận ngay trong ngày
        // theo quy tắc "nhận-trả cùng ngày tính 1 đêm") chỉ tính một đêm - đúng cách báo cáo công suất cũ đếm "số phòng có khách" mỗi ngày.
        return ketQua.GroupBy(x => (x.Ngay, x.MaPhong)).Select(g => g.OrderByDescending(x => x.DonGia).First()).ToList();
    }

    private sealed record HoaDonRut(DateOnly Ngay, decimal DoanhThu, decimal TienDV);

    /// <summary>Hóa đơn lập trong kỳ và đã thu được ít nhất một phần; doanh thu = TongTien (phần thu tại quầy) + TienDaCoc.</summary>
    private async Task<List<HoaDonRut>> LayHoaDonAsync(DateOnly from, DateOnly to)
    {
        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var ds = await _context.HoaDons.AsNoTracking()
            .Where(x => x.NgayLap >= tuLuc && x.NgayLap < denLuc && x.TrangThaiThanhToan != TrangThaiThanhToan.CHUA_THANH_TOAN)
            .Select(x => new { x.NgayLap, x.TongTien, x.TienDaCoc, x.TienDV })
            .ToListAsync();
        return ds.Select(x => new HoaDonRut(DateOnly.FromDateTime(x.NgayLap), x.TongTien + x.TienDaCoc, x.TienDV)).ToList();
    }

    // =====================================================================================================
    // KPI theo kỳ
    // =====================================================================================================

    private sealed record DuLieuKy(DateOnly Tu, DateOnly Den, int TongPhong, List<DateOnly> NgayTaoPhong, List<DemPhong> Dem, List<HoaDonRut> HoaDons, List<DateTime> NgayDatPhieu);

    private async Task<DuLieuKy> TaiDuLieuKyAsync(DateOnly from, DateOnly to, int tongPhong)
    {
        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var ngayDat = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.NgayDat >= tuLuc && x.NgayDat < denLuc).Select(x => x.NgayDat).ToListAsync();
        return new DuLieuKy(from, to, tongPhong, _ngayTaoPhong, await LayDemPhongAsync(from, to), await LayHoaDonAsync(from, to), ngayDat);
    }

    private async Task<PeriodKpiDto> TinhKyAsync(DateOnly from, DateOnly to, int tongPhong)
    {
        var du = await TaiDuLieuKyAsync(from, to, tongPhong);
        var soNgay = to.DayNumber - from.DayNumber + 1;
        var kpi = TinhKpi(du.Dem, KhaDung(tongPhong, _ngayTaoPhong, from, soNgay));
        kpi.TuNgay = from;
        kpi.DenNgay = to;
        kpi.SoNgay = soNgay;
        kpi.TongSoPhong = tongPhong;
        kpi.DoanhThu = du.HoaDons.Sum(x => x.DoanhThu);
        kpi.DoanhThuDichVu = du.HoaDons.Sum(x => x.TienDV);
        kpi.SoDatPhongMoi = du.NgayDatPhieu.Count;

        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        kpi.SoHuy = await _context.PhieuDatPhongs.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY && x.NgayHuy >= tuLuc && x.NgayHuy < denLuc);
        kpi.SoKhongDen = await _context.PhieuDatPhongs.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.KHONG_DEN && x.NgayHuy >= tuLuc && x.NgayHuy < denLuc);
        // Tỷ lệ theo nhóm phiếu tạo trong kỳ: bao nhiêu % trong số đó hiện đã hủy / không đến.
        var nhom = _context.PhieuDatPhongs.Where(x => x.NgayDat >= tuLuc && x.NgayDat < denLuc);
        var tongNhom = kpi.SoDatPhongMoi;
        kpi.TyLeHuyPhanTram = PhanTram(await nhom.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY), tongNhom);
        kpi.TyLeKhongDenPhanTram = PhanTram(await nhom.CountAsync(x => x.TrangThai == TrangThaiPhieuDat.KHONG_DEN), tongNhom);
        // Check-in và Check-out cùng đơn vị: số PHÒNG (đoàn 3 phòng nhận rồi trả đủ → 3 và 3), không tính dòng mở do đổi phòng.
        kpi.SoCheckIn = await LuotNhanThat().CountAsync(x => x.NgayNhan >= from && x.NgayNhan <= to);
        kpi.SoCheckOut = await _context.ChiTietPhieuNhans.CountAsync(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DA_TRA_PHONG && x.NgayTra >= from && x.NgayTra <= to);
        return kpi;
    }

    private static PeriodKpiDto TinhKpi(List<DemPhong> dem, int khaDung)
    {
        var doanhThuPhong = dem.Sum(x => x.DonGia);
        return new PeriodKpiDto
        {
            DoanhThuPhong = doanhThuPhong,
            SoDemPhongBan = dem.Count,
            SoDemPhongKhaDung = khaDung,
            CongSuatPhanTram = PhanTram(dem.Count, khaDung),
            Adr = dem.Count == 0 ? 0 : Math.Round(doanhThuPhong / dem.Count, 0, MidpointRounding.AwayFromZero),
            RevPar = khaDung == 0 ? 0 : Math.Round(doanhThuPhong / khaDung, 0, MidpointRounding.AwayFromZero)
        };
    }

    public async Task<OverviewReportDto> GetOverviewAsync(DateOnly from, DateOnly to, string? nhomTheo)
    {
        KiemTraKhoang(from, to);
        var soNgay = to.DayNumber - from.DayNumber + 1;
        var nhom = string.IsNullOrWhiteSpace(nhomTheo) ? (soNgay > SoNgayNhomThang ? "THANG" : "NGAY") : nhomTheo.Trim().ToUpperInvariant();
        if (nhom is not ("NGAY" or "THANG"))
            throw new ApiException(StatusCodes.Status400BadRequest, "NHOM_THEO_KHONG_HOP_LE", "NhomTheo chỉ chấp nhận: NGAY, THANG.", "nhomTheo");

        // Kỳ trước = khoảng liền trước, dài bằng kỳ này (Hôm nay ↔ hôm qua, Tuần ↔ tuần trước, Tháng ↔ 30 ngày trước...).
        var truocDen = from.AddDays(-1);
        var truocTu = truocDen.AddDays(-(soNgay - 1));
        var tongPhong = await TongPhongAsync();

        var kyNay = await TinhKyAsync(from, to, tongPhong);
        var kyTruoc = await TinhKyAsync(truocTu, truocDen, tongPhong);
        var duNay = await TaiDuLieuKyAsync(from, to, tongPhong);
        var duTruoc = await TaiDuLieuKyAsync(truocTu, truocDen, tongPhong);

        return new OverviewReportDto
        {
            CapNhatLuc = DateTime.Now,
            KyNay = kyNay,
            KyTruoc = kyTruoc,
            NhomTheo = nhom,
            ThayDoi = new PeriodDeltaDto
            {
                DoanhThu = ThayDoi(kyNay.DoanhThu, kyTruoc.DoanhThu),
                DoanhThuPhong = ThayDoi(kyNay.DoanhThuPhong, kyTruoc.DoanhThuPhong),
                CongSuat = ThayDoi(kyNay.CongSuatPhanTram, kyTruoc.CongSuatPhanTram),
                Adr = ThayDoi(kyNay.Adr, kyTruoc.Adr),
                RevPar = ThayDoi(kyNay.RevPar, kyTruoc.RevPar),
                SoDatPhongMoi = ThayDoi(kyNay.SoDatPhongMoi, kyTruoc.SoDatPhongMoi)
            },
            ChuoiKyNay = TinhChuoi(duNay, nhom),
            ChuoiKyTruoc = TinhChuoi(duTruoc, nhom)
        };
    }

    private static List<SeriesPointDto> TinhChuoi(DuLieuKy du, string nhom)
    {
        var diem = new List<SeriesPointDto>();
        if (nhom == "NGAY")
        {
            for (var d = du.Tu; d <= du.Den; d = d.AddDays(1))
                diem.Add(TinhDiem(du, d, d, d.ToString("dd/MM")));
        }
        else
        {
            for (var thang = new DateOnly(du.Tu.Year, du.Tu.Month, 1); thang <= du.Den; thang = thang.AddMonths(1))
            {
                var tu = thang < du.Tu ? du.Tu : thang;
                var cuoiThang = thang.AddMonths(1).AddDays(-1);
                var den = cuoiThang > du.Den ? du.Den : cuoiThang;
                diem.Add(TinhDiem(du, tu, den, thang.ToString("MM/yyyy")));
            }
        }
        return diem;
    }

    private static SeriesPointDto TinhDiem(DuLieuKy du, DateOnly tu, DateOnly den, string nhan)
    {
        var dem = du.Dem.Where(x => x.Ngay >= tu && x.Ngay <= den).ToList();
        var kpi = TinhKpi(dem, KhaDung(du.TongPhong, du.NgayTaoPhong, tu, den.DayNumber - tu.DayNumber + 1));
        var tuLuc = DauNgay(tu);
        var denLuc = DauNgay(den.AddDays(1));
        return new SeriesPointDto
        {
            Ngay = tu,
            Nhan = nhan,
            DoanhThu = du.HoaDons.Where(x => x.Ngay >= tu && x.Ngay <= den).Sum(x => x.DoanhThu),
            DoanhThuPhong = kpi.DoanhThuPhong,
            CongSuatPhanTram = kpi.CongSuatPhanTram,
            Adr = kpi.Adr,
            RevPar = kpi.RevPar,
            SoDatPhongMoi = du.NgayDatPhieu.Count(x => x >= tuLuc && x < denLuc)
        };
    }

    // =====================================================================================================
    // Bảng điều hành
    // =====================================================================================================

    private sealed record NoRut(string MaHD, string? HoTen, string? MaPhieuDat, string? MaKhach, DateTime NgayLap, decimal TongTien, decimal DaThu, decimal ConNo, bool DangO, DateOnly? NgayTraCuoi);

    private (bool QuaHan, int SoNgay) QuaHan(NoRut x) => CongNoQuaHan.Tinh(x.TongTien, x.DaThu, x.DangO, x.NgayTraCuoi, HomNay, NgayQuaHan);

    private async Task<List<NoRut>> LayHoaDonConNoAsync()
    {
        var ds = await _context.HoaDons.AsNoTracking()
            .Select(x => new
            {
                x.MaHD, x.NgayLap, x.TongTien, x.PhieuNhanPhong.MaPhieuDat,
                MaKhach = x.PhieuNhanPhong.MaKhach ?? (x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.MaKhach : null),
                DaThu = x.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0,
                DangO = x.PhieuNhanPhong.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O),
                NgayTraCuoi = x.PhieuNhanPhong.ChiTietPhieuNhans.Max(c => (DateOnly?)c.NgayTra),
                HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen
                    : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : null
            })
            .ToListAsync();
        return ds.Where(x => x.TongTien - x.DaThu > 0)
            .Select(x => new NoRut(x.MaHD, x.HoTen, x.MaPhieuDat, x.MaKhach, x.NgayLap, x.TongTien, x.DaThu, x.TongTien - x.DaThu, x.DangO, x.NgayTraCuoi)).ToList();
    }

    // =====================================================================================================
    // Doanh thu chi tiết
    // =====================================================================================================

    public async Task<RevenueBreakdownDto> GetRevenueBreakdownAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var hoaDons = await LayHoaDonAsync(from, to);
        var dem = await LayDemPhongAsync(from, to);

        var dto = new RevenueBreakdownDto { TuNgay = from, DenNgay = to, TongDoanhThu = hoaDons.Sum(x => x.DoanhThu) };

        var theoNgay = hoaDons.GroupBy(x => x.Ngay).ToDictionary(g => g.Key, g => g.Sum(x => x.DoanhThu));
        for (var d = from; d <= to; d = d.AddDays(1))
            dto.TheoNgay.Add(new RevenueDayDto { Ngay = d, DoanhThu = theoNgay.GetValueOrDefault(d) });

        dto.TheoThang = Chia(hoaDons.GroupBy(x => new DateOnly(x.Ngay.Year, x.Ngay.Month, 1)).OrderBy(g => g.Key)
            .Select(g => (g.Key.ToString("yyyy-MM"), $"Tháng {g.Key:MM/yyyy}", g.Sum(x => x.DoanhThu), g.Count())));

        var loaiPhongs = await _context.LoaiPhongs.AsNoTracking().ToDictionaryAsync(x => x.MaLoai, x => x.TenLoai);
        dto.TheoLoaiPhong = Chia(dem.GroupBy(x => x.MaLoai).OrderByDescending(g => g.Sum(x => x.DonGia))
            .Select(g => (g.Key, loaiPhongs.GetValueOrDefault(g.Key, g.Key), g.Sum(x => x.DonGia), g.Count())));

        dto.TheoNguonDat = Chia(dem.GroupBy(x => x.Nguon?.ToString() ?? "VANG_LAI").OrderByDescending(g => g.Sum(x => x.DonGia))
            .Select(g => (g.Key, g.Key switch
            {
                "WEBSITE" => "Khách đặt qua Website",
                "QUAY_LE_TAN" => "Lễ tân đặt tại quầy",
                _ => "Khách vãng lai (không đặt trước)"
            }, g.Sum(x => x.DonGia), g.Count())));

        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var dichVu = await _context.ChiTietSuDungDVs.AsNoTracking().Where(x => x.NgaySuDung >= tuLuc && x.NgaySuDung < denLuc)
            .GroupBy(x => new { x.MaDV, x.DichVu.TenDV })
            .Select(g => new { g.Key.MaDV, g.Key.TenDV, Tien = g.Sum(x => x.ThanhTien), SoLuong = g.Sum(x => x.SoLuong) })
            .OrderByDescending(x => x.Tien).ToListAsync();
        dto.TheoDichVu = Chia(dichVu.Select(x => (x.MaDV, x.TenDV, x.Tien, x.SoLuong)));

        var thanhToan = await _context.ChiTietThanhToans.AsNoTracking().Where(x => x.ThoiGianThanhToan >= tuLuc && x.ThoiGianThanhToan < denLuc)
            .GroupBy(x => x.HinhThucThanhToan)
            .Select(g => new { Hinh = g.Key, Tien = g.Sum(x => x.SoTien), SoLan = g.Count() }).ToListAsync();
        dto.TheoHinhThucThanhToan = Chia(thanhToan.OrderByDescending(x => x.Tien)
            .Select(x => (x.Hinh.ToString(), x.Hinh == HinhThucThanhToan.TIEN_MAT ? "Tiền mặt" : "Chuyển khoản (QR)", x.Tien, x.SoLan)));

        // ---- Chiều phân tích của kế toán
        var coc = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.NgayNhanCoc >= tuLuc && x.NgayNhanCoc < denLuc)
            .GroupBy(x => x.HinhThucCoc == HinhThucThanhToan.CHUYEN_KHOAN ? HinhThucThanhToan.CHUYEN_KHOAN : HinhThucThanhToan.TIEN_MAT)
            .Select(g => new { Hinh = g.Key, Tien = g.Sum(x => x.TienCoc), SoLan = g.Count() }).ToListAsync();
        dto.TheoHinhThucGomCoc = Chia(new[] { HinhThucThanhToan.TIEN_MAT, HinhThucThanhToan.CHUYEN_KHOAN }
            .Select(h => (h.ToString(), h == HinhThucThanhToan.TIEN_MAT ? "Tiền mặt" : "Chuyển khoản (QR)",
                thanhToan.Where(x => x.Hinh == h).Sum(x => x.Tien) + coc.Where(x => x.Hinh == h).Sum(x => x.Tien),
                thanhToan.Where(x => x.Hinh == h).Sum(x => x.SoLan) + coc.Where(x => x.Hinh == h).Sum(x => x.SoLan)))
            .OrderByDescending(x => x.Item3));

        var doanhThuHoaDon = await InvoiceRevenue.TaiAsync(_context, tuLuc, denLuc);
        var theoLoai = new List<(string, string, decimal, int)>
        {
            ("PHONG", "Doanh thu phòng", doanhThuHoaDon.Sum(x => x.Phong), doanhThuHoaDon.Count(x => x.Phong != 0)),
            ("DICH_VU", "Doanh thu dịch vụ", doanhThuHoaDon.Sum(x => x.DichVu), doanhThuHoaDon.Count(x => x.DichVu != 0)),
            ("MINIBAR", "Doanh thu minibar", doanhThuHoaDon.Sum(x => x.Minibar), doanhThuHoaDon.Count(x => x.Minibar != 0)),
            ("GIAT_LA", "Doanh thu giặt là", doanhThuHoaDon.Sum(x => x.GiatLa), doanhThuHoaDon.Count(x => x.GiatLa != 0)),
            ("KHAC", "Doanh thu khác (phụ thu, dịch vụ khác)", doanhThuHoaDon.Sum(x => x.Khac), doanhThuHoaDon.Count(x => x.Khac != 0)),
            ("GIAM_GIA", "Khuyến mãi / giảm giá", -doanhThuHoaDon.Sum(x => x.GiamGia), doanhThuHoaDon.Count(x => x.GiamGia != 0))
        };
        var tongDuong = theoLoai.Where(x => x.Item3 > 0).Sum(x => x.Item3);
        dto.TheoLoaiDoanhThu = theoLoai.Select(x => new RevenueSliceDto
        {
            Ma = x.Item1, Ten = x.Item2, DoanhThu = x.Item3, SoLuong = x.Item4, TyTrongPhanTram = PhanTram(x.Item3, tongDuong)
        }).ToList();

        string NhanTuan(DateOnly d)
        {
            var thuHai = d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
            var tuan = ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue));
            return $"{ISOWeek.GetYear(d.ToDateTime(TimeOnly.MinValue))}-W{tuan:00}|Tuần {tuan:00}/{ISOWeek.GetYear(d.ToDateTime(TimeOnly.MinValue))} ({thuHai:dd/MM} - {thuHai.AddDays(6):dd/MM})";
        }
        List<RevenueSliceDto> Nhom(Func<DateOnly, string> khoa)
            => Chia(doanhThuHoaDon.GroupBy(x => khoa(x.Ngay)).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (g.Key.Split('|')[0], g.Key.Split('|')[1], g.Sum(x => x.DoanhThu), g.Count())));
        dto.TheoTuan = Nhom(NhanTuan);
        dto.TheoQuy = Nhom(d => $"{d.Year}-Q{(d.Month - 1) / 3 + 1}|Quý {(d.Month - 1) / 3 + 1}/{d.Year}");
        dto.TheoNam = Nhom(d => $"{d.Year}|Năm {d.Year}");

        var phongs = await _context.Phongs.AsNoTracking().ToDictionaryAsync(x => x.MaPhong, x => x.Tang);
        dto.TheoPhong = Chia(dem.GroupBy(x => x.MaPhong).OrderByDescending(g => g.Sum(x => x.DonGia)).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, $"Phòng {g.Key} (tầng {phongs.GetValueOrDefault(g.Key)})", g.Sum(x => x.DonGia), g.Count())));
        dto.TheoTang = Chia(dem.GroupBy(x => phongs.GetValueOrDefault(x.MaPhong)).OrderBy(g => g.Key)
            .Select(g => ($"T{g.Key}", $"Tầng {g.Key}", g.Sum(x => x.DonGia), g.Count())));
        return dto;
    }

    private static List<RevenueSliceDto> Chia(IEnumerable<(string Ma, string Ten, decimal Tien, int SoLuong)> dong)
    {
        var ds = dong.ToList();
        var tong = ds.Sum(x => x.Tien);
        return ds.Select(x => new RevenueSliceDto { Ma = x.Ma, Ten = x.Ten, DoanhThu = x.Tien, SoLuong = x.SoLuong, TyTrongPhanTram = PhanTram(x.Tien, tong) }).ToList();
    }

    // =====================================================================================================
    // Vận hành các bộ phận
    // =====================================================================================================

    // =====================================================================================================
    // Báo cáo đặt phòng
    // =====================================================================================================

    public async Task<BookingReportDto> GetBookingReportAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var tuLuc = DauNgay(from);
        var denLuc = DauNgay(to.AddDays(1));
        var homNay = HomNay;

        var phieus = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.NgayDat >= tuLuc && x.NgayDat < denLuc)
            .Select(x => new
            {
                x.TrangThai, x.NguonDat, x.LoaiDatPhong, x.TienCoc, x.SoTienHoanCoc, x.TienCocGiuLai,
                SoPhong = x.ChiTietPhieuDats.Count,
                TongTien = x.ChiTietPhieuDats.Sum(c => (decimal?)c.ThanhTien) ?? 0
            }).ToListAsync();

        static string TenTrangThai(TrangThaiPhieuDat t) => t switch
        {
            TrangThaiPhieuDat.CHO_XAC_NHAN => "Chờ xác nhận cọc",
            TrangThaiPhieuDat.DA_XAC_NHAN => "Đã xác nhận",
            TrangThaiPhieuDat.DA_NHAN_PHONG => "Đã nhận phòng",
            TrangThaiPhieuDat.DA_TRA_PHONG => "Đã trả phòng",
            TrangThaiPhieuDat.DA_HUY => "Đã hủy",
            _ => "Khách không đến"
        };

        var dto = new BookingReportDto { TuNgay = from, DenNgay = to, TongSoPhieu = phieus.Count };
        dto.TheoTrangThai = Enum.GetValues<TrangThaiPhieuDat>().Select(t => new BookingCountDto
        {
            Ma = t.ToString(), Ten = TenTrangThai(t),
            SoLuong = phieus.Count(x => x.TrangThai == t), GiaTri = phieus.Where(x => x.TrangThai == t).Sum(x => x.TongTien)
        }).ToList();
        dto.TheoNguon = Enum.GetValues<NguonDatPhong>().Select(n => new BookingCountDto
        {
            Ma = n.ToString(), Ten = n == NguonDatPhong.WEBSITE ? "Website (khách tự đặt)" : "Quầy lễ tân",
            SoLuong = phieus.Count(x => x.NguonDat == n), GiaTri = phieus.Where(x => x.NguonDat == n).Sum(x => x.TongTien)
        }).ToList();
        dto.TheoLoai = Enum.GetValues<LoaiDatPhong>().Select(l => new BookingCountDto
        {
            Ma = l.ToString(), Ten = l == LoaiDatPhong.DOAN ? "Đặt phòng đoàn" : "Cá nhân",
            SoLuong = phieus.Count(x => x.LoaiDatPhong == l), GiaTri = phieus.Where(x => x.LoaiDatPhong == l).Sum(x => x.TongTien)
        }).ToList();
        dto.SoPhieuDoan = phieus.Count(x => x.LoaiDatPhong == LoaiDatPhong.DOAN);
        dto.SoPhongDoan = phieus.Where(x => x.LoaiDatPhong == LoaiDatPhong.DOAN).Sum(x => x.SoPhong);
        dto.TyLeHuyPhanTram = PhanTram(phieus.Count(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY), phieus.Count);
        dto.TyLeKhongDenPhanTram = PhanTram(phieus.Count(x => x.TrangThai == TrangThaiPhieuDat.KHONG_DEN), phieus.Count);
        dto.TongCocHoan = phieus.Where(x => x.TrangThai == TrangThaiPhieuDat.DA_HUY).Sum(x => x.SoTienHoanCoc);
        dto.TongCocGiuLai = phieus.Sum(x => x.TienCocGiuLai);

        dto.ChoXuLyKhongDen = await _context.PhieuDatPhongs.CountAsync(x => (x.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN || x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN)
            && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null);

        // Hủy / không đến xảy ra trong kỳ (theo NgayHuy), mới nhất trước.
        dto.HuyGanDay = await _context.PhieuDatPhongs.AsNoTracking()
            .Where(x => (x.TrangThai == TrangThaiPhieuDat.DA_HUY || x.TrangThai == TrangThaiPhieuDat.KHONG_DEN) && x.NgayHuy >= tuLuc && x.NgayHuy < denLuc)
            .OrderByDescending(x => x.NgayHuy).Take(50)
            .Select(x => new CancellationRowDto
            {
                MaPhieuDat = x.MaPhieuDat, HoTenKhach = x.Khach.HoTen, TrangThai = x.TrangThai.ToString(),
                NgayHuy = x.NgayHuy, LyDoHuy = x.LyDoHuy, HuyBoi = x.HuyBoi, TienCoc = x.TienCoc, SoTienHoanCoc = x.SoTienHoanCoc
            }).ToListAsync();
        // Người hủy hiển thị "Họ tên (Mã NV)" thay vì tên đăng nhập.
        var tenNguoiHuy = await TenNguoiDung.HienThiAsync(_context, dto.HuyGanDay.Select(x => x.HuyBoi));
        foreach (var h in dto.HuyGanDay) if (h.HuyBoi is not null) h.HuyBoi = tenNguoiHuy.GetValueOrDefault(h.HuyBoi, h.HuyBoi);
        return dto;
    }

    // =====================================================================================================
    // Công nợ
    // =====================================================================================================

    public async Task<DebtReportDto> GetDebtsAsync()
    {
        var bayGio = DateTime.Now;
        var no = await LayHoaDonConNoAsync();

        // Nhóm tuổi nợ tính từ ngày lập hóa đơn, theo THỜI ĐIỂM (không theo số ngày đã làm tròn xuống): nợ 7,5 ngày nằm ở nhóm "8-30 ngày".
        // Cờ "quá hạn" là khái niệm riêng (CongNoQuaHan: tính từ ngày khách trả phòng) nên có cột QuaHan riêng, không suy ra từ nhóm tuổi.
        static string Nhom(TimeSpan tuoi) => tuoi <= TimeSpan.FromDays(TuoiNoNhomDau) ? "0-7 ngày" : tuoi <= TimeSpan.FromDays(30) ? "8-30 ngày" : "Trên 30 ngày";
        var chiTiet = no.Select(x =>
        {
            var soNgay = Math.Max(0, (int)Math.Floor((bayGio - x.NgayLap).TotalDays));
            return new DebtRowDto
            {
                QuaHan = QuaHan(x).QuaHan,
                MaHD = x.MaHD, HoTenKhach = x.HoTen, MaPhieuDat = x.MaPhieuDat, MaKhach = x.MaKhach, NgayLap = x.NgayLap, SoNgayNo = soNgay,
                TongTien = x.TongTien, DaThanhToan = x.DaThu, ConNo = x.ConNo, NhomTuoiNo = Nhom(bayGio - x.NgayLap)
            };
        }).OrderByDescending(x => x.SoNgayNo).ThenByDescending(x => x.ConNo).ToList();

        return new DebtReportDto
        {
            TongConNo = chiTiet.Sum(x => x.ConNo),
            SoHoaDon = chiTiet.Count,
            TheoNhomTuoiNo = Chia(new[] { "0-7 ngày", "8-30 ngày", "Trên 30 ngày" }
                .Select(n => (n, n, chiTiet.Where(x => x.NhomTuoiNo == n).Sum(x => x.ConNo), chiTiet.Count(x => x.NhomTuoiNo == n)))),
            ChiTiet = chiTiet,
            TheoKhach = chiTiet.Where(x => x.MaKhach != null).GroupBy(x => x.MaKhach!)
                .Select(g => new DebtGroupDto { Ma = g.Key, HoTenKhach = g.Select(x => x.HoTenKhach).FirstOrDefault(n => n != null), SoHoaDon = g.Count(), ConNo = g.Sum(x => x.ConNo), SoNgayNoLonNhat = g.Max(x => x.SoNgayNo) })
                .OrderByDescending(x => x.ConNo).ThenBy(x => x.Ma, StringComparer.Ordinal).ToList(),
            TheoDatPhong = chiTiet.Where(x => x.MaPhieuDat != null).GroupBy(x => x.MaPhieuDat!)
                .Select(g => new DebtGroupDto { Ma = g.Key, HoTenKhach = g.Select(x => x.HoTenKhach).FirstOrDefault(n => n != null), SoHoaDon = g.Count(), ConNo = g.Sum(x => x.ConNo), SoNgayNoLonNhat = g.Max(x => x.SoNgayNo) })
                .OrderByDescending(x => x.ConNo).ThenBy(x => x.Ma, StringComparer.Ordinal).ToList()
        };
    }
}
