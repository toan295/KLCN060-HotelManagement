using System.ComponentModel.DataAnnotations;

namespace KLCN060.Web.Models.Account;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email đã đăng ký.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100, ErrorMessage = "Tối đa 100 ký tự.")]
    [Display(Name = "Email đã đăng ký")]
    public string Email { get; set; } = null!;
}

public class VerifyCodeViewModel
{
    [Required]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận.")]
    [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "Mã xác nhận gồm 6 chữ số.")]
    [Display(Name = "Mã xác nhận")]
    public string Ma { get; set; } = null!;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [MaxLength(64, ErrorMessage = "Mật khẩu tối đa 64 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string MatKhauMoi { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string XacNhanMatKhau { get; set; } = null!;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string MatKhauCu { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [MaxLength(64, ErrorMessage = "Mật khẩu tối đa 64 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string MatKhauMoi { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
    [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string XacNhanMatKhau { get; set; } = null!;
}

/// <summary>Khớp ForgotPasswordResultDto bên API.</summary>
public class ForgotPasswordResultModel
{
    public string ThongBao { get; set; } = "";
}

/// <summary>Khớp VerifyResetCodeResultDto bên API.</summary>
public class VerifyResetCodeResultModel
{
    public string Token { get; set; } = "";
}

/// <summary>Khớp RegisterResultDto bên API.</summary>
public class RegisterResultModel
{
    public string MaKhach { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
}
