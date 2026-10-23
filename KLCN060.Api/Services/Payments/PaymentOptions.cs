namespace KLCN060.Api.Services.Payments;

/// <summary>Cấu hình thanh toán chuyển khoản qua tài khoản ngân hàng THẬT của khách sạn (mục "Payment" trong appsettings.json / user-secrets).</summary>
public class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>Tài khoản ngân hàng THẬT nhận tiền của khách sạn (hiển thị trên mã QR VietQR / màn hình thanh toán).</summary>
    public BankAccountOptions Bank { get; set; } = new();

    /// <summary>
    /// Chế độ THỬ NGHIỆM (chỉ bật trong appsettings.Development.json): chưa có tài khoản ngân hàng thật vẫn tạo được yêu cầu chuyển khoản với tài khoản MẪU,
    /// và khách bấm "Tôi đã chuyển khoản" để giả lập tiền về (đi qua đúng luồng đối soát như thông báo ngân hàng thật, mã giao dịch bắt đầu bằng DEMO-).
    /// Môi trường thật PHẢI để false.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Số phút một yêu cầu chuyển khoản còn hiệu lực trước khi hết hạn.</summary>
    public int TransferExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Khóa bí mật dùng chung giữa "ngân hàng" và hệ thống để ký/kiểm tra chữ ký HMAC-SHA256 của webhook.
    /// Giá trị trong appsettings.json chỉ dành cho môi trường phát triển/demo; môi trường thật phải đặt qua
    /// user-secrets hoặc biến môi trường (Payment__WebhookSecret).
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Cổng SePay (sepay.vn) theo dõi biến động số dư tài khoản thật và gọi webhook về hệ thống khi có tiền vào.</summary>
    public SePayOptions SePay { get; set; } = new();

    /// <summary>
    /// true khi đã khai báo tài khoản nhận THẬT (có BIN 6 số, số tài khoản, tên chủ tài khoản). Chưa khai báo thì không được sinh mã QR
    /// để tránh khách chuyển tiền vào một tài khoản không phải của khách sạn.
    /// </summary>
    public bool BankDaCauHinh =>
        System.Text.RegularExpressions.Regex.IsMatch(Bank.BankBin ?? string.Empty, @"^\d{6}$")
        && System.Text.RegularExpressions.Regex.IsMatch(Bank.AccountNumber ?? string.Empty, @"^\d{6,19}$")
        && !string.IsNullOrWhiteSpace(Bank.AccountName);
}

public static class PaymentOptionsExtensions
{
    /// <summary>Tài khoản mẫu chỉ dùng khi DemoMode bật mà chưa khai báo tài khoản thật - KHÔNG phải tài khoản của khách sạn.</summary>
    private static readonly BankAccountOptions TaiKhoanMau = new() { BankName = "Ngân hàng mẫu (THỬ NGHIỆM - đừng chuyển tiền thật)", BankBin = "970436", AccountNumber = "0000000000", AccountName = "KLCN060 HOTEL TEST" };

    /// <summary>Tài khoản dùng để hiển thị / dựng QR / đối soát: tài khoản thật nếu đã khai báo, không thì (chỉ khi DemoMode) tài khoản mẫu.</summary>
    public static BankAccountOptions BankHieuLuc(this PaymentOptions o) => o.BankDaCauHinh || !o.DemoMode ? o.Bank : TaiKhoanMau;
}

public class BankAccountOptions
{
    public string BankName { get; set; } = string.Empty;
    /// <summary>Mã BIN 6 số của ngân hàng theo chuẩn NAPAS (dùng dựng mã QR VietQR).</summary>
    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
}

public class SePayOptions
{
    /// <summary>Khóa SePay gửi trong header "Authorization: Apikey {khóa}". Đặt qua user-secrets/biến môi trường (Payment__SePay__ApiKey); để trống = từ chối mọi webhook SePay.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
