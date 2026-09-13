using KLCN060.Api.DTOs.Auth;

namespace KLCN060.Api.Services;

public interface IAuthService
{
    Task<RegisterResultDto> RegisterAsync(RegisterRequest request);
    Task<LoginResultDto> LoginAsync(LoginRequest request);
    Task<RefreshResultDto> RefreshAsync(RefreshRequest request);
    Task LogoutAsync(string tenDangNhap);
}
