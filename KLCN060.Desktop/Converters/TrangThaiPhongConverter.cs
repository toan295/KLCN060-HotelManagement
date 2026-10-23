using System.Globalization;
using System.Windows.Data;

namespace KLCN060.Desktop.Converters;

/// <summary>Đổi mã trạng thái phòng (VC/VD/VI/OC/OD/OOO) sang mô tả tiếng Việt dễ hiểu cho người dùng cuối -
/// giá trị binding thật (dùng khi gọi API) vẫn là mã gốc, converter chỉ đổi phần HIỂN THỊ.</summary>
public sealed class TrangThaiPhongConverter : IValueConverter
{
    public static string MoTa(string? maTrangThai) => maTrangThai switch
    {
        "VC" => "Trống, đã dọn",
        "VD" => "Trống, chưa dọn",
        "VI" => "Trống, đã kiểm tra",
        "OC" => "Đang có khách",
        "OD" => "Có khách, cần dọn",
        "OOO" => "Ngừng phục vụ",
        _ => maTrangThai ?? ""
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MoTa(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
