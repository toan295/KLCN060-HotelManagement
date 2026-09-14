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

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl trong appsettings.json.");

        var mainWindow = new MainWindow
        {
            DataContext = new LoginViewModel(new ApiClient(apiBaseUrl), AppSession.Current)
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
