namespace KLCN060.Api.DTOs.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public T? Data { get; set; }
    public object? Meta { get; set; }

    public static ApiResponse<T> Ok(T data, object? meta = null) => new() { Success = true, Data = data, Meta = meta };
}

public class ApiErrorBody
{
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Field { get; set; }
}

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;
    public ApiErrorBody Error { get; set; } = null!;

    public static ApiErrorResponse Of(string code, string message, string? field = null) => new()
    {
        Success = false,
        Error = new ApiErrorBody { Code = code, Message = message, Field = field }
    };
}
