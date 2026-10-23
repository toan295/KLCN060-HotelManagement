using CommunityToolkit.Mvvm.ComponentModel;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D21 - Đổi mật khẩu của chính mình (mọi vai trò nhân viên). Tài khoản mẫu dùng chung một mật khẩu ban đầu nên nhân viên phải tự đổi được.
/// API thu hồi các phiên đăng nhập cũ sau khi đổi, vì vậy thành công thì ứng dụng đưa người dùng về màn hình đăng nhập.
/// </summary>
public partial class DoiMatKhauViewModel : ObservableObject
{
    private const int DoDaiToiThieu = 8;

    private readonly ApiClient _api;

    /// <summary>Phát ra sau khi đổi mật khẩu thành công (sau một nhịp ngắn để người dùng đọc thông báo).</summary>
    public event Action? DaDoiMatKhau;

    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool dangXuLy;
    [ObservableProperty] private bool daDoiXong;

    public DoiMatKhauViewModel(ApiClient api) => _api = api;

    /// <summary>Trả về true khi đổi thành công. Mật khẩu lấy trực tiếp từ PasswordBox (không lưu trong thuộc tính của view model).</summary>
    public async Task<bool> DoiAsync(string matKhauCu, string matKhauMoi, string nhapLai)
    {
        if (DangXuLy || DaDoiXong) return false;

        if (string.IsNullOrEmpty(matKhauCu))
            return Loi("Vui lòng nhập mật khẩu hiện tại.");
        if (matKhauMoi.Length < DoDaiToiThieu)
            return Loi($"Mật khẩu mới phải có ít nhất {DoDaiToiThieu} ký tự.");
        if (matKhauMoi != nhapLai)
            return Loi("Mật khẩu nhập lại không khớp.");
        if (matKhauMoi == matKhauCu)
            return Loi("Mật khẩu mới phải khác mật khẩu hiện tại.");

        DangXuLy = true;
        try
        {
            await _api.DoiMatKhauAsync(matKhauCu, matKhauMoi);
            DaDoiXong = true;
            ThongBaoLaLoi = false;
            ThongBao = "Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới...";
            await Task.Delay(1800);
            DaDoiMatKhau?.Invoke();
            return true;
        }
        catch (LoiYeuCauApi loi)
        {
            return Loi(loi.Message);
        }
        finally
        {
            DangXuLy = false;
        }
    }

    private bool Loi(string noiDung)
    {
        ThongBaoLaLoi = true;
        ThongBao = noiDung;
        return false;
    }
}
