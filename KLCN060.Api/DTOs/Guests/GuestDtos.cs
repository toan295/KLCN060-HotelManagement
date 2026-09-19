using System.Text.Json;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Guests;

public class GuestRequest
{
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
}

/// <summary>Khách tự sửa hồ sơ: KHÔNG có CCCD (chỉ lễ tân nhập lúc check-in).</summary>
public class UpdateProfileRequest
{
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? Email { get; set; }
    public string? DiaChi { get; set; }

    /// <summary>Gom các field lạ để từ chối rõ ràng nếu client cố gửi CCCD (thay vì âm thầm bỏ qua).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? TruongKhac { get; set; }
}

public class GuestDto
{
    public string MaKhach { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
}

public class GuestStayHistoryDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTien { get; set; }
}

public class GuestDetailDto : GuestDto
{
    public List<GuestStayHistoryDto> LichSuLuuTru { get; set; } = new();
}
