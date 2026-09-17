using System.Globalization;

namespace KLCN060.Web.Helpers;

public static class MoneyFormatter
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Định dạng số nguyên VNĐ kèm "đ", ví dụ 450000 -> "450.000đ".</summary>
    public static string ToVnd(this decimal amount)
        => amount.ToString("N0", Vn) + "đ";
}
