using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class ManHinhChinhViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AppSession _session;
    [ObservableProperty] private object currentViewModel;
    [ObservableProperty] private string manHinhDangChon = "";

    public ManHinhChinhViewModel(ApiClient api, AppSession session)
    {
        _api = api;
        _session = session;
        _api.SessionExpired += TrenPhienHetHan;
        currentViewModel = TaoManHinhDangNhap();
    }

    // Access token het han (30 phut) VA lam moi bang refresh token cung that bai (ApiClient.SessionExpired) ->
    // truoc day Desktop khong co duong nao thoat khoi trang thai loi lien tuc, phai dong app thu cong.
    // Su kien co the phat tu 1 continuation nen khong chac chan dang o UI thread -> dieu phoi qua Dispatcher.
    private void TrenPhienHetHan()
    {
        void ThucHien()
        {
            _session.Clear();
            _api.ClearAccessToken();
            CurrentViewModel = TaoManHinhDangNhap();
        }

        if (Application.Current?.Dispatcher.CheckAccess() == false)
            Application.Current.Dispatcher.Invoke(ThucHien);
        else
            ThucHien();
    }

    // Khung shell (app-chrome + sidebar) chỉ hiện sau khi đăng nhập thành công - đúng bản thiết kế
    // ("D1: standalone, no app chrome/sidebar (login screen precedes the main app shell)").
    public bool DaDangNhap => CurrentViewModel is not DangNhapViewModel;

    public bool LaQuanLy => _session.VaiTro == "QUAN_LY";

    public bool CoTheQuanLyKhachHang
        => _session.VaiTro is "QUAN_LY" or "LE_TAN";

    // Nhãn vai trò hiển thị trên thanh app-chrome, khớp mẫu "Quản lý · Trần Văn Long" trong bản thiết kế
    // nhưng dùng đúng tên đăng nhập thật của phiên hiện tại thay vì tên minh hoạ.
    public string VaiTroHienThi => _session.VaiTro switch
    {
        "QUAN_LY" => "Quản lý",
        "LE_TAN" => "Lễ tân",
        "BUONG_PHONG" => "Buồng phòng",
        "KE_TOAN" => "Kế toán",
        _ => _session.VaiTro ?? ""
    } + (string.IsNullOrEmpty(_session.TenDangNhap) ? "" : $" · {_session.TenDangNhap}");

    partial void OnCurrentViewModelChanged(object value)
    {
        OnPropertyChanged(nameof(DaDangNhap));
        OnPropertyChanged(nameof(VaiTroHienThi));
        OnPropertyChanged(nameof(LaQuanLy));
        OnPropertyChanged(nameof(CoTheQuanLyKhachHang));
        ManHinhDangChon = value switch
        {
            QuanLyKhachHangViewModel => "d9",
            DashboardViewModel => "d2",
            DatPhongViewModel => "d3",
            NhanPhongViewModel => "d4",
            QuanLyDanhMucPhongViewModel => "d11",
            QuanLyKhuyenMaiViewModel => "d12",
            QuanLyDichVuViewModel => "d13",
            QuanLyCoSoVatChatViewModel => "d14",
            _ => ""
        };
    }

    private DangNhapViewModel TaoManHinhDangNhap()
    {
        var login = new DangNhapViewModel(_api, _session);
        login.LoginSucceeded += DieuHuongSauDangNhap;
        return login;
    }

    // D11-D14 (Mục 2, CLAUDE.md Trọng) chỉ dành cho vai trò QUAN_LY. Các vai trò khác (LE_TAN/KE_TOAN/BUONG_PHONG)
    // chưa có màn hình riêng ở Giai đoạn này -> giữ nguyên màn hình "Đăng nhập thành công" của D1 làm màn hình tạm,
    // đúng như phương án Mục 5 cho phép, thay vì đẩy nhầm họ vào màn hình quản lý mà họ không có quyền thao tác.
    private void DieuHuongSauDangNhap()
    {
        if (_session.VaiTro == "QUAN_LY")
            HienThiPhong();
        else if (_session.VaiTro == "LE_TAN")
            HienThiDashboard();
    }

    [RelayCommand]
    private void HienThiKhachHang()
    {
        if (CoTheQuanLyKhachHang)
            CurrentViewModel = new QuanLyKhachHangViewModel(_api);
    }
    [RelayCommand] private void HienThiDashboard() { if (_session.VaiTro is "LE_TAN" or "BUONG_PHONG" or "QUAN_LY") CurrentViewModel = new DashboardViewModel(_api); }
    [RelayCommand] private void HienThiDatPhong() { if (_session.VaiTro == "LE_TAN") CurrentViewModel = new DatPhongViewModel(_api); }
    [RelayCommand] private void HienThiNhanPhong() { if (_session.VaiTro == "LE_TAN") CurrentViewModel = new NhanPhongViewModel(_api); }

    [RelayCommand] private void HienThiPhong() => CurrentViewModel = new QuanLyDanhMucPhongViewModel(_api);
    [RelayCommand] private void HienThiKhuyenMai() => CurrentViewModel = new QuanLyKhuyenMaiViewModel(_api);
    [RelayCommand] private void HienThiDichVu() => CurrentViewModel = new QuanLyDichVuViewModel(_api);
    [RelayCommand] private void HienThiCoSoVatChat() => CurrentViewModel = new QuanLyCoSoVatChatViewModel(_api);

    // "← Đăng xuất" (bản thiết kế D2-D17): gọi API để thu hồi refresh token, xoá phiên làm việc,
    // rồi quay về màn hình đăng nhập D1 - trước đây chưa có cách nào thoát khỏi phiên đã đăng nhập.
    [RelayCommand]
    private async Task DangXuatAsync()
    {
        try { await _api.LogoutAsync(); }
        catch (LoiYeuCauApi) { /* best-effort: vẫn đăng xuất phía client kể cả khi gọi API thất bại */ }

        _session.Clear();
        _api.ClearAccessToken();
        CurrentViewModel = TaoManHinhDangNhap();
    }
}
