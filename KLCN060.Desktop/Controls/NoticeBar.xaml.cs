using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KLCN060.Desktop.Controls;

/// <summary>
/// Thanh thông báo dùng chung thay cho các dòng chữ đỏ rời rạc: Text rỗng thì ẩn hoàn toàn.
/// IsError = true/false ép màu (đỏ/xanh); để null thì tự nhận: câu có "thành công" hoặc mở đầu bằng "Đã " là thành công (xanh),
/// còn lại là lỗi/cảnh báo (đỏ) - giữ đúng hành vi cũ (mọi thông báo mặc định đỏ).
/// </summary>
public partial class NoticeBar : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(NoticeBar), new PropertyMetadata(string.Empty, (d, _) => ((NoticeBar)d).CapNhat()));

    public static readonly DependencyProperty IsErrorProperty =
        DependencyProperty.Register(nameof(IsError), typeof(bool?), typeof(NoticeBar), new PropertyMetadata(null, (d, _) => ((NoticeBar)d).CapNhat()));

    public NoticeBar()
    {
        InitializeComponent();
        CapNhat();
    }

    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool? IsError { get => (bool?)GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }

    private void CapNhat()
    {
        var noiDung = Text?.Trim() ?? string.Empty;
        Visibility = noiDung.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var laLoi = IsError ?? !(noiDung.Contains("thành công", StringComparison.OrdinalIgnoreCase)
                                 || noiDung.StartsWith("Đã ", StringComparison.Ordinal));

        Bar.Background = (Brush)FindResource(laLoi ? "MauLoiNhat" : "MauThanhCongNhat");
        Bar.BorderBrush = (Brush)FindResource(laLoi ? "MauNguyHiem" : "MauThanhCong");
        Icon.Foreground = Bar.BorderBrush;
        Icon.Text = laLoi ? "" : "";
    }
}
