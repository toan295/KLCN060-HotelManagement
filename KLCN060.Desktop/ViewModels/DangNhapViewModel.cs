using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DangNhapViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;
    private readonly AppSession _appSession;

    public event Action? LoginSucceeded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string tenDangNhap = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool dangDangNhap;

    [ObservableProperty]
    private string thongBao = string.Empty;

    [ObservableProperty]
    private bool laTaiKhoanBiKhoa;

    [ObservableProperty]
    private bool daDangNhap;

    public DangNhapViewModel(ApiClient apiClient, AppSession appSession)
    {
        _apiClient = apiClient;
        _appSession = appSession;
    }

    private bool CanLogin(string? _) => !DangDangNhap;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(string? matKhau)
    {
        ThongBao = string.Empty;
        LaTaiKhoanBiKhoa = false;

        if (string.IsNullOrWhiteSpace(TenDangNhap) || string.IsNullOrWhiteSpace(matKhau))
        {
            ThongBao = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            return;
        }

        DangDangNhap = true;

        try
        {
            var result = await _apiClient.LoginAsync(TenDangNhap.Trim(), matKhau!);
            _appSession.Start(result.AccessToken, result.RefreshToken);
            _apiClient.SetAccessToken(result.AccessToken);
            DaDangNhap = true;
            LoginSucceeded?.Invoke();
        }
        catch (LoiYeuCauApi exception)
        {
            ThongBao = exception.Message;
            LaTaiKhoanBiKhoa = exception.StatusCode == HttpStatusCode.Locked;
        }
        catch (HttpRequestException)
        {
            ThongBao = "Không thể kết nối tới máy chủ.";
        }
        catch (Exception)
        {
            ThongBao = "Đã xảy ra lỗi khi đăng nhập.";
        }
        finally
        {
            DangDangNhap = false;
        }
    }
}
