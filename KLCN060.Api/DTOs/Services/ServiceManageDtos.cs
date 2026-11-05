using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Services;

/// <summary>Một dịch vụ trong màn Quản lý dịch vụ: thông tin danh mục + thống kê sử dụng + các ràng buộc cho biết thao tác nào còn được phép.</summary>
public class ServiceCatalogItemDto
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal GiaDV { get; set; }
    public string DonViTinh { get; set; } = null!;
    public bool HoatDong { get; set; }
    public string LoaiDoanhThu { get; set; } = "DICH_VU";

    /// <summary>Số dòng sử dụng đã ghi cho khách / tổng số lượng / doanh thu đã ghi (theo giá chốt lúc bán).</summary>
    public int SoLuotDung { get; set; }
    public int TongSoLuong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }
    public DateTime? LanDungCuoi { get; set; }

    public int SoMucChiTiet { get; set; }
    public int SoMucChiTietHienThi { get; set; }

    public bool CoTheXoa { get; set; }
    public string? LyDoKhongXoa { get; set; }

    public string? NguoiSuaCuoi { get; set; }
    public DateTime? ThoiGianSuaCuoi { get; set; }
}

public class ServiceCatalogSummaryDto
{
    public int TongDichVu { get; set; }
    public int DangPhucVu { get; set; }
    public int NgungPhucVu { get; set; }
    public int DichVuChuaCoChiTiet { get; set; }
    public int DichVuChuaTungDung { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuDichVu { get; set; }
}

public class ServiceCatalogDto
{
    public DateTime CapNhatLuc { get; set; }
    public ServiceCatalogSummaryDto TongHop { get; set; } = new();
    public List<ServiceCatalogItemDto> DichVus { get; set; } = new();
}

/// <summary>Một dòng lịch sử thay đổi của dịch vụ/gói (lấy từ nhật ký thao tác): ai, lúc nào, làm gì, chi tiết trước → sau.</summary>
public class ServiceHistoryEntryDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string MoTaHanhDong { get; set; } = "";
    public string? ChiTiet { get; set; }
}
