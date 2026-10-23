using System.Globalization;
using System.Windows.Data;

namespace KLCN060.Desktop.Converters;

/// <summary>
/// true khi các giá trị truyền vào bằng nhau (so sánh bằng Equals, chuỗi so sánh thường). Dùng trong DataTrigger/MultiBinding để
/// đánh dấu mục đang chọn (nút điều hướng đang mở, ô phòng đang chọn...) mà không phải viết riêng một Style cho từng mục.
/// </summary>
public sealed class EqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length >= 2 && values[0] is not null && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
