using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KLCN060.Web.Models.Common;

namespace KLCN060.Web.Services;

/// <summary>
/// Gọi KLCN060.Api qua HttpClient (từ Controller, phía server). Theo Mục 3 của tài liệu:
/// - ASP.NET Core MVC chạy phía server, JWT lưu ở Session (KHÔNG dùng localStorage của trình duyệt).
/// - BaseAddress đọc từ appsettings.json (Api:BaseUrl), không hard-code.
/// - Tự gắn header Authorization: Bearer {token} từ Session nếu có.
/// - Khi API trả 401 (token hết hạn) với request cần auth: gọi /auth/refresh 1 lần rồi thử lại request gốc;
///   nếu vẫn 401 thì xóa Session và ném ApiSessionExpiredException để SessionExpiredFilter chuyển hướng về W5.
/// </summary>
public class ApiClient
{
    private const string AccessTokenKey = "AccessToken";
    private const string RefreshTokenKey = "RefreshToken";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext HttpContext =>
        _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Không có HttpContext hiện tại.");

    public bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString(AccessTokenKey));

    public Task<ApiResult<TRes>> GetAsync<TRes>(string path, bool requireAuth = false)
        => SendAsync<TRes>(HttpMethod.Get, path, null, requireAuth);

    public Task<ApiResult<TRes>> PostAsync<TRes>(string path, object? body, bool requireAuth = false)
        => SendAsync<TRes>(HttpMethod.Post, path, body, requireAuth);

    public Task<ApiResult<TRes>> PutAsync<TRes>(string path, object? body, bool requireAuth = false)
        => SendAsync<TRes>(HttpMethod.Put, path, body, requireAuth);

    public void SaveTokens(string accessToken, string refreshToken)
    {
        HttpContext.Session.SetString(AccessTokenKey, accessToken);
        HttpContext.Session.SetString(RefreshTokenKey, refreshToken);
    }

    public void ClearSession()
    {
        HttpContext.Session.Remove(AccessTokenKey);
        HttpContext.Session.Remove(RefreshTokenKey);
    }

    private async Task<ApiResult<TRes>> SendAsync<TRes>(HttpMethod method, string path, object? body, bool requireAuth)
    {
        var response = await SendRawAsync(method, path, body);

        if (response.StatusCode == HttpStatusCode.Unauthorized && requireAuth)
        {
            var refreshed = await TryRefreshTokenAsync();
            if (refreshed)
                response = await SendRawAsync(method, path, body);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ClearSession();
                throw new ApiSessionExpiredException();
            }
        }

        return await ReadResultAsync<TRes>(response);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);

        var accessToken = HttpContext.Session.GetString(AccessTokenKey);
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _httpClient.SendAsync(request);
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        var refreshToken = HttpContext.Session.GetString(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", new { refreshToken }, JsonOptions);
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await ReadResultAsync<RefreshResultModel>(response);
        if (!result.Success || result.Data is null)
            return false;

        HttpContext.Session.SetString(AccessTokenKey, result.Data.AccessToken);
        return true;
    }

    private static async Task<ApiResult<TRes>> ReadResultAsync<TRes>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new ApiResult<TRes> { Success = true, StatusCode = (int)response.StatusCode };

            var envelope = JsonSerializer.Deserialize<ApiEnvelope<TRes>>(raw, JsonOptions);
            return new ApiResult<TRes>
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Data = envelope is null ? default : envelope.Data
            };
        }

        // Lỗi: đọc nguyên văn error.message trả về từ API (Mục 3: "hiển thị đúng nguyên văn error.message ...
        // không tự diễn giải lại bằng câu khác").
        var errorMessage = "Đã xảy ra lỗi không xác định.";
        string? errorCode = null;
        string? errorField = null;

        try
        {
            var errorEnvelope = JsonSerializer.Deserialize<ApiErrorEnvelope>(raw, JsonOptions);
            if (errorEnvelope?.Error is not null)
            {
                errorMessage = errorEnvelope.Error.Message;
                errorCode = errorEnvelope.Error.Code;
                errorField = errorEnvelope.Error.Field;
            }
        }
        catch (JsonException)
        {
            // API trả về nội dung không đúng envelope chuẩn (ví dụ lỗi hạ tầng) — vẫn giữ statusCode để Controller xử lý.
        }

        return new ApiResult<TRes>
        {
            Success = false,
            StatusCode = (int)response.StatusCode,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            ErrorField = errorField
        };
    }

    private class RefreshResultModel
    {
        public string AccessToken { get; set; } = null!;
        public int ExpiresInMinutes { get; set; }
    }
}

/// <summary>Token hết hạn và refresh cũng thất bại -> SessionExpiredFilter sẽ chuyển hướng về W5 (Đăng nhập).</summary>
public class ApiSessionExpiredException : Exception
{
}
