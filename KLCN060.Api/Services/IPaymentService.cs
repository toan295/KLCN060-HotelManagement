using KLCN060.Api.DTOs.Payments;

namespace KLCN060.Api.Services;

public interface IPaymentService
{
    Task<TransferDto> CreateTransferAsync(CreateTransferRequest request, CurrentUser user);
    Task<TransferDto> GetTransferAsync(string maGiaoDich, CurrentUser user);
    Task<TransferDto> CancelTransferAsync(string maGiaoDich, CurrentUser user);
    /// <summary>Chỉ khi Payment:DemoMode: giả lập tiền về cho một yêu cầu đang chờ (nút "Tôi đã chuyển khoản" của khách).</summary>
    Task<TransferDto> SimulatePaidAsync(string maGiaoDich, CurrentUser user);
    /// <summary>Các yêu cầu chuyển khoản đang chờ tiền về (chưa hết hạn) của một đối tượng (VD hóa đơn) - để lễ tân biết trước khi thu thêm.</summary>
    Task<List<TransferDto>> ListPendingForReferenceAsync(string loaiThamChieu, string maThamChieu);
    Task<(List<TransferDto> Items, int Total)> ListTransfersAsync(TransferListQuery query);
    Task<(List<BankNoticeDto> Items, int Total)> ListBankNoticesAsync(string? ketQua, int page, int pageSize, string? trangThaiXuLy = null);

    /// <summary>Điểm vào của webhook: ngân hàng báo "tiền về". Kiểm tra chữ ký, đối soát rồi ghi nhận thanh toán.</summary>
    Task<BankNotificationResultDto> HandleBankNotificationAsync(BankNotificationRequest request, string? signature);

    /// <summary>Webhook SePay: tiền VÀO tài khoản ngân hàng thật. Trả null nếu là giao dịch tiền ra (bỏ qua).</summary>
    Task<BankNotificationResultDto?> HandleSePayWebhookAsync(SePayWebhookRequest request, string? authorization);
}
