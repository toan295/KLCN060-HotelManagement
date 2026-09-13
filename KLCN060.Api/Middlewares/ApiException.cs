namespace KLCN060.Api.Middlewares;

/// <summary>Lỗi nghiệp vụ có chủ đích, được ExceptionHandlingMiddleware chuyển thành envelope lỗi chuẩn.</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public string? Field { get; }

    public ApiException(int statusCode, string code, string message, string? field = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Field = field;
    }
}
