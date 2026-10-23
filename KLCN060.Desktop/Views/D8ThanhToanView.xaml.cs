using System.Windows.Controls;
using System.Threading.Tasks;
using KLCN060.Desktop.ViewModels;

namespace KLCN060.Desktop.Views;

public partial class D8ThanhToanView : UserControl
{
    public D8ThanhToanView()
    {
        InitializeComponent();
        // Rời màn hình thanh toán thì dừng vòng hỏi trạng thái chuyển khoản (nếu đang chờ), tránh chạy ngầm vô ích.
        Unloaded += (_, _) => (DataContext as ThanhToanViewModel)?.DungTheoDoi();
    }

    // Chọn hóa đơn ở bước 1 xong thì cuộn tới bước 2 (kiểm tra và thu tiền) để lễ tân/kế toán không phải tự tìm.
    private async void DanhSach_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || !IsLoaded) return;
        await Task.Delay(450);   // chờ tải chi tiết hóa đơn
        if (CuonTrang.Content is not System.Windows.Media.Visual noiDung) return;
        var y = BuocThuTien.TransformToAncestor(noiDung).Transform(new System.Windows.Point(0, 0)).Y;
        CuonTrang.ScrollToVerticalOffset(Math.Max(0, y - 8));
    }
}
