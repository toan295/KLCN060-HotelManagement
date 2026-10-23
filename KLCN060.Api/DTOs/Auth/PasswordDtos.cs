using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Auth;

public class ForgotPasswordRequest
{
    /// <summary>Email đã đăng ký cho tài khoản (chỉ dùng email để xác minh, không dùng số điện thoại).</summary>
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string Email { get; set; } = null!;

}

public class ForgotPasswordResultDto
{
    /// <summary>Thông báo đã gửi mã xác nhận tới email.</summary>
    public string ThongBao { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [StringLength(200, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string Token { get; set; } = null!;

    public string MatKhauMoi { get; set; } = null!;
}

public class VerifyResetCodeRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string Email { get; set; } = null!;

    /// <summary>Mã xác nhận 6 chữ số nhận qua email.</summary>
    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string Ma { get; set; } = null!;
}

public class VerifyResetCodeResultDto
{
    /// <summary>Mã đặt lại dùng một lần, gửi kèm mật khẩu mới ở bước cuối.</summary>
    public string Token { get; set; } = null!;
}

public class ChangePasswordRequest
{
    public string MatKhauCu { get; set; } = null!;
    public string MatKhauMoi { get; set; } = null!;
}
