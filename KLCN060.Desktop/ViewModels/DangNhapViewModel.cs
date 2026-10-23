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

            // Ứng dụng này chỉ dành cho nhân viên: tài khoản khách hàng không có màn hình nào để vào. Nhân viên còn phải có ít nhất một quyền
            // (vai trò mới tạo ở D15 mà chưa được cấp quyền thì không có chức năng nào để hiện). Nếu cho qua, người dùng bị kẹt ở màn hình
            // đăng nhập "thành công" mà không có menu. Thu hồi phiên vừa tạo và báo rõ lý do.
            string? lyDoTuChoi = null;
            if (_appSession.VaiTro is null or "KHACH_HANG")
                lyDoTuChoi = "Tài khoản này không phải tài khoản nhân viên. Khách hàng vui lòng sử dụng Website để đặt phòng.";
            else
            {
                // Quyền HIỆN TẠI của vai trò (theo phân quyền ở D15) - quyết định các chức năng được hiện.
                var quyen = await _apiClient.GetQuyenCuaToiAsync();
                _appSession.DatQuyen(quyen.Quyen);
                if (quyen.Quyen.Count == 0)
                    lyDoTuChoi = "Vai trò của tài khoản này chưa được cấp quyền nào. Vui lòng liên hệ Quản lý.";
            }

            if (lyDoTuChoi is not null)
            {
                try { await _apiClient.LogoutAsync(); }
                catch (LoiYeuCauApi) { /* best-effort: phiên này bị bỏ ở phía client dù thu hồi thất bại */ }
                _appSession.Clear();
                _apiClient.ClearAccessToken();
                ThongBao = lyDoTuChoi;
                return;
            }

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
