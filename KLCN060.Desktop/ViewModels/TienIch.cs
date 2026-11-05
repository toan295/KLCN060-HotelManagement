using System.Globalization;
using System.Text;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Hộp thoại xác nhận cho thao tác khó hoàn tác; kiểm thử thay bằng hàm trả sẵn true/false để không bị treo chờ bấm.</summary>
public static class HopThoai
{
    public static Func<string, string, bool> XacNhan { get; set; } = (tieuDe, noiDung) =>
        System.Windows.MessageBox.Show(noiDung, tieuDe, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
}

/// <summary>Xử lý chuỗi tiếng Việt dùng chung cho ô tìm kiếm.</summary>
public static class VanBan
{
    /// <summary>Bỏ dấu tiếng Việt để tìm "nguyen" ra "Nguyễn".</summary>
    public static string BoDau(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c == 'đ' ? 'd' : c == 'Đ' ? 'D' : c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
