using System.ComponentModel.DataAnnotations;

namespace KLCN060.Web.Models.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [Display(Name = "Tên đăng nhập (SĐT/Email)")]
    public string TenDangNhap { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = null!;

    public string? ReturnUrl { get; set; }
}
