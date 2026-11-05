using System.Text.RegularExpressions;

namespace KLCN060.Web.Helpers;

/// <summary>
/// Mã (loại phòng, phiếu đặt, giao dịch) do khách gửi lên được ghép vào đường dẫn gọi API: chỉ chấp nhận ký tự an toàn
/// (chữ, số, gạch ngang/dưới) để không ai chèn được "../", "?" hay "#" làm Website gọi sang endpoint khác bằng token của khách.
/// </summary>
public static partial class MaThamChieu
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex MauHopLe();

    public static bool HopLe(string? ma) => ma is not null && MauHopLe().IsMatch(ma);

    /// <summary>Trả mã đã mã hóa URL, hoặc null nếu mã không hợp lệ (controller trả 404).</summary>
    public static string? DoanDuongDan(string? ma) => HopLe(ma) ? Uri.EscapeDataString(ma!) : null;
}
