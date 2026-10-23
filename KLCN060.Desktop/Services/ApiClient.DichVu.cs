using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Màn Quản lý dịch vụ (D13): danh mục kèm thống kê/ràng buộc và lịch sử thay đổi.
public sealed partial class ApiClient
{
    public Task<DichVuCatalogDto> GetDichVuCatalogAsync() => GetAsync<DichVuCatalogDto>("api/v1/services/catalog");
    public Task<List<LichSuThayDoiDichVuDto>> GetLichSuDichVuAsync(string maDV) => GetAsync<List<LichSuThayDoiDichVuDto>>($"api/v1/services/{Uri.EscapeDataString(maDV)}/history");
    public Task<List<LichSuThayDoiDichVuDto>> GetLichSuGoiDichVuAsync(string maGoi) => GetAsync<List<LichSuThayDoiDichVuDto>>($"api/v1/service-packages/{Uri.EscapeDataString(maGoi)}/history");
}

public sealed class DichVuQuanLyDto
{
    public string MaDV { get; init; } = "";
    public string TenDV { get; init; } = "";
    public decimal GiaDV { get; init; }
    public string DonViTinh { get; init; } = "";
    public bool HoatDong { get; init; } = true;
    public string LoaiDoanhThu { get; init; } = "DICH_VU";
    public int SoLuotDung { get; init; }
    public int TongSoLuong { get; init; }
    public decimal DoanhThu { get; init; }
    public DateTime? LanDungCuoi { get; init; }
    public int SoMucChiTiet { get; init; }
    public int SoMucChiTietHienThi { get; init; }
    public List<string> CacGoiChua { get; init; } = [];
    public bool DangGanMinibar { get; init; }
    public bool CoTheXoa { get; init; }
    public string? LyDoKhongXoa { get; init; }
    public string? NguoiSuaCuoi { get; init; }
    public DateTime? ThoiGianSuaCuoi { get; init; }

    public string NhomHienThi => LoaiDoanhThu switch { "MINIBAR" => "Minibar", "GIAT_LA" => "Giặt là", "KHAC" => "Khác", _ => "Dịch vụ" };
    public string TrangThaiHienThi => HoatDong ? "Đang phục vụ" : "Ngừng phục vụ";
    public string GiaHienThi => $"{GiaDV:N0}đ / {DonViTinh}";
    public string DungHienThi => SoLuotDung == 0 ? "Chưa từng ghi cho khách" : $"Đã ghi {SoLuotDung} lần ({TongSoLuong} {DonViTinh}) · {DoanhThu:N0}đ" + (LanDungCuoi is { } d ? $" · gần nhất {d:dd/MM/yyyy}" : "");
    public string GoiHienThi => CacGoiChua.Count == 0 ? "Không nằm trong gói nào" : "Trong gói: " + string.Join(", ", CacGoiChua);
    public string ChiTietHienThi => SoMucChiTiet == 0 ? "Chưa có mô tả chi tiết cho khách" : $"{SoMucChiTietHienThi}/{SoMucChiTiet} mục chi tiết đang hiển thị cho khách";
    public string SuaCuoiHienThi => NguoiSuaCuoi is null ? "" : $"Thay đổi gần nhất: {NguoiSuaCuoi} lúc {ThoiGianSuaCuoi:dd/MM/yyyy HH:mm}";
}

public sealed class DichVuTongHopDto
{
    public int TongDichVu { get; init; }
    public int DangPhucVu { get; init; }
    public int NgungPhucVu { get; init; }
    public int DichVuChuaCoChiTiet { get; init; }
    public int DichVuChuaTungDung { get; init; }
    public int TongGoi { get; init; }
    public int GoiDangBan { get; init; }
    public int GoiKhongBanDuoc { get; init; }
    public decimal DoanhThuDichVu { get; init; }
}

public sealed class DichVuCatalogDto
{
    public DateTime CapNhatLuc { get; init; }
    public DichVuTongHopDto TongHop { get; init; } = new();
    public List<DichVuQuanLyDto> DichVus { get; init; } = [];
}

public sealed class LichSuThayDoiDichVuDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string MoTaHanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {MoTaHanhDong}";
    public string ChiTietDoc => ChiTiet is null ? "" : ChiTiet.Replace("; ", "\n").Replace("->", "→");
}
