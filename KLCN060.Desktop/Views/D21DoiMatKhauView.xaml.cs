using System.Windows;
using System.Windows.Controls;
using KLCN060.Desktop.ViewModels;

namespace KLCN060.Desktop.Views;

public partial class D21DoiMatKhauView : UserControl
{
    public D21DoiMatKhauView()
    {
        InitializeComponent();
    }

    // PasswordBox.Password không binding được vào view model (cố ý, để mật khẩu không nằm trong thuộc tính): đọc trực tiếp khi bấm nút.
    private async void DoiButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DoiMatKhauViewModel viewModel)
            return;

        var thanhCong = await viewModel.DoiAsync(MatKhauCu.Password, MatKhauMoi.Password, NhapLai.Password);
        if (!thanhCong)
        {
            MatKhauMoi.Clear();
            NhapLai.Clear();
        }
    }
}
