using System.Text.Json;
using KLCN060.Api.DTOs.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            context.Items["MaLoiApi"] = ex.Code;
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;
            var body = ApiErrorResponse.Of(ex.Code, ex.Message, ex.Field);
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Hai yêu cầu đồng thời cùng tạo/sửa ra dữ liệu trùng: chỉ mục duy nhất của CSDL là chốt chặn cuối cùng.
            _logger.LogWarning(ex, "Vi phạm chỉ mục duy nhất khi xử lý request {Path}", context.Request.Path);
            context.Items["MaLoiApi"] = "DU_LIEU_DA_TON_TAI";
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            var body = ApiErrorResponse.Of("DU_LIEU_DA_TON_TAI", "Dữ liệu này vừa được người khác tạo hoặc đã tồn tại (trùng tên, số điện thoại hoặc số giấy tờ). Hãy tải lại và thử lại.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi xử lý request {Path}", context.Request.Path);
            context.Items["MaLoiApi"] = "LOI_HE_THONG";
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var body = ApiErrorResponse.Of("LOI_HE_THONG", "Đã xảy ra lỗi không xác định.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
