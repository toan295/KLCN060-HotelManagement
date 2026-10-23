using System.Windows;
using System.Windows.Controls;

namespace KLCN060.Desktop.Controls;

/// <summary>Tiêu đề trang: Title (bắt buộc), Subtitle (mô tả ngắn, có thể để trống), Actions (nút thao tác đặt ở góc phải).</summary>
public partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, (d, _) => ((PageHeader)d).CapNhatTieuDe()));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    /// <summary>Tiêu đề riêng cho vai trò Quản lý (ví dụ "Quản lý hóa đơn"); để trống thì mọi vai trò dùng Title.</summary>
    public static readonly DependencyProperty ManagerTitleProperty =
        DependencyProperty.Register(nameof(ManagerTitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, (d, _) => ((PageHeader)d).CapNhatTieuDe()));

    public static readonly DependencyProperty DisplayTitleProperty =
        DependencyProperty.Register(nameof(DisplayTitle), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public PageHeader()
    {
        InitializeComponent();
        Loaded += (_, _) => CapNhatTieuDe();
    }

    private void CapNhatTieuDe() =>
        DisplayTitle = !string.IsNullOrEmpty(ManagerTitle) && Services.AppSession.Current.VaiTro == "QUAN_LY" ? ManagerTitle : Title;

    public string ManagerTitle { get => (string)GetValue(ManagerTitleProperty); set => SetValue(ManagerTitleProperty, value); }
    public string DisplayTitle { get => (string)GetValue(DisplayTitleProperty); private set => SetValue(DisplayTitleProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
}
