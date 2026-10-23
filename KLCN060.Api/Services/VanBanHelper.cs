using System.Globalization;
using System.Text;

namespace KLCN060.Api.Services;

/// <summary>So sánh tên không phân biệt dấu/hoa thường (dùng chung cho các danh mục: thiết bị, vai trò...).</summary>
public static class VanBanHelper
{
    public static string KhongDau(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D').Trim();
    }

    public static bool CungTen(string? a, string? b)
        => string.Equals(KhongDau(a), KhongDau(b), StringComparison.OrdinalIgnoreCase);
}
