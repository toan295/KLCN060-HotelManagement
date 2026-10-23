using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KLCN060.Api.Auditing;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Middlewares;

/// <summary>
/// Tự động ghi NhatKyThaoTac (Mục 2, Nhóm 7 - tài liệu đặc tả) cho mọi request POST/PUT/PATCH/DELETE đã xác thực
/// và thành công (2xx), thay vì phải cài đặt riêng lẻ ở từng Controller/Service - đảm bảo mọi endpoint mutating
/// hiện tại và sau này (room-types, rooms, services, promotions, facilities...) đều tự động có audit trail.
/// Đặt trước UseAuthentication trong Program.cs để bọc toàn bộ pipeline: request.User chỉ có claims SAU khi
/// _next() chạy xong, đúng lúc middleware này đọc ở "chiều về".
/// Đăng ký/đăng nhập/đăng xuất có audit log riêng, chi tiết hơn, trong AuthService - middleware này bỏ qua nhóm auth.
/// Thao tác BỊ TỪ CHỐI (403 - không đủ quyền) hoặc THẤT BẠI (400/409/5xx...) của người đã đăng nhập cũng được ghi, kèm kết quả và mã lỗi:
/// để điều tra được "ai đã thử làm gì mà không được", không chỉ những lần thành công.
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

    public async Task InvokeAsync(HttpContext context, KLCN060DbContext db, IAuditTrail audit)
    {
        // ExceptionHandlingMiddleware nằm NGOÀI middleware này nên lỗi nghiệp vụ (ApiException) bị ném xuyên qua đây
        // trước khi có status code: bắt lại để xác định mã/mã lỗi, ghi nhật ký rồi ném tiếp cho lớp ngoài trả response.
        Exception? loi = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            loi = ex;
            var (status, ma) = ex switch
            {
                ApiException a => (a.StatusCode, a.Code),
                DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "DU_LIEU_DA_TON_TAI"),
                _ => (StatusCodes.Status500InternalServerError, "LOI_HE_THONG")
            };
            context.Items["MaLoiApi"] = ma;
            context.Response.StatusCode = status;
        }

        try { await GhiAsync(context, db, audit); }
        finally
        {
            if (loi is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(loi).Throw();
        }
    }

    private async Task GhiAsync(HttpContext context, KLCN060DbContext db, IAuditTrail audit)
    {
        var thanhCong = context.Response.StatusCode is >= 200 and < 300;
        if (!NenGhiLog(context)) return;
        // Nghiệp vụ đã ghi dòng nhật ký riêng (chi tiết hơn) trong cùng transaction: không ghi thêm dòng chung "POST_/api/...".
        if (thanhCong && audit.DaGhi) return;

        var tenDangNhap = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(tenDangNhap)) return;

        try
        {
            string? maLoi = null;
            if (!thanhCong)
            {
                // Request thất bại: bỏ các dòng nhật ký "thành công" đang chờ (nghiệp vụ không hoàn tất) và các thay đổi chưa lưu của DbContext.
                (audit as AuditTrail)?.LayHangCho();
                db.ChangeTracker.Clear();
                maLoi = context.Items.TryGetValue("MaLoiApi", out var m) ? m as string : null;
            }
            var status = context.Response.StatusCode;
            db.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaTaiKhoan = tenDangNhap,
                HanhDong = $"{context.Request.Method}_{context.Request.Path}",
                DoiTuongTacDong = context.Request.Path.Value is { Length: > 100 } p ? p[..100] : context.Request.Path.Value,
                ThoiGian = DateTime.Now,
                DiaChiIP = context.Connection.RemoteIpAddress?.ToString(),
                KetQua = thanhCong ? "THANH_CONG" : status == StatusCodes.Status403Forbidden ? "TU_CHOI" : "THAT_BAI",
                MaLoi = thanhCong ? null : (maLoi ?? $"HTTP_{status}"),
                ChiTiet = thanhCong ? null : $"httpStatus={status}; maLoi={maLoi ?? "?"}"
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
        var status = context.Response.StatusCode;
        if (context.Request.Path.StartsWithSegments("/api/v1/auth")) return false;
        // Không đủ quyền: ghi cả GET (cố xem dữ liệu không được phép).
        if (status == StatusCodes.Status403Forbidden) return context.User.Identity?.IsAuthenticated == true;
        if (!PhuongThucCanGhi.Contains(context.Request.Method)) return false;
        if (status is >= 200 and < 300) return true;
        // Thất bại của thao tác ghi: bỏ qua 401 (chưa đăng nhập - không biết là ai), 404/405 (không có endpoint) và chuyển hướng.
        // 404 do nghiệp vụ (có mã lỗi, vd không tìm thấy phòng đích) vẫn ghi; 404/405 do không có endpoint (không mã lỗi) thì bỏ qua.
        if (status == StatusCodes.Status404NotFound && context.Items.ContainsKey("MaLoiApi")) return true;
        return status is >= 400 and not 401 and not 404 and not 405;
    }
}
