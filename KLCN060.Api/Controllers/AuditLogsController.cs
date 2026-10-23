using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Audit;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>
/// Nhật ký hoạt động: toàn hệ thống với XEM_NHAT_KY (Quản lý); chỉ các nghiệp vụ tài chính (thanh toán, hóa đơn, đối soát chuyển khoản)
/// với XEM_NHAT_KY_TAI_CHINH (Kế toán).
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[HasPermission(Permissions.XemNhatKy, Permissions.XemNhatKyTaiChinh)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly IPermissionService _permissions;

    public AuditLogsController(IAuditLogService auditLogService, IPermissionService permissions)
    {
        _auditLogService = auditLogService;
        _permissions = permissions;
    }

    private async Task<bool> ChiTaiChinhAsync() => !await _permissions.CoQuyenAsync(CurrentUser.From(User).TenDN, Permissions.XemNhatKy);

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AuditLogQuery query)
    {
        var (items, total) = await _auditLogService.SearchAsync(query, await ChiTaiChinhAsync());
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<AuditLogDto>>.Ok(items, meta));
    }

    /// <summary>Mọi thứ liên quan tới một dòng nhật ký: chuỗi sự kiện của cùng đối tượng, lịch sử tình trạng phòng.</summary>
    [HttpGet("{maNhatKy:long}/related")]
    public async Task<IActionResult> Related(long maNhatKy)
    {
        var kq = await _auditLogService.GetRelatedAsync(maNhatKy, await ChiTaiChinhAsync())
            ?? throw new Middlewares.ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAT_KY", "Không tìm thấy dòng nhật ký (hoặc bạn không có quyền xem).");
        return Ok(ApiResponse<AuditRelatedDto>.Ok(kq));
    }

    /// <summary>Xuất nhật ký ra CSV (mặc định) hoặc Excel (dinhDang=xlsx) theo bộ lọc hiện tại. Cần quyền riêng XUAT_NHAT_KY; việc xuất được ghi lại trong nhật ký.</summary>
    [HttpGet("export")]
    [HasPermission(Permissions.XuatNhatKy)]
    public async Task<IActionResult> Export([FromQuery] AuditLogQuery query, [FromQuery] string? dinhDang = "csv")
    {
        var dd = dinhDang?.Trim() ?? "";
        if (dd.Length > 0 && !dd.Equals("csv", StringComparison.OrdinalIgnoreCase) && !dd.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            throw new Middlewares.ApiException(StatusCodes.Status400BadRequest, "DINH_DANG_KHONG_HOP_LE", "DinhDang chỉ chấp nhận: csv, xlsx.", "dinhDang");
        var xlsx = dd.Equals("xlsx", StringComparison.OrdinalIgnoreCase);
        var (noi, soDong, biCat) = await _auditLogService.XuatCsvAsync(query, CurrentUser.From(User).TenDN, await ChiTaiChinhAsync(), xlsx);
        Response.Headers["X-So-Dong"] = soDong.ToString();
        Response.Headers["X-Bi-Cat"] = biCat ? "true" : "false";
        if (xlsx) return File(noi, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", TenTepXuat.Tao("NhatKyHoatDong", "xlsx"));
        return File(noi, "text/csv; charset=utf-8", TenTepXuat.Tao("NhatKyHoatDong", "csv"));
    }

    /// <summary>Kiểm tra chữ ký từng dòng nhật ký để phát hiện dòng bị sửa trực tiếp trong CSDL (chỉ người xem toàn bộ nhật ký).</summary>
    [HttpGet("integrity")]
    [HasPermission(Permissions.XemNhatKy)]
    public async Task<IActionResult> Integrity([FromQuery] DateOnly? tuNgay)
        => Ok(ApiResponse<AuditIntegrityDto>.Ok(await _auditLogService.KiemTraToanVenAsync(tuNgay)));

    /// <summary>Danh sách nhóm nghiệp vụ dùng để lọc.</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> Groups() => Ok(ApiResponse<List<AuditGroupDto>>.Ok(_auditLogService.GetGroups(await ChiTaiChinhAsync())));
}
