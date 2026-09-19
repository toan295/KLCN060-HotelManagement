using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KLCN060.Api.Services;

/// <summary>Thông tin người gọi API, đọc từ JWT claims (Mục 5, CLAUDE.md).</summary>
public record CurrentUser(string TenDN, string? MaNV, string? MaKhach, bool LaKhachHang)
{
    public static CurrentUser From(ClaimsPrincipal user) => new(
        user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        user.FindFirstValue("maNV"),
        user.FindFirstValue("maKhach"),
        user.IsInRole("KHACH_HANG"));
}
