using System.Net.Sockets;
using System.Threading.Channels;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services.Auth;

/// <summary>
/// Hàng đợi email gửi đi. Request của người dùng chỉ việc bỏ thư vào hàng đợi rồi trả lời ngay: thời gian phản hồi không phụ thuộc
/// máy chủ SMTP chậm hay nhanh, và cũng không phụ thuộc tài khoản có tồn tại hay không (không dò được tài khoản qua độ trễ).
/// </summary>
public interface IEmailQueue
{
    /// <summary>false = chưa cấu hình gửi email; người gọi phải báo lỗi rõ ràng thay vì giả vờ đã gửi.</summary>
    bool IsAvailable { get; }

    /// <summary>Đưa thư vào hàng đợi. false nếu hàng đợi đầy (đã ghi log).</summary>
    bool TryEnqueue(EmailMessage message);
}

public sealed class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });
    private readonly IEmailSender _sender;
    private readonly ILogger<ChannelEmailQueue> _logger;

    public ChannelEmailQueue(IEmailSender sender, ILogger<ChannelEmailQueue> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool IsAvailable => _sender.IsConfigured;

    public bool TryEnqueue(EmailMessage message)
    {
        if (_channel.Writer.TryWrite(message))
            return true;

        _logger.LogError("Hàng đợi email đầy, bỏ thư \"{Subject}\" tới {To}.", message.Subject, SmtpEmailSender.MaAn(message.To));
        return false;
    }
}

/// <summary>Dịch vụ nền lấy thư khỏi hàng đợi và gửi; lỗi tạm thời (mạng, máy chủ thư bận) được thử lại vài lần, lỗi vĩnh viễn thì bỏ và ghi log.</summary>
public sealed class EmailDispatcher : BackgroundService
{
    private static readonly int[] MacDinhThuLai = [3, 10];

    private readonly ChannelEmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(ChannelEmailQueue queue, IEmailSender sender, IOptions<EmailOptions> options, ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_sender.IsConfigured)
            _logger.LogWarning("CHƯA cấu hình gửi email (Email:Smtp:Host / Email:Smtp:From): chức năng quên mật khẩu sẽ trả lỗi 503 cho tới khi cấu hình.");

        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
                await GuiCoThuLaiAsync(message, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Đang tắt ứng dụng.
        }
    }

    private async Task GuiCoThuLaiAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        var doiGiua = _options.RetryDelaysSeconds is { Length: > 0 } ? _options.RetryDelaysSeconds : MacDinhThuLai;
        var soLanToiDa = doiGiua.Length + 1;

        for (var lan = 1; lan <= soLanToiDa; lan++)
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var tamThoi = LaLoiTamThoi(ex);
                var conThuLai = tamThoi && lan < soLanToiDa;
                _logger.LogError(ex, "Gửi email \"{Subject}\" tới {To} thất bại (lần {Lan}/{Max}, {Loai}){ThuLai}",
                    message.Subject, SmtpEmailSender.MaAn(message.To), lan, soLanToiDa, tamThoi ? "lỗi tạm thời" : "lỗi vĩnh viễn",
                    conThuLai ? $", thử lại sau {doiGiua[lan - 1]}s" : ", bỏ qua");

                if (!conThuLai)
                    return;

                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, doiGiua[lan - 1])), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Lỗi mạng/hết thời gian/máy chủ báo tạm thời (mã 4xx) thì đáng thử lại; sai mật khẩu, địa chỉ bị từ chối (5xx), cấu hình sai thì không.</summary>
    internal static bool LaLoiTamThoi(Exception ex) => ex switch
    {
        SmtpCommandException cmd => (int)cmd.StatusCode is >= 400 and < 500,
        ServiceNotAuthenticatedException or MailKit.Security.AuthenticationException => false,
        SmtpProtocolException or ServiceNotConnectedException => true,
        IOException or SocketException or TimeoutException => true,
        InvalidOperationException => false,
        _ => false
    };
}
