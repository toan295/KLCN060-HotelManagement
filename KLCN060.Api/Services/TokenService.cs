using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KLCN060.Domain;
using Microsoft.IdentityModel.Tokens;

namespace KLCN060.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public int AccessTokenExpiryMinutes => _configuration.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 30);
    public int RefreshTokenExpiryDays => _configuration.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);

    public string GenerateAccessToken(TaiKhoan taiKhoan, string vaiTro)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key (User Secrets).");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, taiKhoan.TenDN),
            new("loaiTaiKhoan", taiKhoan.LoaiTaiKhoan.ToString()),
            new(ClaimTypes.Role, vaiTro),
            new("vaiTro", vaiTro),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(taiKhoan.MaNV))
            claims.Add(new Claim("maNV", taiKhoan.MaNV));
        if (!string.IsNullOrEmpty(taiKhoan.MaKhach))
            claims.Add(new Claim("maKhach", taiKhoan.MaKhach));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
