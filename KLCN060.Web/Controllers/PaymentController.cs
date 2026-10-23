using KLCN060.Web.Models.Payment;
using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Web.Controllers;

/// <summary>
/// Thanh toán đặt cọc bằng chuyển khoản vào tài khoản ngân hàng THẬT:
///   Start -> tạo yêu cầu + mã QR VietQR -> Transfer (hiện QR, đếm ngược, tự hỏi lại trạng thái) -> khách quét QR bằng ứng dụng
///   ngân hàng của mình và chuyển tiền -> ngân hàng báo tiền về qua SePay -> API đối soát và xác nhận cọc -> Transfer thấy THANH_CONG.
/// Mọi quyết định (đúng số tiền? còn hạn? phiếu còn chờ cọc?) đều do API đưa ra; Website chỉ hiển thị.
/// </summary>
public class PaymentController : Controller
{
    private readonly ApiClient _apiClient;

    public PaymentController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // POST /Payment/Start — W8 nút "Thanh toán đặt cọc bằng chuyển khoản"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        var result = await _apiClient.PostAsync<TransferViewModel>("api/v1/payments/transfers",
            new { loaiThamChieu = "DAT_COC", maThamChieu = maPhieuDat }, requireAuth: true);

        if (!result.Success || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không tạo được yêu cầu chuyển khoản.";
            return RedirectToAction("Confirmation", "Booking", new { maPhieuDat });
        }

        return RedirectToAction(nameof(Transfer), new { maGiaoDich = result.Data.MaGiaoDich });
    }

    // GET /Payment/Transfer?maGiaoDich=... — màn hình quét mã QR / thông tin chuyển khoản
    [HttpGet]
    public async Task<IActionResult> Transfer(string maGiaoDich)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Transfer), new { maGiaoDich }) });

        var result = await _apiClient.GetAsync<TransferViewModel>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}", requireAuth: true);
        if (!result.Success || result.Data is null)
            return NotFound();

        return View(result.Data);
    }

    // GET /Payment/Status?maGiaoDich=... — JavaScript trên màn hình QR gọi lặp lại để biết tiền đã về chưa
    [HttpGet]
    public async Task<IActionResult> Status(string maGiaoDich)
    {
        if (!_apiClient.IsLoggedIn)
            return Unauthorized();

        var result = await _apiClient.GetAsync<TransferViewModel>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}", requireAuth: true);
        if (!result.Success || result.Data is null)
            return StatusCode(result.StatusCode == 0 ? 502 : result.StatusCode);

        return Json(new { trangThai = result.Data.TrangThai, soGiayConLai = result.Data.SoGiayConLai });
    }

    // POST /Payment/SimulatePaid — chế độ thử nghiệm: nút "Tôi đã chuyển khoản" (API từ chối khi DemoMode tắt)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulatePaid(string maGiaoDich)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        var result = await _apiClient.PostAsync<TransferViewModel>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}/simulate-paid", null, requireAuth: true);
        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không ghi nhận được khoản chuyển khoản thử nghiệm.";

        return RedirectToAction(nameof(Transfer), new { maGiaoDich });
    }

    // POST /Payment/Cancel — hủy yêu cầu chuyển khoản đang chờ
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string maGiaoDich, string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        var result = await _apiClient.PostAsync<TransferViewModel>($"api/v1/payments/transfers/{Uri.EscapeDataString(maGiaoDich)}/cancel", null, requireAuth: true);
        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không hủy được giao dịch.";

        return RedirectToAction("Confirmation", "Booking", new { maPhieuDat });
    }
}
