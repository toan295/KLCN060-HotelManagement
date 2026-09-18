using System.Text.Json;
using KLCN060.Api.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace KLCN060.Api.Middlewares;

/// <summary>
/// Mặc định, khi [Authorize]/[Authorize(Roles=...)] từ chối request, ASP.NET Core Authorization Middleware
/// tự ghi thẳng status code 401/403 với BODY RỖNG (không qua ExceptionHandlingMiddleware vì không phải exception).
/// Điều này phá vỡ quy ước response lỗi { success:false, error:{...} } ở Mục 4 và khiến client (Desktop) parse
/// JSON trên body rỗng bị lỗi. Handler này bọc lại để mọi trường hợp 401/403 đều trả đúng envelope chuẩn.
/// </summary>
public class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await GhiLoiAsync(context, StatusCodes.Status401Unauthorized, "CHUA_DANG_NHAP", "Bạn cần đăng nhập để thực hiện thao tác này.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await GhiLoiAsync(context, StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN", "Bạn không có quyền thực hiện thao tác này.");
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private static async Task GhiLoiAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var body = ApiErrorResponse.Of(code, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
