using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KLCN060.Desktop.Services;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };
    }

    public async Task<LoginResult> LoginAsync(string tenDangNhap, string matKhau, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/v1/auth/login", new LoginRequest(tenDangNhap, matKhau), JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(JsonOptions, cancellationToken)
            ?? throw new ApiRequestException(response.StatusCode, "API không trả dữ liệu đăng nhập.");

        if (!body.Success || body.Data is null)
            throw new ApiRequestException(response.StatusCode, "API không trả dữ liệu đăng nhập.");

        return body.Data;
    }

    private static async Task<ApiRequestException> CreateApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
        return new ApiRequestException(response.StatusCode, body?.Error?.Message ?? "Không thể kết nối tới máy chủ.");
    }
}

public sealed record LoginRequest(string TenDangNhap, string MatKhau);

public sealed class LoginResult
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresInMinutes { get; init; }
}

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
}

public sealed class ApiErrorResponse
{
    public ApiError? Error { get; init; }
}

public sealed class ApiError
{
    public string Message { get; init; } = string.Empty;
}

public sealed class ApiRequestException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiRequestException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
