namespace KLCN060.Api.DTOs.Auth;

public class RefreshRequest
{
    public string RefreshToken { get; set; } = null!;
}

public class RefreshResultDto
{
    public string AccessToken { get; set; } = null!;
    public int ExpiresInMinutes { get; set; }
}
