namespace KLCN060.Web.Models.Common;

/// <summary>Khớp với ApiResponse&lt;T&gt; bên KLCN060.Api (DTOs/Common/ApiResponse.cs).</summary>
public class ApiEnvelope<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public object? Meta { get; set; }
}

/// <summary>Khớp với ApiErrorResponse bên KLCN060.Api.</summary>
public class ApiErrorEnvelope
{
    public bool Success { get; set; }
    public ApiErrorBody? Error { get; set; }
}

public class ApiErrorBody
{
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Field { get; set; }
}

/// <summary>
/// Kết quả gọi API đã được ApiClient chuẩn hoá cho Controller sử dụng.
/// Khi Success = false, ErrorMessage LUÔN là nguyên văn error.message trả về từ API (Mục 3, Mục 6).
/// </summary>
public class ApiResult<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorField { get; set; }
}
