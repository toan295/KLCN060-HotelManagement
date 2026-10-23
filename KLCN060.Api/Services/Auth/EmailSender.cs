using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace KLCN060.Api.Services.Auth;

/// <summary>Một email cần gửi: có cả bản HTML và bản văn bản thuần (trình đọc mail không hiển thị được HTML vẫn đọc được).</summary>
public sealed record EmailMessage(string To, string? ToName, string Subject, string HtmlBody, string TextBody);

public interface IEmailSender
{
    /// <summary>false = chưa cấu hình SMTP hợp lệ (thiếu Host/From).</summary>
    bool IsConfigured { get; }

    /// <summary>Gửi ngay một email. Ném ngoại lệ nếu gửi thất bại (việc thử lại do <see cref="EmailDispatcher"/> đảm nhiệm).</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Gửi email THẬT qua SMTP bằng MailKit (hỗ trợ StartTLS/SSL, xác thực, UTF-8 đầy đủ tiêu đề tiếng Việt).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _smtp = options.Value.Smtp;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_smtp.Host)
        && !string.IsNullOrWhiteSpace(_smtp.From)
        && MailboxAddress.TryParse(_smtp.From, out var nguoiGui)
        && nguoiGui.Address.Contains('@');   // MailKit chấp nhận cả địa chỉ không có tên miền; ở đây bắt buộc phải có

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình SMTP (Email:Smtp:Host và Email:Smtp:From).");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_smtp.FromName, _smtp.From!.Trim()));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();
        // Đánh dấu thư tự động: các máy chủ thư/tự trả lời không phản hồi lại, giảm nguy cơ bị xếp vào thư rác.
        mime.Headers.Add("Auto-Submitted", "auto-generated");

        using var client = new SmtpClient { Timeout = Math.Max(5, _smtp.TimeoutSeconds) * 1000 };
        await client.ConnectAsync(_smtp.Host!.Trim(), _smtp.Port, LayCheDoBaoMat(_smtp.SecureMode), cancellationToken);

        if (!string.IsNullOrWhiteSpace(_smtp.User))
            await client.AuthenticateAsync(_smtp.User, _smtp.Password ?? string.Empty, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Đã gửi email \"{Subject}\" tới {To} qua {Host}:{Port}", message.Subject, MaAn(message.To), _smtp.Host, _smtp.Port);
    }

    private static SecureSocketOptions LayCheDoBaoMat(string? cheDo) => cheDo?.Trim().ToLowerInvariant() switch
    {
        "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
        "none" => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls
    };

    /// <summary>Không ghi địa chỉ email đầy đủ vào log.</summary>
    internal static string MaAn(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : email[0] + "***" + email[at..];
    }
}
