namespace KLCN060.Api.DTOs.Auth;

public class RegisterRequest
{
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? Email { get; set; }
    public string MatKhau { get; set; } = null!;
}

public class RegisterResultDto
{
    public string MaKhach { get; set; } = null!;
    public string TenDangNhap { get; set; } = null!;
}
