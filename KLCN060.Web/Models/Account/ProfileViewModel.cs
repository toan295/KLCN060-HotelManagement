using System.ComponentModel.DataAnnotations;

namespace KLCN060.Web.Models.Account;

/// <summary>
/// W11 - Hồ sơ cá nhân. KHÔNG có ô nhập CCCD (chỉ lễ tân nhập lúc check-in trực tiếp) -
/// CCCD chỉ hiển thị dạng chỉ đọc nếu API trả về (Mục 5B).
/// </summary>
public class ProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [Display(Name = "Họ tên")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Display(Name = "Số điện thoại")]
    public string SoDT { get; set; } = null!;

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    [Display(Name = "Địa chỉ")]
    public string? DiaChi { get; set; }

    /// <summary>Chỉ hiển thị (nếu có), không cho sửa trên form này.</summary>
    public string? CCCD { get; set; }
}

/// <summary>Khớp với GuestDto bên KLCN060.Api - dùng để đọc response GET/PUT /users/me.</summary>
public class ProfileApiModel
{
    public string MaKhach { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
}
