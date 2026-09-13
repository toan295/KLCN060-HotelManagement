namespace KLCN060.Api.DTOs.Auth;

public class LoginRequest
{
    public string TenDangNhap { get; set; } = null!;
    public string MatKhau { get; set; } = null!;
}

public class LoginResultDto
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public int ExpiresInMinutes { get; set; }
}
