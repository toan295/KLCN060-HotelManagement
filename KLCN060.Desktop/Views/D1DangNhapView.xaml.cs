using System.Windows.Controls;
using KLCN060.Desktop.ViewModels;

namespace KLCN060.Desktop.Views;

public partial class D1DangNhapView : UserControl
{
    public D1DangNhapView()
    {
        InitializeComponent();
    }

    private async void LoginButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not DangNhapViewModel viewModel)
            return;

        var matKhau = PasswordInput.Password;
        if (viewModel.LoginCommand.CanExecute(matKhau))
            await viewModel.LoginCommand.ExecuteAsync(matKhau);
    }
}
