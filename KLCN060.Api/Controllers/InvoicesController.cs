using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Services;
using KLCN060.Api.Services.EInvoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IEInvoiceService _eInvoiceService;

    public InvoicesController(IInvoiceService invoiceService, IEInvoiceService eInvoiceService)
    {
        _invoiceService = invoiceService;
        _eInvoiceService = eInvoiceService;
    }

    /// <summary>Danh sách hóa đơn (lọc theo trạng thái thanh toán, tìm theo mã/khách) để lễ tân, kế toán tìm hóa đơn cần thu tiền hoặc đối chiếu.</summary>
    // Quản lý/kế toán xem hóa đơn qua quyền báo cáo (chỉ xem, không thu tiền).
    [HttpGet]
    [HasPermission(Permissions.ThanhToan, Permissions.XemBaoCao)]
    public async Task<IActionResult> Search([FromQuery] InvoiceListQuery query)
    {
        var (items, total) = await _invoiceService.SearchAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<InvoiceSummaryDto>>.Ok(items, meta));
    }

    /// <summary>Số liệu tổng hợp (tổng tiền, đã thu, công nợ, quá hạn, chờ duyệt...) của TOÀN BỘ hóa đơn khớp bộ lọc.</summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.ThanhToan, Permissions.XemBaoCao)]
    public async Task<IActionResult> Stats([FromQuery] InvoiceListQuery query)
        => Ok(ApiResponse<InvoiceStatsDto>.Ok(await _invoiceService.GetStatsAsync(query)));

    /// <summary>Tổng quan một hóa đơn: của ai, booking/phòng nào, cơ cấu tiền, ai thu/khi nào, hoàn/đảo, yêu cầu duyệt, lịch sử, ai được làm gì.</summary>
    [HttpGet("{maHD}/overview")]
    [HasPermission(Permissions.ThanhToan, Permissions.XemBaoCao)]
    public async Task<IActionResult> Overview(string maHD)
        => Ok(ApiResponse<InvoiceOverviewDto>.Ok(await _invoiceService.GetOverviewAsync(maHD, CurrentUser.From(User))));

    [HttpGet("{maHD}")]
    [HasPermission(Permissions.ThanhToan, Permissions.XemBaoCao, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> GetById(string maHD)
    {
        var result = await _invoiceService.GetAsync(maHD, CurrentUser.From(User));
        return Ok(ApiResponse<InvoiceDto>.Ok(result));
    }

    [HttpPost("{maHD}/payments")]
    [HasPermission(Permissions.ThanhToan)]
    public async Task<IActionResult> AddPayment(string maHD, [FromBody] PaymentRequest request)
    {
        var result = await _invoiceService.AddPaymentAsync(maHD, request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<InvoiceDto>.Ok(result));
    }

    [HttpGet("{maHD}/export")]
    [HasPermission(Permissions.ThanhToan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> Export(string maHD)
    {
        var (content, fileName) = await _invoiceService.ExportPdfAsync(maHD, CurrentUser.From(User));
        return File(content, "application/pdf", fileName);
    }

    /// <summary>Hóa đơn điện tử (gửi nhà cung cấp HĐĐT/cơ quan thuế) của một hóa đơn. data = null nếu chưa lập.</summary>
    [HttpGet("{maHD}/e-invoice")]
    [HasPermission(Permissions.ThanhToan, Permissions.XemBaoCao)]
    public async Task<IActionResult> GetEInvoice(string maHD)
        => Ok(ApiResponse<EInvoiceDto?>.Ok(await _eInvoiceService.GetAsync(maHD)));

    /// <summary>Lưu thông tin người mua và phát hành hóa đơn điện tử (chỉ khi hóa đơn đã thanh toán đủ). Dùng lại để sửa thông tin khi trạng thái CHO_PHAT_HANH/LOI.</summary>
    [HttpPost("{maHD}/e-invoice")]
    [HasPermission(Permissions.ThanhToan)]
    public async Task<IActionResult> IssueEInvoice(string maHD, [FromBody] IssueEInvoiceRequest request)
        => Ok(ApiResponse<EInvoiceDto>.Ok(await _eInvoiceService.IssueAsync(maHD, request, CurrentUser.From(User))));

    /// <summary>Gửi lại hóa đơn điện tử đang chờ phát hành/lỗi bằng thông tin đã lưu.</summary>
    [HttpPost("{maHD}/e-invoice/retry")]
    [HasPermission(Permissions.ThanhToan)]
    public async Task<IActionResult> RetryEInvoice(string maHD)
        => Ok(ApiResponse<EInvoiceDto>.Ok(await _eInvoiceService.RetryAsync(maHD, CurrentUser.From(User))));
}
