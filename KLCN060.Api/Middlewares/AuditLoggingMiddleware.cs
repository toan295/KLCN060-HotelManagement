using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Middlewares;

/// <summary>
/// Tự động ghi NhatKyThaoTac (Mục 2, Nhóm 7 - CLAUDE.md) cho mọi request POST/PUT/PATCH/DELETE đã xác thực
/// và thành công (2xx), thay vì phải cài đặt riêng lẻ ở từng Controller/Service - đảm bảo mọi endpoint mutating
/// hiện tại và sau này (room-types, rooms, services, promotions, facilities...) đều tự động có audit trail.
/// Đặt trước UseAuthentication trong Program.cs để bọc toàn bộ pipeline: request.User chỉ có claims SAU khi
/// _next() chạy xong, đúng lúc middleware này đọc ở "chiều về".
/// Đăng ký/đăng nhập/đăng xuất có audit log riêng, chi tiết hơn, trong AuthService - middleware này bỏ qua nhóm auth.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    private static readonly string[] PhuongThucCanGhi = { "POST", "PUT", "PATCH", "DELETE" };

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, KLCN060DbContext db)
    {
        await _next(context);

        if (!NenGhiLog(context)) return;

        var tenDangNhap = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(tenDangNhap)) return;

        try
        {
            db.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaTaiKhoan = tenDangNhap,
                HanhDong = $"{context.Request.Method}_{context.Request.Path}",
                DoiTuongTacDong = context.Request.Path,
                ThoiGian = DateTime.Now,
                DiaChiIP = context.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Ghi audit la best-effort: khong duoc lam anh huong response da tra ve cho client.
            _logger.LogWarning(ex, "Khong ghi duoc audit log cho {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    private static bool NenGhiLog(HttpContext context)
    {
        if (context.Response.StatusCode is < 200 or >= 300) return false;
        if (!PhuongThucCanGhi.Contains(context.Request.Method)) return false;
        if (context.Request.Path.StartsWithSegments("/api/v1/auth")) return false;
        return true;
    }
}
