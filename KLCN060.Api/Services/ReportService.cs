using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Middlewares;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class ReportService : IReportService
{
    private const int SoNgayToiDa = 366;

    private readonly KLCN060DbContext _context;

    public ReportService(KLCN060DbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Doanh thu theo ngày lập hóa đơn, chỉ tính hóa đơn đã thu được ít nhất 1 phần. Doanh thu 1 hóa đơn =
    /// TongTien (phần thu tại quầy) + TienDaCoc (đã thu trước qua đặt cọc), vì TongTien đã trừ tiền cọc.
    /// </summary>
    public async Task<RevenueReportDto> RevenueAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var tuLuc = from.ToDateTime(TimeOnly.MinValue);
        var denLuc = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var hoaDons = await _context.HoaDons.AsNoTracking()
            .Where(x => x.NgayLap >= tuLuc && x.NgayLap < denLuc && x.TrangThaiThanhToan != TrangThaiThanhToan.CHUA_THANH_TOAN)
            .Select(x => new { x.NgayLap, x.TongTien, x.TienDaCoc })
            .ToListAsync();

        var theoNgay = hoaDons
            .GroupBy(x => DateOnly.FromDateTime(x.NgayLap))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TongTien + x.TienDaCoc));

        var ketQua = new RevenueReportDto { TuNgay = from, DenNgay = to };
        for (var d = from; d <= to; d = d.AddDays(1))
            ketQua.TheoNgay.Add(new RevenueDayDto { Ngay = d, DoanhThu = theoNgay.GetValueOrDefault(d) });
        ketQua.TongCong = ketQua.TheoNgay.Sum(x => x.DoanhThu);
        return ketQua;
    }

    /// <summary>
    /// Tỉ lệ lấp đầy mỗi ngày = số phòng có khách qua đêm ÷ tổng số phòng. Một lượt ở chiếm các đêm [NgayNhan, NgayTra);
    /// lượt đang ở chiếm tới hết hôm nay; lượt nhận-trả trong cùng ngày tính 1 đêm (giống cách tính tiền phòng),
    /// trừ dòng phòng đã bị đổi đi ngay trong ngày.
    /// </summary>
    public async Task<OccupancyReportDto> OccupancyAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var homNay = DateOnly.FromDateTime(DateTime.Now);

        var tongSoPhong = await _context.Phongs.CountAsync();
        var luotO = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.NgayNhan <= to && (x.NgayTra == null || x.NgayTra >= from))
            .Select(x => new { x.MaPhieuNhan, x.MaPhong, x.NgayNhan, x.NgayTra })
            .ToListAsync();
        var daDoiDi = (await _context.PhieuDoiPhongs.Select(x => new { x.MaPhieuNhan, x.MaPhongCu }).ToListAsync())
            .Select(x => (x.MaPhieuNhan, x.MaPhongCu)).ToHashSet();

        var khoangO = luotO.Select(x =>
        {
            var ketThuc = x.NgayTra ?? homNay.AddDays(1);
            if (x.NgayTra is not null && ketThuc <= x.NgayNhan && !daDoiDi.Contains((x.MaPhieuNhan, x.MaPhong)))
                ketThuc = x.NgayNhan.AddDays(1);
            return (x.MaPhong, Bat: x.NgayNhan, Het: ketThuc);
        }).ToList();

        var ketQua = new OccupancyReportDto { TuNgay = from, DenNgay = to, TongSoPhong = tongSoPhong };
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var ngay = d;
            var soPhong = khoangO.Where(x => x.Bat <= ngay && ngay < x.Het).Select(x => x.MaPhong).Distinct().Count();
            ketQua.TheoNgay.Add(new OccupancyDayDto
            {
                Ngay = d,
                SoPhongCoKhach = soPhong,
                TyLePhanTram = tongSoPhong == 0 ? 0 : Math.Round(soPhong * 100m / tongSoPhong, 2)
            });
        }
        ketQua.TyLeTrungBinhPhanTram = ketQua.TheoNgay.Count == 0 ? 0 : Math.Round(ketQua.TheoNgay.Average(x => x.TyLePhanTram), 2);
        return ketQua;
    }

    /// <summary>
    /// Khách mới = khách có lượt lưu trú đầu tiên (theo ngày nhận) rơi vào khoảng; khách quay lại = có lượt trong khoảng
    /// và đã có lượt trước khoảng. Khách vãng lai không gắn với hồ sơ khách (phiếu nhận không có phiếu đặt) không được tính.
    /// </summary>
    public async Task<GuestReportDto> GuestsAsync(DateOnly from, DateOnly to)
    {
        KiemTraKhoang(from, to);
        var tuLuc = from.ToDateTime(TimeOnly.MinValue);
        var denLuc = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var luot = await _context.PhieuNhanPhongs.AsNoTracking()
            .Where(x => x.PhieuDatPhong != null && x.NgayNhan < denLuc)
            .Select(x => new { x.PhieuDatPhong!.MaKhach, x.NgayNhan })
            .ToListAsync();

        var moi = 0;
        var quayLai = 0;
        foreach (var g in luot.GroupBy(x => x.MaKhach))
        {
            if (!g.Any(x => x.NgayNhan >= tuLuc)) continue;
            if (g.Min(x => x.NgayNhan) >= tuLuc) moi++;
            else quayLai++;
        }

        return new GuestReportDto { TuNgay = from, DenNgay = to, KhachMoi = moi, KhachQuayLai = quayLai, TongKhach = moi + quayLai };
    }

    private static void KiemTraKhoang(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "to phải sau hoặc bằng from.", "to");
        if (to.DayNumber - from.DayNumber + 1 > SoNgayToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Khoảng báo cáo tối đa {SoNgayToiDa} ngày.", "to");
    }
}
