using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services.Finance;

/// <summary>Doanh thu của một hóa đơn, tách theo nhóm. DoanhThu = Phong + DichVu + Minibar + GiatLa + Khac − GiamGia (luôn khớp, không lệch đồng nào).</summary>
internal sealed record DongDoanhThu(
    string MaHD, string MaPhieuNhan, DateTime NgayLap, decimal DoanhThu,
    decimal Phong, decimal DichVu, decimal Minibar, decimal GiatLa, decimal Khac, decimal GiamGia)
{
    public DateOnly Ngay => DateOnly.FromDateTime(NgayLap);
}

/// <summary>
/// Doanh thu theo hóa đơn lập trong kỳ, chỉ tính hóa đơn đã thu được ít nhất một phần (cùng định nghĩa với báo cáo của Quản lý): DoanhThu = TongTien + TienDaCoc.
/// Phân loại: tiền phòng → Phòng; dịch vụ chia theo nhóm doanh thu của dịch vụ (minibar / giặt là / khác / dịch vụ chung); phụ thu → Khác;
/// phần chênh giữa tổng gộp và DoanhThu là khuyến mãi (GiamGia).
/// </summary>
internal static class InvoiceRevenue
{
    public static async Task<List<DongDoanhThu>> TaiAsync(KLCN060DbContext context, DateTime tuLuc, DateTime denLuc)
    {
        var hoaDons = await context.HoaDons.AsNoTracking()
            .Where(x => x.NgayLap >= tuLuc && x.NgayLap < denLuc && x.TrangThaiThanhToan != TrangThaiThanhToan.CHUA_THANH_TOAN)
            .Select(x => new { x.MaHD, x.MaPhieuNhan, x.NgayLap, x.TienPhong, x.TienDV, x.PhuThu, x.TienDaCoc, x.TongTien })
            .ToListAsync();
        if (hoaDons.Count == 0) return new List<DongDoanhThu>();

        var maPhieuNhans = hoaDons.Select(x => x.MaPhieuNhan).Distinct().ToList();
        var dichVu = await context.ChiTietSuDungDVs.AsNoTracking()
            .Where(x => maPhieuNhans.Contains(x.MaPhieuNhan))
            .GroupBy(x => new { x.MaPhieuNhan, x.DichVu.LoaiDoanhThu })
            .Select(g => new { g.Key.MaPhieuNhan, g.Key.LoaiDoanhThu, Tien = g.Sum(x => x.ThanhTien) })
            .ToListAsync();
        var theoLuot = dichVu.GroupBy(x => x.MaPhieuNhan).ToDictionary(
            g => g.Key,
            g => (Tong: g.Sum(x => x.Tien),
                  Minibar: g.Where(x => x.LoaiDoanhThu == LoaiDoanhThuDichVu.MINIBAR).Sum(x => x.Tien),
                  GiatLa: g.Where(x => x.LoaiDoanhThu == LoaiDoanhThuDichVu.GIAT_LA).Sum(x => x.Tien),
                  Khac: g.Where(x => x.LoaiDoanhThu == LoaiDoanhThuDichVu.KHAC).Sum(x => x.Tien)));

        return hoaDons.Select(h =>
        {
            decimal minibar = 0, giatLa = 0, dvKhac = 0;
            if (h.TienDV > 0 && theoLuot.TryGetValue(h.MaPhieuNhan, out var d) && d.Tong > 0)
            {
                // Chia theo tỉ lệ để tổng các nhóm luôn bằng đúng TienDV của hóa đơn, kể cả khi một lượt ở có nhiều hóa đơn.
                minibar = Math.Round(h.TienDV * d.Minibar / d.Tong, 2);
                giatLa = Math.Round(h.TienDV * d.GiatLa / d.Tong, 2);
                dvKhac = Math.Round(h.TienDV * d.Khac / d.Tong, 2);
            }
            var dichVuChung = h.TienDV - minibar - giatLa - dvKhac;
            var doanhThu = h.TongTien + h.TienDaCoc;
            var chenh = h.TienPhong + h.TienDV + h.PhuThu - doanhThu;   // dương = đã giảm; âm = cọc giữ lại lớn hơn tổng tiền (tính vào "khác")
            return new DongDoanhThu(h.MaHD, h.MaPhieuNhan, h.NgayLap, doanhThu,
                h.TienPhong, dichVuChung, minibar, giatLa, h.PhuThu + dvKhac + Math.Max(0, -chenh), Math.Max(0, chenh));
        }).ToList();
    }
}
