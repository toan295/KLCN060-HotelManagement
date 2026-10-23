using System.Windows;
using KLCN060.Desktop.Services;
using KLCN060.Desktop.ViewModels;
using Microsoft.Extensions.Configuration;

namespace KLCN060.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WPF mặc định định dạng số/ngày trong Binding theo en-US (5,450,000 / 10/29/2026); đặt vi-VN để mọi StringFormat hiển thị 5.450.000 và dd/MM/yyyy thống nhất với VndMoneyConverter.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("vi-VN")));

        // Chuỗi số do ViewModel tự ghép ($"{x:N0}") theo văn hóa của luồng: cũng phải là vi-VN, nếu không cùng một màn hình hiện lẫn "1,650,000đ" và "960.000đ".
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = vn;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = vn;
        System.Globalization.CultureInfo.CurrentCulture = vn;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            // [Khối 8 - Desktop UI Automation] Cho phép ghi đè bằng biến môi trường (Api__BaseUrl) — để
            // KLCN060.Desktop.UITests (FlaUI) trỏ ứng dụng THẬT vào 1 Api thật chạy trên CSDL test riêng, không phải
            // sửa appsettings.json của máy dev. Không đặt biến này thì hành vi giữ nguyên như cũ (đọc appsettings.json).
            .AddEnvironmentVariables()
            .Build();

        var apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl trong appsettings.json.");

        var mainWindow = new MainWindow
        {
            DataContext = new ManHinhChinhViewModel(new ApiClient(apiBaseUrl) { WebBaseUrl = configuration["Web:BaseUrl"] }, AppSession.Current)
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
