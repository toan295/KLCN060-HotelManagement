namespace KLCN060.Api.Services.Auth;

/// <summary>Cấu hình gửi email (mục "Email" trong appsettings.json / User Secrets / biến môi trường).</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>
    /// Thời gian chờ (giây) giữa các lần thử gửi lại khi lỗi tạm thời (mạng, máy chủ thư bận...). Số phần tử + 1 = số lần thử tối đa.
    /// Không cấu hình thì dùng {3, 10}: thử tối đa 3 lần. Lỗi vĩnh viễn (sai mật khẩu SMTP, địa chỉ nhận bị từ chối) không thử lại.
    /// (Giá trị mặc định để rỗng vì bộ nạp cấu hình NỐI thêm phần tử vào mảng có sẵn thay vì thay thế.)
    /// </summary>
    public int[] RetryDelaysSeconds { get; set; } = [];
}

public class SmtpOptions
{
    /// <summary>Máy chủ SMTP (VD smtp.gmail.com). Để trống = CHƯA cấu hình: chức năng quên mật khẩu trả lỗi 503 thay vì giả vờ đã gửi.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>StartTls (cổng 587, phổ biến nhất) | SslOnConnect (cổng 465) | None (không mã hóa - chỉ dùng máy chủ thử nghiệm nội bộ).</summary>
    public string SecureMode { get; set; } = "StartTls";

    public string? User { get; set; }

    /// <summary>KHÔNG ghi mật khẩu vào appsettings.json: dùng User Secrets (phát triển) hoặc biến môi trường Email__Smtp__Password (triển khai).</summary>
    public string? Password { get; set; }

    /// <summary>Địa chỉ người gửi hiển thị. Với Gmail/Outlook phải trùng tài khoản đăng nhập SMTP.</summary>
    public string? From { get; set; }

    public string FromName { get; set; } = "KLCN060 Hotel";

    public int TimeoutSeconds { get; set; } = 20;
}
