using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Finance;
using KLCN060.Api.Services;
using KLCN060.Api.Services.Finance;
using KLCN060.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>
/// Đối soát chuyển khoản: tổng quan tiền về, danh sách thông báo ngân hàng truy ngược tới yêu cầu thanh toán / booking / hóa đơn, chi tiết một khoản
/// (kèm lịch sử xử lý) và xuất CSV. Xem cần XEM_DOI_SOAT; xử lý ngoại lệ dùng <c>POST /api/v1/reconciliation/bank-notifications/{id}/resolve</c>.
/// </summary>
[ApiController]
[Route("api/v1/reconciliation/transfers")]
[HasPermission(Permissions.XemDoiSoat)]
public class TransferReconciliationController : ControllerBase
{
    private readonly ITransferReconciliationService _service;

    public TransferReconciliationController(ITransferReconciliationService service) => _service = service;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<TransferReconOverviewDto>.Ok(await _service.GetOverviewAsync(tuNgay, denNgay)));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TransferReconListQuery query)
    {
        var (items, total) = await _service.ListAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        return Ok(ApiResponse<List<TransferReconRowDto>>.Ok(items, new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) }));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TransferReconListQuery query, [FromQuery] string? dinhDang = "csv")
    {
        var nguoiXuat = KLCN060.Api.Services.CurrentUser.From(User).TenDN;
        if (!string.Equals(dinhDang, "csv", StringComparison.OrdinalIgnoreCase) && !string.Equals(dinhDang, "xlsx", StringComparison.OrdinalIgnoreCase))
            throw new KLCN060.Api.Middlewares.ApiException(StatusCodes.Status400BadRequest, "DINH_DANG_KHONG_HOP_LE", "dinhDang chỉ nhận: csv, xlsx.", "dinhDang");
        if (string.Equals(dinhDang, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var (xl, tenXlsx) = await _service.ExportXlsxAsync(query, nguoiXuat);
            return File(xl, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", tenXlsx);
        }
        var (content, fileName) = await _service.ExportCsvAsync(query, nguoiXuat);
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("{maThongBao:int}")]
    public async Task<IActionResult> Detail(int maThongBao)
        => Ok(ApiResponse<TransferReconDetailDto>.Ok(await _service.GetDetailAsync(maThongBao)));
}
