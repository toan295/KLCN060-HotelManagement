using System.Windows;

namespace KLCN060.Desktop.Controls;

/// <summary>
/// Thuộc tính đính kèm cho nút điều hướng ở thanh bên: Glyph = mã ký tự trong font Segoe MDL2 Assets (biểu tượng đơn sắc, đồng bộ),
/// IsActive = nút của màn hình đang mở (được Style đặt tự động theo ManHinhDangChon).
/// </summary>
public static class NavIcon
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.RegisterAttached("Glyph", typeof(string), typeof(NavIcon), new PropertyMetadata(string.Empty));

    public static string GetGlyph(DependencyObject d) => (string)d.GetValue(GlyphProperty);
    public static void SetGlyph(DependencyObject d, string value) => d.SetValue(GlyphProperty, value);

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(NavIcon), new PropertyMetadata(false));

    public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);
    public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);
}
