using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Màn Quản lý cơ sở vật chất (D14): tổng quan + danh sách đầy đủ, thêm vào phòng/kho, điều chuyển, lịch sử.
public sealed partial class ApiClient
{
    public Task<CsvcTongQuanDto> GetCsvcTongQuanAsync() => GetAsync<CsvcTongQuanDto>("api/v1/facilities/overview");
    public Task<List<CsvcLichSuDto>> GetCsvcLichSuAsync(string maSo) => GetAsync<List<CsvcLichSuDto>>($"api/v1/facilities/{Uri.EscapeDataString(maSo)}/history");
    public Task<CoSoVatChatDto> TaoCsvcAsync(YeuCauCoSoVatChat x) => PostAsync<YeuCauCoSoVatChat, CoSoVatChatDto>("api/v1/facilities", x);
    public Task<CoSoVatChatDto> DieuChuyenCsvcAsync(string maSo, YeuCauDieuChuyenCsvc x) => PostAsync<YeuCauDieuChuyenCsvc, CoSoVatChatDto>($"api/v1/facilities/{Uri.EscapeDataString(maSo)}/transfer", x);
    public Task XoaCsvcAsync(string maSo, string? lyDo) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/facilities/{Uri.EscapeDataString(maSo)}" + (string.IsNullOrWhiteSpace(lyDo) ? "" : $"?lyDo={Uri.EscapeDataString(lyDo)}"));
}

public sealed record YeuCauDieuChuyenCsvc(string? MaPhongMoi, int? SoLuong, string LyDo);

public static class TinhTrangCsvcText
{
    public static string HienThi(string t) => t switch
    {
        "TOT" => "Tốt", "HONG_CHO_SUA" => "Hỏng, chờ sửa", "HONG_DANG_SUA" => "Hỏng, đang sửa", "DA_THAY_MOI" => "Đã thay mới",
        "NGUNG_SU_DUNG" => "Ngừng sử dụng", "DA_THANH_LY" => "Đã thanh lý", _ => t
    };
}

public sealed class CsvcThietBiDto
{
    public string MaSo { get; init; } = "";
    public string Ten { get; init; } = "";
    public int SoLuong { get; init; }
    public string TinhTrang { get; init; } = "";
    public string? MaPhong { get; init; }
    public string? TenPhong { get; init; }
    public int? Tang { get; init; }
    public string? GhiChu { get; init; }
    public DateTime? TinhTrangTuLuc { get; init; }
    public string? LyDoTinhTrang { get; init; }
    public DateTime? NgayTao { get; init; }
    public DateTime? CapNhatLuc { get; init; }
    public string? NguoiCapNhat { get; init; }
    public int? SoNgayHong { get; init; }
    public bool CanXuLy { get; init; }
    public string? GoiY { get; init; }
    public bool CoTheXoa { get; init; }
    public string? LyDoKhongXoa { get; init; }

    public string TinhTrangHienThi => TinhTrangCsvcText.HienThi(TinhTrang);
    public string ViTriHienThi => MaPhong is null ? "Kho / dùng chung" : $"{TenPhong ?? MaPhong}" + (Tang is { } t ? $" · tầng {t}" : "");
    public string TuLucHienThi => TinhTrangTuLuc is { } d ? $"từ {d:dd/MM/yyyy}" + (SoNgayHong is { } n ? $" ({n} ngày)" : "") : "chưa rõ từ khi nào";
    public string CapNhatHienThi => CapNhatLuc is null ? "" : $"Cập nhật: {NguoiCapNhat ?? "?"} lúc {CapNhatLuc:dd/MM/yyyy HH:mm}";
}

public sealed class CsvcTinhTrangDemDto { public string TinhTrang { get; init; } = ""; public int SoDong { get; init; } public int SoLuong { get; init; } }

public sealed class CsvcTongHopDto
{
    public int TongDong { get; init; }
    public int TongSoLuong { get; init; }
    public int SoLuongDangDung { get; init; }
    public int SoLuongHong { get; init; }
    public int SoLuongTrongKho { get; init; }
    public int SoPhongCoThietBiHong { get; init; }
    public int SoPhongChuaKhaiBao { get; init; }
    public int HongQuaHan { get; init; }
    public int HongQuaHanNgay { get; init; }
    public List<CsvcTinhTrangDemDto> TheoTinhTrang { get; init; } = [];
}

public sealed class CsvcPhongCanChuYDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public int Tang { get; init; }
    public string TinhTrangPhong { get; init; } = "";
    public int SoThietBiHong { get; init; }
    public int SuCoDangMo { get; init; }
    public string ThietBi { get; init; } = "";
    public string HienThi => $"{TenPhong} (tầng {Tang}): {ThietBi}" + (SuCoDangMo > 0 ? $" · {SuCoDangMo} sự cố đang mở" : "");
}

public sealed class CsvcTongQuanDto
{
    public DateTime CapNhatLuc { get; init; }
    public CsvcTongHopDto TongHop { get; init; } = new();
    public List<CsvcPhongCanChuYDto> PhongCanChuY { get; init; } = [];
    public List<string> PhongChuaKhaiBao { get; init; } = [];
    public List<CsvcThietBiDto> ThietBis { get; init; } = [];
}

public sealed class CsvcLichSuDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string MoTaHanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {MoTaHanhDong}";
    public string ChiTietDoc => ChiTiet is null ? "" : ChiTiet.Replace("; ", "\n").Replace("->", "→");
}
