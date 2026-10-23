using System.Globalization;
using KLCN060.Domain;
using KLCN060.Domain.Enums;

namespace KLCN060.Api.Services;

/// <summary>Một điều kiện áp dụng khuyến mãi và kết quả với một booking cụ thể.</summary>
public sealed record KiemTraKhuyenMai(string Ma, string Ten, bool Dat, string ChiTiet);

/// <summary>
/// Quy tắc áp dụng khuyến mãi dùng CHUNG cho đặt phòng, sửa phiếu, nhận sớm và phần "giải thích vì sao booking được / không được hưởng":
/// chương trình đang hoạt động (không tạm ngưng) → ngày đặt nằm trong thời gian áp dụng → còn lượt → có phòng thuộc phạm vi (loại phòng)
/// → đủ số đêm tối thiểu → đủ giá trị tối thiểu. Tiền giảm tính trên phần tiền thuộc phạm vi, tối đa bằng phần tiền đó.
/// Mỗi đặt phòng chỉ áp dụng MỘT khuyến mãi (không cộng dồn); đơn giá đã gồm quy tắc giá theo mùa / cuối tuần / lễ trước khi trừ khuyến mãi.
/// </summary>
public static class PromotionRules
{
    private static string Tien(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ";

    /// <summary>Phần tiền phòng thuộc phạm vi của khuyến mãi (toàn bộ nếu khuyến mãi áp dụng mọi loại phòng).</summary>
    public static decimal CoSoTinh(KhuyenMai km, IEnumerable<(string MaLoai, decimal ThanhTien)> dong)
        => km.MaLoaiApDung is null ? dong.Sum(x => x.ThanhTien) : dong.Where(x => x.MaLoai == km.MaLoaiApDung).Sum(x => x.ThanhTien);

    /// <summary>Tiền khuyến mãi của booking = giảm tính trên phần tiền thuộc phạm vi (không âm, không vượt phần đó).</summary>
    public static decimal TinhTien(KhuyenMai km, IEnumerable<(string MaLoai, decimal ThanhTien)> dong)
        => BookingService.TinhKhuyenMai(km, CoSoTinh(km, dong));

    public static List<KiemTraKhuyenMai> DanhGia(KhuyenMai km, DateOnly ngayDat, int soDem, IReadOnlyCollection<(string MaLoai, decimal ThanhTien)> dong, bool tinhLuot, string? tenLoaiApDung = null)
    {
        var ds = new List<KiemTraKhuyenMai>();
        ds.Add(new("HOAT_DONG", "Chương trình đang hoạt động", !km.TamNgung,
            km.TamNgung ? $"Đang tạm ngưng{(string.IsNullOrWhiteSpace(km.LyDoTamNgung) ? "" : $": {km.LyDoTamNgung}")}." : "Không tạm ngưng."));
        var trongKy = ngayDat >= km.NgayBatDau && ngayDat <= km.NgayKetThuc;
        ds.Add(new("THOI_GIAN", "Ngày đặt nằm trong thời gian áp dụng", trongKy,
            $"Ngày đặt {ngayDat:dd/MM/yyyy}; chương trình áp dụng {km.NgayBatDau:dd/MM/yyyy} - {km.NgayKetThuc:dd/MM/yyyy}."));
        if (tinhLuot)
        {
            var conLuot = km.SoLuongGioiHan is null || km.SoLuongDaSuDung < km.SoLuongGioiHan;
            ds.Add(new("LUOT", "Còn lượt sử dụng", conLuot,
                km.SoLuongGioiHan is null ? "Không giới hạn lượt." : $"Đã dùng {km.SoLuongDaSuDung}/{km.SoLuongGioiHan} lượt."));
        }
        if (km.MaLoaiApDung is not null)
        {
            var co = dong.Any(x => x.MaLoai == km.MaLoaiApDung);
            ds.Add(new("PHAM_VI", "Có phòng thuộc loại được áp dụng", co,
                $"Chỉ áp dụng cho loại {tenLoaiApDung ?? km.MaLoaiApDung}; booking {(co ? "có" : "không có")} phòng loại này."));
        }
        if (km.SoDemToiThieu is { } minDem)
            ds.Add(new("SO_DEM", "Đủ số đêm tối thiểu", soDem >= minDem, $"Cần tối thiểu {minDem} đêm; booking ở {soDem} đêm."));
        if (km.TongTienToiThieu is { } minTien)
        {
            var coSo = CoSoTinh(km, dong);
            ds.Add(new("GIA_TRI", "Đủ giá trị tiền phòng tối thiểu", coSo >= minTien,
                $"Cần tiền phòng{(km.MaLoaiApDung is null ? "" : " thuộc phạm vi")} tối thiểu {Tien(minTien)}; booking có {Tien(coSo)}."));
        }
        return ds;
    }
}
