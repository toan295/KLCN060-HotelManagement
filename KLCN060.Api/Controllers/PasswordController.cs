using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KLCN060.Api.DTOs.Auth;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KLCN060.Api.Controllers;

/// <summary>Quên / đặt lại / đổi mật khẩu.</summary>
[ApiController]
[Route("api/v1/auth")]
public class PasswordController : ControllerBase
{
    /// <summary>Tên chính sách giới hạn tần suất theo IP (đăng ký trong Program.cs).</summary>
    public const string RateLimitPolicy = "forgot-password";

    private readonly IPasswordService _passwordService;

    public PasswordController(IPasswordService passwordService)
    {
        _passwordService = passwordService;
    }

    /// <summary>Bước 1: gửi MÃ XÁC NHẬN 6 số tới EMAIL đã đăng ký. Email chưa đăng ký thì trả 404 EMAIL_CHUA_DANG_KY.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _passwordService.ForgotPasswordAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<ForgotPasswordResultDto>.Ok(result));
    }

    /// <summary>Bước 2: nhập mã xác nhận. Đúng thì trả mã đặt lại (dùng một lần) để sang bước đổi mật khẩu; sai thì 400 MA_XAC_NHAN_SAI.</summary>
    [HttpPost("forgot-password/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest request)
        => Ok(ApiResponse<VerifyResetCodeResultDto>.Ok(await _passwordService.XacNhanMaAsync(request)));

    /// <summary>Bước 3: đặt mật khẩu mới bằng mã đặt lại nhận được ở bước 2.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _passwordService.ResetPasswordAsync(request);
        return Ok(new ApiResponse<object?> { Success = true, Data = null });
    }

    /// <summary>Đổi mật khẩu khi đã đăng nhập (mọi vai trò). Thành công thì các phiên đăng nhập cũ bị thu hồi.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var tenDangNhap = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _passwordService.ChangePasswordAsync(tenDangNhap, request);
        return Ok(new ApiResponse<object?> { Success = true, Data = null });
    }
}
