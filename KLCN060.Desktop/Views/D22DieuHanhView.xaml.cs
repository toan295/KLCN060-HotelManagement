using System.Windows;
using System.Windows.Controls;
using KLCN060.Desktop.ViewModels;

namespace KLCN060.Desktop.Views;

public partial class D22DieuHanhView : UserControl
{
    public D22DieuHanhView()
    {
        InitializeComponent();
        // Tự làm mới chỉ chạy khi màn hình đang hiện (rời màn hình thì dừng, vào lại thì chạy tiếp).
        Loaded += (_, _) => (DataContext as DieuHanhViewModel)?.BatDauTuLamMoi();
        Unloaded += (_, _) => (DataContext as DieuHanhViewModel)?.DungTuLamMoi();
    }
}
