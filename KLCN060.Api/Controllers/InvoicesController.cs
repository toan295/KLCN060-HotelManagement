using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet("{maHD}")]
    [Authorize(Roles = "LE_TAN,KE_TOAN,KHACH_HANG")]
    public async Task<IActionResult> GetById(string maHD)
    {
        var result = await _invoiceService.GetAsync(maHD, CurrentUser.From(User));
        return Ok(ApiResponse<InvoiceDto>.Ok(result));
    }

    [HttpPost("{maHD}/payments")]
    [Authorize(Roles = "LE_TAN,KE_TOAN")]
    public async Task<IActionResult> AddPayment(string maHD, [FromBody] PaymentRequest request)
    {
        var result = await _invoiceService.AddPaymentAsync(maHD, request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<InvoiceDto>.Ok(result));
    }

    [HttpGet("{maHD}/export")]
    [Authorize(Roles = "LE_TAN,KE_TOAN,KHACH_HANG")]
    public async Task<IActionResult> Export(string maHD)
    {
        var (content, fileName) = await _invoiceService.ExportPdfAsync(maHD, CurrentUser.From(User));
        return File(content, "application/pdf", fileName);
    }
}
