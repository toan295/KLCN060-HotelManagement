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

    /// <summary>Giải thích khi khuyến mãi đã chốt lúc đặt không còn được hưởng (khách ở ngắn hơn điều kiện của chương trình); null nếu không có gì đáng nói.</summary>
    public string? GhiChuKhuyenMai { get; init; }
    public decimal TienDaCoc { get; init; }

    /// <summary>Số tiền còn phải thu sau khi trừ khuyến mãi và tiền cọc (không âm).</summary>
    public decimal ConPhaiThu => Math.Max(0, TongCong - TongKhuyenMai - TienDaCoc);
}

public static class FolioCalculator
{
    /// <param name="chotLuuTru">
    /// true khi đây là lần tính để TRẢ PHÒNG CUỐI CÙNG (không còn phòng nào của phiếu nhận đang ở): thời gian ở đã chốt nên được kiểm tra lại điều kiện
    /// số đêm / tiền phòng tối thiểu của khuyến mãi. Khi đang ở giữa chừng (false) chỉ giảm theo tiền phòng đã ở, chưa tước khuyến mãi.
    /// </param>
    public static async Task<Folio> ComputeAsync(KLCN060DbContext context, string maPhieuNhan, DateOnly homNay, bool chotLuuTru = false)
    {
        var phieuNhan = await context.PhieuNhanPhongs
            .AsNoTracking()
            .Include(x => x.PhieuDatPhong).ThenInclude(p => p!.ChiTietKhuyenMais).ThenInclude(k => k.KhuyenMai)
            .Include(x => x.PhieuDatPhong).ThenInclude(p => p!.ChiTietPhieuDats)
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

        // Giá chốt từng đêm của phiếu đặt (nếu có): tiền phòng = tổng giá các đêm đã ở, không dùng đơn giá bình quân.
        var giaChot = phieuNhan.MaPhieuDat is null ? new Dictionary<(string, DateOnly), decimal>()
            : (await context.GiaDemDatPhongs.AsNoTracking().Where(x => x.MaPhieuDat == phieuNhan.MaPhieuDat).ToListAsync())
                .ToDictionary(x => (x.MaPhong, x.Dem), x => x.Gia);

        var dichVus = await context.ChiTietSuDungDVs
            .AsNoTracking()
            .Include(x => x.DichVu)
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .OrderBy(x => x.NgaySuDung)
            .ToListAsync();

        var lines = new List<FolioLine>();
        decimal tienPhong = 0, phuThu = 0;
        var tienPhongTheoLoai = new List<(string MaLoai, decimal ThanhTien)>();

        foreach (var o in luotO)
        {
            var ketThuc = o.NgayTra ?? homNay;
            var soDem = Math.Max(ketThuc.DayNumber - o.NgayNhan.DayNumber, phongDaDoiDi.Contains(o.MaPhong) ? 0 : 1);
            var loai = o.Phong.LoaiPhong;

            if (soDem > 0)
            {
                var giaTungDem = Enumerable.Range(0, soDem)
                    .Select(i => giaChot.TryGetValue((o.MaPhong, o.NgayNhan.AddDays(i)), out var g) ? g : o.DonGia).ToList();
                var tt = giaTungDem.Sum();
                var khacGia = giaTungDem.Distinct().Count() > 1;
                var donGiaHienThi = khacGia ? Math.Round(tt / soDem, 0, MidpointRounding.AwayFromZero) : giaTungDem[0];
                tienPhong += tt;
                tienPhongTheoLoai.Add((loai.MaLoai, tt));
                lines.Add(new FolioLine(LoaiKhoanMuc.TIEN_PHONG, $"Tiền phòng {o.Phong.MaPhong} ({loai.TenLoai})" + (khacGia ? " - giá theo từng đêm" : ""), soDem, donGiaHienThi, tt));

                // Phụ thu vượt số người tiêu chuẩn, tính theo từng đêm.
                // Dùng sức chứa/phụ thu đã chốt lúc nhận phòng; lượt cũ chưa chốt mới theo loại phòng hiện hành.
                var tieuChuan = o.SoNguoiTieuChuanChot ?? loai.SoNguoiTieuChuan;
                var phuThuMoiNguoi = o.PhuThuNguoiThemChot ?? loai.PhuThu;
                var nguoiThem = Math.Max(0, o.SoNguoi - tieuChuan);
                if (nguoiThem > 0 && phuThuMoiNguoi > 0)
                {
                    var phuThuNguoi = phuThuMoiNguoi * nguoiThem * soDem;
                    phuThu += phuThuNguoi;
                    lines.Add(new FolioLine(LoaiKhoanMuc.PHU_THU, $"Phụ thu {nguoiThem} người vượt tiêu chuẩn - phòng {o.Phong.MaPhong}", nguoiThem * soDem, phuThuMoiNguoi, phuThuNguoi));
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
            lines.Add(new FolioLine(LoaiKhoanMuc.DICH_VU, dv.TenDichVu ?? dv.DichVu.TenDV, dv.SoLuong, dv.SoLuong == 0 ? 0 : dv.ThanhTien / dv.SoLuong, dv.ThanhTien));
        }

        // Khuyến mãi được tính lúc đặt trên toàn bộ số đêm đã đặt; khách ở ngắn hơn thì chỉ được giảm theo phần tiền phòng thực ở
        // (tiền phòng thực tế / tiền phòng đã đặt). Ở đủ hoặc quá kỳ đặt (gia hạn, đổi phòng đắt hơn) thì giữ nguyên mức đã chốt.
        // Khi lưu trú đã chốt (trả phòng cuối): khuyến mãi có điều kiện số đêm / tiền phòng tối thiểu chỉ còn hiệu lực nếu thời gian ở thực tế vẫn đạt điều kiện đó.
        // Các điều kiện chỉ xét lúc đặt (thời gian áp dụng, số lượt) không xét lại.
        var khuyenMaiDaChot = 0m;
        var ghiChu = new List<string>();
        var soDemThucTe = luotO.Count == 0 ? 0 : luotO.Max(o => o.NgayTra ?? homNay).DayNumber - luotO.Min(o => o.NgayNhan).DayNumber;
        foreach (var ct in phieuNhan.PhieuDatPhong?.ChiTietKhuyenMais ?? [])
        {
            var km = ct.KhuyenMai;
            if (chotLuuTru && luotO.Count > 0)
            {
                if (km.SoDemToiThieu is { } minDem && soDemThucTe < minDem)
                {
                    ghiChu.Add($"Khuyến mãi \"{km.TenKM}\" không còn áp dụng vì khách ở {soDemThucTe} đêm, chương trình yêu cầu tối thiểu {minDem} đêm.");
                    continue;
                }
                if (km.TongTienToiThieu is { } minTien && PromotionRules.CoSoTinh(km, tienPhongTheoLoai) < minTien)
                {
                    ghiChu.Add($"Khuyến mãi \"{km.TenKM}\" không còn áp dụng vì tiền phòng thực tế thấp hơn mức tối thiểu {minTien:N0}đ của chương trình.");
                    continue;
                }
            }
            khuyenMaiDaChot += ct.TongKhuyenMai;
        }
        var tienPhongDaDat = phieuNhan.PhieuDatPhong?.ChiTietPhieuDats.Sum(x => x.ThanhTien) ?? 0;
        if (khuyenMaiDaChot > 0 && tienPhongDaDat > 0 && tienPhong < tienPhongDaDat)
            khuyenMaiDaChot = Math.Round(khuyenMaiDaChot * tienPhong / tienPhongDaDat, 0, MidpointRounding.AwayFromZero);
        var khuyenMai = Math.Min(khuyenMaiDaChot, tienPhong);
        if (khuyenMai > 0)
            lines.Add(new FolioLine(LoaiKhoanMuc.KHUYEN_MAI, "Khuyến mãi", 1, -khuyenMai, -khuyenMai));

        return new Folio
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhieuDat = phieuNhan.MaPhieuDat,
            // [Khối 8 - K8-3] Cùng nguyên nhân K7-1: trước đây chỉ suy khách qua PhieuDatPhong → lượt walk-in có MaKhach = null →
            // StayService.GetFolioAsync từ chối (403) chính chủ khi khách xem folio của mình. Đọc thẳng PhieuNhanPhong.MaKhach
            // (nguồn chung cho cả 2 luồng nhận phòng), fallback qua phiếu đặt cho dữ liệu trước migration K7-1.
            MaKhach = phieuNhan.MaKhach ?? phieuNhan.PhieuDatPhong?.MaKhach,
            Lines = lines,
            TienPhong = tienPhong,
            PhuThu = phuThu,
            TienDichVu = tienDV,
            TongKhuyenMai = khuyenMai,
            GhiChuKhuyenMai = ghiChu.Count == 0 ? null : string.Join(" ", ghiChu),
            TienDaCoc = phieuNhan.PhieuDatPhong?.TienCoc ?? 0
        };
    }
}
