namespace KLCN060.Api.Services.Auth;

/// <summary>Cấu hình quên/đặt lại mật khẩu (mục "Auth" trong appsettings.json).</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Số phút mã xác nhận (6 số gửi qua email) còn hiệu lực.</summary>
    public int ResetCodeMinutes { get; set; } = 10;

    /// <summary>Số lần tối đa được nhập mã xác nhận cho một yêu cầu; sai quá số này phải yêu cầu mã mới.</summary>
    public int MaxCodeAttempts { get; set; } = 5;

    /// <summary>Số phút được phép đặt mật khẩu mới sau khi đã nhập đúng mã xác nhận.</summary>
    public int ResetTokenMinutes { get; set; } = 30;

    /// <summary>Tối đa bao nhiêu yêu cầu "quên mật khẩu" cho MỘT tài khoản trong 1 giờ (chống spam hộp thư của chủ tài khoản).</summary>
    public int MaxResetRequestsPerHour { get; set; } = 5;

    /// <summary>Tối đa bao nhiêu yêu cầu "quên mật khẩu" (gửi mã + nhập mã) từ MỘT địa chỉ IP trong một cửa sổ thời gian (chống spam nhiều hộp thư khác nhau).</summary>
    public int ForgotPasswordMaxPerIp { get; set; } = 15;

    /// <summary>Độ dài (phút) cửa sổ đếm của giới hạn theo IP ở trên.</summary>
    public int ForgotPasswordWindowMinutes { get; set; } = 15;

    /// <summary>
    /// Địa chỉ IP của các máy chủ trung gian (Website/reverse proxy) được tin cậy khi đọc header X-Forwarded-For để biết IP thật của người dùng.
    /// Địa chỉ loopback (127.0.0.1, ::1) luôn được tin cậy (Website và API chạy cùng máy). Khi Website chạy máy khác, thêm IP của nó vào đây.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];
}
