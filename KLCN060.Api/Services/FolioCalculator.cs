using KLCN060.Api.Middlewares;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public record FolioLine(LoaiKhoanMuc Loai, string MoTa, int SoLuong, decimal DonGia, decimal ThanhTien);

/// <summary>Tổng hợp folio của 1 phiếu nhận phòng (mọi phòng + mọi dịch vụ). Chỉ tính toán, không ghi DB.</summary>
public class Folio
{
    public string MaPhieuNhan { get; init; } = null!;
    public string? MaPhieuDat { get; init; }
    public string? MaKhach { get; init; }
    public List<FolioLine> Lines { get; init; } = new();
    public decimal TienPhong { get; init; }
    public decimal PhuThu { get; init; }
    public decimal TienDichVu { get; init; }
    public decimal TongCong => TienPhong + PhuThu + TienDichVu;
    public decimal TongKhuyenMai { get; init; }
    public decimal TienDaCoc { get; init; }

    /// <summary>Số tiền còn phải thu sau khi trừ khuyến mãi và tiền cọc (không âm).</summary>
    public decimal ConPhaiThu => Math.Max(0, TongCong - TongKhuyenMai - TienDaCoc);
}

public static class FolioCalculator
{
    public static async Task<Folio> ComputeAsync(KLCN060DbContext context, string maPhieuNhan, DateOnly homNay)
    {
        var phieuNhan = await context.PhieuNhanPhongs
            .AsNoTracking()
            .Include(x => x.PhieuDatPhong).ThenInclude(p => p!.ChiTietKhuyenMais)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");

        var luotO = await context.ChiTietPhieuNhans
            .AsNoTracking()
            .Include(x => x.Phong).ThenInclude(p => p.LoaiPhong)
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .OrderBy(x => x.NgayNhan).ThenBy(x => x.MaPhong)
            .ToListAsync();

        // Phòng đã bị đổi đi: nếu đổi ngay trong ngày nhận thì không tính đêm nào cho phòng cũ.
        var phongDaDoiDi = (await context.PhieuDoiPhongs
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .Select(x => x.MaPhongCu)
            .ToListAsync()).ToHashSet();

        var dichVus = await context.ChiTietSuDungDVs
            .AsNoTracking()
            .Include(x => x.DichVu)
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .OrderBy(x => x.NgaySuDung)
            .ToListAsync();

        var lines = new List<FolioLine>();
        decimal tienPhong = 0, phuThu = 0;

        foreach (var o in luotO)
        {
            var ketThuc = o.NgayTra ?? homNay;
            var soDem = Math.Max(ketThuc.DayNumber - o.NgayNhan.DayNumber, phongDaDoiDi.Contains(o.MaPhong) ? 0 : 1);
            var loai = o.Phong.LoaiPhong;

            if (soDem > 0)
            {
                var tt = o.DonGia * soDem;
                tienPhong += tt;
                lines.Add(new FolioLine(LoaiKhoanMuc.TIEN_PHONG, $"Tiền phòng {o.Phong.MaPhong} ({loai.TenLoai})", soDem, o.DonGia, tt));

                // Phụ thu vượt số người tiêu chuẩn, tính theo từng đêm.
                var nguoiThem = Math.Max(0, o.SoNguoi - loai.SoNguoiTieuChuan);
                if (nguoiThem > 0 && loai.PhuThu > 0)
                {
                    var phuThuNguoi = loai.PhuThu * nguoiThem * soDem;
                    phuThu += phuThuNguoi;
                    lines.Add(new FolioLine(LoaiKhoanMuc.PHU_THU, $"Phụ thu {nguoiThem} người vượt tiêu chuẩn - phòng {o.Phong.MaPhong}", nguoiThem * soDem, loai.PhuThu, phuThuNguoi));
                }
            }

            if (o.TienPhuThu > 0)
            {
                phuThu += o.TienPhuThu;
                lines.Add(new FolioLine(LoaiKhoanMuc.PHU_THU, $"Phụ thu trả phòng trễ giờ - phòng {o.Phong.MaPhong}", 1, o.TienPhuThu, o.TienPhuThu));
            }
        }

        decimal tienDV = 0;
        foreach (var dv in dichVus)
        {
            tienDV += dv.ThanhTien;
            lines.Add(new FolioLine(LoaiKhoanMuc.DICH_VU, dv.DichVu.TenDV, dv.SoLuong, dv.SoLuong == 0 ? 0 : dv.ThanhTien / dv.SoLuong, dv.ThanhTien));
        }

        var khuyenMai = Math.Min(phieuNhan.PhieuDatPhong?.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai) ?? 0, tienPhong);
        if (khuyenMai > 0)
            lines.Add(new FolioLine(LoaiKhoanMuc.KHUYEN_MAI, "Khuyến mãi", 1, -khuyenMai, -khuyenMai));

        return new Folio
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhieuDat = phieuNhan.MaPhieuDat,
            MaKhach = phieuNhan.PhieuDatPhong?.MaKhach,
            Lines = lines,
            TienPhong = tienPhong,
            PhuThu = phuThu,
            TienDichVu = tienDV,
            TongKhuyenMai = khuyenMai,
            TienDaCoc = phieuNhan.PhieuDatPhong?.TienCoc ?? 0
        };
    }
}
