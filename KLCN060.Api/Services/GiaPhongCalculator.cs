using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Kết quả tính giá cho một khoảng đêm của một loại phòng.</summary>
public record KetQuaGia(decimal DonGiaBinhQuan, decimal TongTien, List<(DateOnly Dem, decimal Gia, QuyTacGia? QuyTac)> TheoDem);

/// <summary>
/// Tính giá phòng theo đêm từ đơn giá cơ bản của loại phòng và các quy tắc giá đang bật (cuối tuần, mùa cao điểm, lễ tết, giá đoàn...).
/// Mỗi đêm: quy tắc khớp có ưu tiên cao nhất được áp dụng (bằng nhau thì lấy quy tắc mới tạo hơn); không quy tắc nào khớp thì tính giá cơ bản.
/// TỔNG TIỀN = tổng giá từng đêm (chính xác từng đồng); đơn giá bình quân chỉ để hiển thị/tham khảo (làm tròn đến đồng). Giá từng đêm được chốt vào
/// <see cref="GiaDemDatPhong"/> khi đặt phòng (xem <see cref="TaoGiaChot"/>) nên hóa đơn không bao giờ lệch so với giá đã báo cho khách.
/// </summary>
public static class GiaPhongCalculator
{
    public static Task<List<QuyTacGia>> LayQuyTacDangBatAsync(KLCN060DbContext context)
        => context.QuyTacGias.AsNoTracking().Where(x => x.HoatDong).ToListAsync();

    public static bool ApDungChoDem(QuyTacGia q, string maLoai, DateOnly dem, LoaiDatPhong loaiDat)
    {
        if (!q.HoatDong) return false;
        if (q.MaLoai is not null && q.MaLoai != maLoai) return false;
        if (q.TuNgay is { } tu && dem < tu) return false;
        if (q.DenNgay is { } den && dem > den) return false;
        if (q.LoaiDatPhongApDung is { } loai && loai != loaiDat) return false;
        if (!string.IsNullOrWhiteSpace(q.CacThuApDung))
        {
            var thu = ((int)dem.DayOfWeek).ToString();
            if (!q.CacThuApDung.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(x => x == thu))
                return false;
        }
        return true;
    }

    public static decimal GiaDem(LoaiPhong loai, DateOnly dem, LoaiDatPhong loaiDat, IEnumerable<QuyTacGia> quyTacs, out QuyTacGia? apDung)
    {
        apDung = quyTacs.Where(q => ApDungChoDem(q, loai.MaLoai, dem, loaiDat))
            .OrderByDescending(q => q.UuTien).ThenByDescending(q => q.MaQuyTac)
            .FirstOrDefault();
        if (apDung is null) return loai.DonGia;

        var gia = apDung.KieuDieuChinh == KieuDieuChinhGia.GIA_CO_DINH
            ? apDung.GiaTri
            : loai.DonGia * (1 + apDung.GiaTri / 100m);
        return Math.Max(0m, Math.Round(gia, 0, MidpointRounding.AwayFromZero));
    }

    /// <summary>Giá cho các đêm [nhan, tra); khoảng rỗng được tính là 1 đêm.</summary>
    public static KetQuaGia Tinh(LoaiPhong loai, DateOnly nhan, DateOnly tra, LoaiDatPhong loaiDat, IReadOnlyCollection<QuyTacGia> quyTacs)
    {
        var soDem = Math.Max(1, tra.DayNumber - nhan.DayNumber);
        var theoDem = new List<(DateOnly Dem, decimal Gia, QuyTacGia? QuyTac)>();
        for (var i = 0; i < soDem; i++)
        {
            var dem = nhan.AddDays(i);
            theoDem.Add((dem, GiaDem(loai, dem, loaiDat, quyTacs, out var quyTac), quyTac));
        }

        var tong = theoDem.Sum(x => x.Gia);
        // Mọi đêm cùng một giá (kể cả không có quy tắc) → đúng giá đó; khác nhau → bình quân làm tròn đến đồng (chỉ để hiển thị, KHÔNG dùng để tính tiền).
        var donGia = theoDem.Select(x => x.Gia).Distinct().Count() == 1
            ? theoDem[0].Gia
            : Math.Round(tong / soDem, 0, MidpointRounding.AwayFromZero);
        return new KetQuaGia(donGia, tong, theoDem);
    }

    /// <summary>Mô tả cách quy tắc điều chỉnh giá, VD "+20%", "-10%", "giá cố định 900.000đ".</summary>
    public static string MoTaDieuChinh(QuyTacGia q)
        => q.KieuDieuChinh == KieuDieuChinhGia.GIA_CO_DINH
            ? $"giá cố định {q.GiaTri.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}đ"
            : $"{(q.GiaTri > 0 ? "+" : "")}{q.GiaTri.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%";

    /// <summary>Dựng các dòng giá chốt từng đêm của một phòng trong phiếu đặt từ kết quả tính giá.</summary>
    public static List<GiaDemDatPhong> TaoGiaChot(string maPhieuDat, string maPhong, LoaiPhong loai, KetQuaGia kq)
        => kq.TheoDem.Select(d => new GiaDemDatPhong
        {
            MaPhieuDat = maPhieuDat, MaPhong = maPhong, Dem = d.Dem, GiaCoBan = loai.DonGia, Gia = d.Gia,
            MaQuyTac = d.QuyTac?.MaQuyTac, TenQuyTac = d.QuyTac?.TenQuyTac, DieuChinh = d.QuyTac is null ? null : MoTaDieuChinh(d.QuyTac)
        }).ToList();

    /// <summary>Đơn giá một đêm cho ngày <paramref name="dem"/> (dùng cho khách vãng lai / đổi phòng khi chưa biết số đêm ở).</summary>
    public static async Task<decimal> DonGiaDemAsync(KLCN060DbContext context, LoaiPhong loai, DateOnly dem, LoaiDatPhong loaiDat)
        => GiaDem(loai, dem, loaiDat, await LayQuyTacDangBatAsync(context), out _);
}
