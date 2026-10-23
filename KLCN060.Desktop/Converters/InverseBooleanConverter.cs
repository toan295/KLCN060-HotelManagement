using System.Globalization;
using System.Windows.Data;

namespace KLCN060.Desktop.Converters;

/// <summary>Dùng cho IsEnabled="{Binding DangXuLy, Converter=...}" - vô hiệu hóa nút trong lúc đang gọi API
/// (chặn bấm đúp tạo trùng dữ liệu), không cần viết property IsEnabled riêng cho từng ViewModel.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value!;
}
