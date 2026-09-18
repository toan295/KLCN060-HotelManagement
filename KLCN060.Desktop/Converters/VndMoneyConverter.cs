using System.Globalization;
using System.Windows.Data;

namespace KLCN060.Desktop.Converters;

/// <summary>Định dạng tiền VNĐ theo Mục 6 (CLAUDE.md Trọng): số nguyên kèm "đ", ví dụ 450000 -> "450.000đ".</summary>
public class VndMoneyConverter : IValueConverter
{
    private static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var amount = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return amount.ToString("N0", Vn) + "đ";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
