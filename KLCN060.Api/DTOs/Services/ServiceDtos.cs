using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Services;

public class ServiceRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột DichVu.TenDV
    public string TenDV { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaDV { get; set; }

    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột DichVu.DonViTinh
    public string DonViTinh { get; set; } = null!;

    /// <summary>Trạng thái phục vụ: null = giữ nguyên khi sửa (mặc định bật khi tạo mới); false = ngừng phục vụ.</summary>
    public bool? HoatDong { get; set; }

    /// <summary>Nhóm doanh thu của dịch vụ cho báo cáo kế toán: DICH_VU, MINIBAR, GIAT_LA hoặc KHAC. null = giữ nguyên khi sửa (mặc định DICH_VU khi tạo mới).</summary>
    public string? LoaiDoanhThu { get; set; }
}

public class ServiceDto
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaDV { get; set; }

    public string DonViTinh { get; set; } = null!;

    /// <summary>false = ngừng phục vụ (ẩn với khách/lễ tân, không ghi thêm cho khách được).</summary>
    public bool HoatDong { get; set; } = true;

    /// <summary>Nhóm doanh thu của dịch vụ: DICH_VU, MINIBAR, GIAT_LA, KHAC.</summary>
    public string LoaiDoanhThu { get; set; } = "DICH_VU";

    /// <summary>Các mục chi tiết đang hiển thị của dịch vụ, đã sắp theo ThuTu (mục bị ẩn chỉ quản lý xem được qua GET /services/{id}/details).</summary>
    public List<ServiceDetailDto> ChiTiets { get; set; } = new();
}

public class ServiceDetailRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột ChiTietDichVu.TieuDe
    public string TieuDe { get; set; } = null!;

    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột ChiTietDichVu.NoiDung
    public string NoiDung { get; set; } = null!;

    [Range(0, 1000, ErrorMessage = "{0} phải từ {1} đến {2}.")]
    public int ThuTu { get; set; }

    public bool ConHienThi { get; set; } = true;
}

public class ServiceDetailDto
{
    public string MaCTDV { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string NoiDung { get; set; } = null!;
    public int ThuTu { get; set; }
    public bool ConHienThi { get; set; }
}
