using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Finance;
using KLCN060.Api.DTOs.Payments;
using KLCN060.Api.Services;
using KLCN060.Api.Services.Finance;
using KLCN060.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>
/// Xử lý thông báo tiền về bất thường của đối soát chuyển khoản (quyền XU_LY_DOI_SOAT): đã xử lý ngoài hệ thống hoặc bỏ qua, bắt buộc ghi chú,
/// mỗi khoản chỉ xử lý một lần. Xem danh sách / chi tiết nằm ở <see cref="TransferReconciliationController"/> (quyền XEM_DOI_SOAT).
/// </summary>
[ApiController]
[Route("api/v1/reconciliation")]
public class ReconciliationController : ControllerBase
{
    private readonly ITransferReconciliationService _service;

    public ReconciliationController(ITransferReconciliationService service) => _service = service;

    [HttpPost("bank-notifications/{maThongBao:int}/resolve")]
    [HasPermission(Permissions.XuLyDoiSoat)]
    public async Task<IActionResult> ResolveBankNotice(int maThongBao, [FromBody] ResolveBankNoticeRequest request)
        => Ok(ApiResponse<BankNoticeDto>.Ok(await _service.ResolveBankNoticeAsync(maThongBao, request, CurrentUser.From(User))));
}
