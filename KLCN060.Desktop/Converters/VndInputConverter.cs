using System.Globalization;
using System.Windows.Data;
using KLCN060.Desktop.Controls;

namespace KLCN060.Desktop.Converters;

/// <summary>
/// Nối ô nhập tiền (chuỗi "450.000", dùng cùng <see cref="MoneyInput"/>) với thuộc tính số thập phân của ViewModel:
/// 450000 ⇄ "450.000"; 0 hiện là ô trống; chữ không phải số bị bỏ; số quá lớn bị chặn để không tràn.
/// </summary>
public class VndInputConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is decimal d && d > 0 ? MoneyInput.Dinh(d) : "";

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var chuSo = new string((value as string ?? "").Where(char.IsDigit).ToArray());
        if (chuSo.Length == 0) return 0m;
        if (chuSo.Length > 12) chuSo = chuSo[..12];
        return decimal.Parse(chuSo, CultureInfo.InvariantCulture);
    }
}
