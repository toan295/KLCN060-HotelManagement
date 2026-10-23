using System.Globalization;
using System.Windows.Data;

namespace KLCN060.Desktop.Converters;

/// <summary>
/// Trả về max(giá trị, tham số). Dùng để "kích thước tối thiểu của trang": khi cửa sổ đủ lớn, trang lấp đầy khung nhìn như thiết kế;
/// khi cửa sổ nhỏ hơn, trang giữ kích thước tối thiểu và khung bao ngoài (ScrollViewer) hiện thanh cuộn thay vì làm nội dung bị cắt.
/// </summary>
public sealed class MaxValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var giaTri = value is double d ? d : 0d;
        var toiThieu = parameter is null ? 0d : double.Parse(parameter.ToString()!, CultureInfo.InvariantCulture);
        return Math.Max(giaTri, toiThieu);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
