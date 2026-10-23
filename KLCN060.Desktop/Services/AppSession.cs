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
    public string? HoTen { get; private set; }

    // Quyền HIỆN TẠI của vai trò đang đăng nhập (lấy từ API sau khi đăng nhập, làm mới khi bị từ chối quyền) - quyết định menu hiện ra.
    // Quyền thật luôn do API kiểm tra ở từng request; đây chỉ để ẩn/hiện chức năng cho khớp.
    private HashSet<string> _quyen = new();
    public bool CoQuyen(string tenQuyen) => _quyen.Contains(tenQuyen);
    public void DatQuyen(IEnumerable<string> cacQuyen) => _quyen = new HashSet<string>(cacQuyen);

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
        HoTen = ReadClaim(payload.RootElement, "hoTen");
    }

    /// <summary>Cập nhật access token mới sau khi làm mới thành công (Mục 3: giữ trong bộ nhớ, không ghi xuống đĩa).</summary>
    public void CapNhatAccessToken(string accessToken) => AccessToken = accessToken;

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        VaiTro = null;
        TenDangNhap = null;
        HoTen = null;
        _quyen = new HashSet<string>();
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
