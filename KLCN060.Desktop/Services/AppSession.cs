using System.Text;
using System.Text.Json;

namespace KLCN060.Desktop.Services;

public sealed class AppSession
{
    public static AppSession Current { get; } = new();

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? VaiTro { get; private set; }
    public string? TenDangNhap { get; private set; }

    private AppSession()
    {
    }

    public void Start(string accessToken, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;

        using var payload = JsonDocument.Parse(DecodePayload(accessToken));
        TenDangNhap = ReadClaim(payload.RootElement, "sub");
        VaiTro = ReadClaim(payload.RootElement, "vaiTro");
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        VaiTro = null;
        TenDangNhap = null;
    }

    private static string DecodePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Access token không đúng định dạng JWT.");

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    }

    private static string? ReadClaim(JsonElement payload, string claimName) =>
        payload.TryGetProperty(claimName, out var claim) ? claim.GetString() : null;
}
