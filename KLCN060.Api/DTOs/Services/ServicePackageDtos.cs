using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Services;

public class ServicePackageItemRequest
{
    [Required(ErrorMessage = "{0} là bắt buộc.")]
    public string MaDV { get; set; } = null!;

    [Range(1, 1000, ErrorMessage = "{0} phải từ {1} đến {2}.")]
    public int SoLuong { get; set; } = 1;
}

public class ServicePackageRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột GoiDichVu.TenGoi
    public string TenGoi { get; set; } = null!;

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột GoiDichVu.MoTa
    public string? MoTa { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaGoi { get; set; }

    /// <summary>null = giữ nguyên (khi sửa) / true (khi tạo mới).</summary>
    public bool? HoatDong { get; set; }

    public List<ServicePackageItemRequest> DichVus { get; set; } = new();
}

public class ServicePackageItemDto
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public bool DichVuConPhucVu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaDV { get; set; }

    public int SoLuong { get; set; }

    /// <summary>Phần giá gói được phân bổ cho dịch vụ này khi bán 1 gói (chia theo tỷ lệ giá lẻ, dòng cuối nhận phần làm tròn dư) - đúng như số tiền ghi vào hóa đơn.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhanBo { get; set; }
}

public class ServicePackageDto
{
    public string MaGoi { get; set; } = null!;
    public string TenGoi { get; set; } = null!;
    public string? MoTa { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaGoi { get; set; }

    /// <summary>Tổng giá nếu mua lẻ từng dịch vụ trong gói (theo giá dịch vụ hiện tại).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaLe { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TietKiem { get; set; }

    public bool HoatDong { get; set; }

    /// <summary>true khi gói đang bật và mọi dịch vụ trong gói còn phục vụ - chỉ khi đó mới ghi gói cho khách được.</summary>
    public bool CoTheBan { get; set; }

    /// <summary>Số lần gói đã được ghi cho khách (hóa đơn cũ giữ nguyên giá chốt lúc bán).</summary>
    public int SoLanDaBan { get; set; }
    public bool CoTheXoa { get; set; }
    public string? NguoiSuaCuoi { get; set; }
    public DateTime? ThoiGianSuaCuoi { get; set; }

    public List<ServicePackageItemDto> DichVus { get; set; } = new();
}
