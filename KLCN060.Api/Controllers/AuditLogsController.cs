using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Audit;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Nhật ký hệ thống: Quản lý (quyền XEM_NHAT_KY) xem ai đã làm gì, lúc nào, kết quả ra sao. Chỉ đọc.</summary>
[ApiController]
[Route("api/v1/audit-logs")]
[HasPermission(Permissions.XemNhatKy)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AuditLogQuery query)
    {
        var (items, total) = await _auditLogService.SearchAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<AuditLogDto>>.Ok(items, meta));
    }
}
