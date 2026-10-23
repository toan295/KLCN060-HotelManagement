using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Payments;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Thanh toán chuyển khoản qua tài khoản ngân hàng thật: tạo yêu cầu + mã QR VietQR, theo dõi trạng thái, đối soát, và webhook nhận tiền về từ ngân hàng.</summary>
[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>Tạo yêu cầu chuyển khoản (kèm nội dung chuyển khoản duy nhất và mã QR). Khách hàng: cọc phiếu của mình; nhân viên: cọc hoặc hóa đơn.</summary>
    [HttpPost("transfers")]
    [HasPermission(Permissions.ThanhToan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        var result = await _paymentService.CreateTransferAsync(request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<TransferDto>.Ok(result));
    }

    /// <summary>Xem trạng thái một yêu cầu chuyển khoản (client gọi lặp lại để biết tiền đã về chưa).</summary>
    [HttpGet("transfers/{maGiaoDich}")]
    [HasPermission(Permissions.ThanhToan, Permissions.XemDoiSoat, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> GetTransfer(string maGiaoDich)
        => Ok(ApiResponse<TransferDto>.Ok(await _paymentService.GetTransferAsync(maGiaoDich, CurrentUser.From(User))));

    /// <summary>Chế độ thử nghiệm: khách bấm "Tôi đã chuyển khoản" để giả lập tiền về (bị chặn khi DemoMode tắt).</summary>
    [HttpPost("transfers/{maGiaoDich}/simulate-paid")]
    [HasPermission(Permissions.ThanhToan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> SimulatePaid(string maGiaoDich)
        => Ok(ApiResponse<TransferDto>.Ok(await _paymentService.SimulatePaidAsync(maGiaoDich, CurrentUser.From(User))));

    [HttpPost("transfers/{maGiaoDich}/cancel")]
    [HasPermission(Permissions.ThanhToan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> CancelTransfer(string maGiaoDich)
        => Ok(ApiResponse<TransferDto>.Ok(await _paymentService.CancelTransferAsync(maGiaoDich, CurrentUser.From(User))));

    /// <summary>Yêu cầu chuyển khoản đang chờ tiền về của một hóa đơn/phiếu cọc (để thu tiền mặt không vô tình chồng lên QR đang treo).</summary>
    [HttpGet("transfers/pending")]
    [HasPermission(Permissions.ThanhToan)]
    public async Task<IActionResult> PendingTransfers([FromQuery] string maThamChieu, [FromQuery] string loaiThamChieu = "HOA_DON")
        => Ok(ApiResponse<List<TransferDto>>.Ok(await _paymentService.ListPendingForReferenceAsync(loaiThamChieu, maThamChieu)));

    /// <summary>Danh sách các yêu cầu chuyển khoản (đối soát).</summary>
    [HttpGet("transfers")]
    [HasPermission(Permissions.XemDoiSoat)]
    public async Task<IActionResult> ListTransfers([FromQuery] TransferListQuery query)
    {
        var (items, total) = await _paymentService.ListTransfersAsync(query);
        return Ok(ApiResponse<List<TransferDto>>.Ok(items, TaoMeta(query.Page, query.PageSize, total)));
    }

    /// <summary>Nhật ký thông báo tiền về từ ngân hàng, kể cả thông báo không khớp giao dịch nào (để kế toán đối soát).</summary>
    [HttpGet("bank-notifications")]
    [HasPermission(Permissions.XemDoiSoat)]
    public async Task<IActionResult> ListBankNotifications([FromQuery] string? ketQua, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? trangThaiXuLy = null)
    {
        var (items, total) = await _paymentService.ListBankNoticesAsync(ketQua, page, pageSize, trangThaiXuLy);
        return Ok(ApiResponse<List<BankNoticeDto>>.Ok(items, TaoMeta(page, pageSize, total)));
    }

    /// <summary>
    /// Webhook của SePay: khi tài khoản ngân hàng thật nhận tiền, SePay gọi vào đây (header Authorization: Apikey ...).
    /// Luôn trả {"success": true} khi đã tiếp nhận (kể cả giao dịch không khớp - đã vào hàng chờ đối soát) để SePay không gửi lại.
    /// </summary>
    [HttpPost("webhooks/sepay")]
    [AllowAnonymous]
    public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookRequest request, [FromHeader(Name = "Authorization")] string? authorization)
    {
        await _paymentService.HandleSePayWebhookAsync(request, authorization);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Webhook ký HMAC: bên gửi gọi vào đây khi có tiền về. Không dùng JWT (ngân hàng không phải người dùng của hệ thống)
    /// mà xác thực bằng chữ ký HMAC-SHA256 trong header X-Signature.
    /// </summary>
    [HttpPost("webhooks/bank")]
    [AllowAnonymous]
    public async Task<IActionResult> BankWebhook([FromBody] BankNotificationRequest request, [FromHeader(Name = "X-Signature")] string? signature)
        => Ok(ApiResponse<BankNotificationResultDto>.Ok(await _paymentService.HandleBankNotificationAsync(request, signature)));

    private static object TaoMeta(int page, int pageSize, int total)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }
}
