using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class ManHinhChinhViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private object currentViewModel;

    public ManHinhChinhViewModel(ApiClient api, AppSession session)
    {
        _api = api;
        var login = new DangNhapViewModel(api, session);
        login.LoginSucceeded += HienThiPhong;
        currentViewModel = login;
    }

    [RelayCommand] private void HienThiPhong() => CurrentViewModel = new QuanLyDanhMucPhongViewModel(_api);
    [RelayCommand] private void HienThiKhuyenMai() => CurrentViewModel = new QuanLyKhuyenMaiViewModel(_api);
    [RelayCommand] private void HienThiDichVu() => CurrentViewModel = new QuanLyDichVuViewModel(_api);
    [RelayCommand] private void HienThiCoSoVatChat() => CurrentViewModel = new QuanLyCoSoVatChatViewModel(_api);
}
