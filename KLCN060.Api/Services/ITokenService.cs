using KLCN060.Domain;

namespace KLCN060.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(TaiKhoan taiKhoan, string vaiTro);
    string GenerateRefreshToken();
    int AccessTokenExpiryMinutes { get; }
    int RefreshTokenExpiryDays { get; }
}
