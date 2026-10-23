using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KLCN060.Api.DTOs.Payments;

namespace KLCN060.Api.Services.Payments;

/// <summary>
/// Chữ ký HMAC-SHA256 cho webhook "tiền về": bên gửi (ngân hàng) ký bằng khóa bí mật dùng chung, bên nhận (hệ thống) tính lại
/// và so sánh. Nhờ vậy chỉ ngân hàng mới tạo được thông báo hợp lệ - kẻ ngoài không thể tự gọi webhook để "đánh dấu đã trả tiền".
/// </summary>
public static class WebhookSignature
{
    /// <summary>Chuỗi chuẩn hóa để ký: các trường ghép bằng '|' theo thứ tự cố định (không phụ thuộc thứ tự/khoảng trắng của JSON).</summary>
    public static string Canonicalize(BankNotificationRequest n)
        => string.Join('|',
            n.MaGiaoDichNganHang,
            n.SoTaiKhoanNhan,
            Math.Round(n.SoTien, 0).ToString(CultureInfo.InvariantCulture),
            n.NoiDung,
            n.ThoiGian.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));

    public static string Sign(string secret, BankNotificationRequest n)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Canonicalize(n)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string secret, BankNotificationRequest n, string? signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
            return false;

        var expected = Encoding.ASCII.GetBytes(Sign(secret, n));
        var actual = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        // So sánh thời gian cố định để không lộ thông tin qua độ trễ phản hồi.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
