using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Yêu cầu chuyển từ một màn hình sang màn hình xử lý khác kèm sẵn đối tượng đang chọn (phiếu đặt, lượt ở, phòng, hóa đơn, khách).</summary>
public sealed record YeuCauMoManHinh(string ManHinh, string? MaPhieuDat = null, string? MaPhieuNhan = null, string? MaPhong = null, string? MaHoaDon = null, string? MaKhach = null, string? TuKhoa = null);

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
        _api.BiTuChoiQuyen += TrenBiTuChoiQuyen;
        currentViewModel = TaoManHinhDangNhap();
    }

    private static void ChayTrenUi(Action hanhDong)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
            Application.Current.Dispatcher.Invoke(hanhDong);
        else
            hanhDong();
    }

    // Access token hết hạn (30 phút) VÀ làm mới bằng refresh token cũng thất bại (ApiClient.SessionExpired) -> quay về màn hình đăng nhập.
    // Sự kiện có thể phát từ một continuation nên không chắc đang ở UI thread -> điều phối qua Dispatcher.
    private void TrenPhienHetHan() => ChayTrenUi(() =>
    {
        // Đang ở màn hình đăng nhập (đã đăng xuất / phiên đã bị đóng) thì không dựng lại form: việc đó làm mất tên đăng nhập người dùng đang gõ.
        if (!DaDangNhap) return;
        _session.Clear();
        _api.ClearAccessToken();
        CurrentViewModel = TaoManHinhDangNhap();
    });

    // API vừa từ chối một thao tác (403): rất có thể Quản lý đã đổi quyền của vai trò này ở màn hình Phân quyền. Lấy lại danh sách quyền
    // hiện tại để thanh bên hiện đúng các chức năng còn được phép (quyền thật do API kiểm tra ở từng request, không phụ thuộc bước này).
    private bool _dangLamMoiQuyen;

    private void TrenBiTuChoiQuyen() => ChayTrenUi(() => _ = LamMoiQuyenAsync());

    private async Task LamMoiQuyenAsync()
    {
        if (_dangLamMoiQuyen || !DaDangNhap) return;
        _dangLamMoiQuyen = true;
        try
        {
            var quyen = await _api.GetQuyenCuaToiAsync();
            _session.DatQuyen(quyen.Quyen);
            CapNhatQuyenHienThi();
            (CurrentViewModel as DashboardViewModel)?.LamMoiQuyen();
        }
        catch (LoiYeuCauApi) { /* giữ nguyên menu hiện tại */ }
        catch (System.Net.Http.HttpRequestException) { }
        finally { _dangLamMoiQuyen = false; }
    }

    // Khung shell (app-chrome + sidebar) chỉ hiện sau khi đăng nhập thành công.
    public bool DaDangNhap => CurrentViewModel is not DangNhapViewModel;

    // ----- Chức năng nào hiện trên thanh bên: theo QUYỀN của vai trò (D15), không theo tên vai trò -----
    public bool CoQuyenXemPhong => _session.CoQuyen(QuyenHeThong.XemPhong);                 // D2
    public bool CoQuyenLeTan => _session.CoQuyen(QuyenHeThong.ThaoTacLeTan);                // D3-D7
    public bool CoQuyenKhachHang => _session.CoQuyen(QuyenHeThong.XemKhachHang);            // D9
    public bool CoQuyenDatPhong => _session.CoQuyen(QuyenHeThong.XemDatPhong);              // D19
    public bool CoQuyenThanhToan => _session.CoQuyen(QuyenHeThong.ThanhToan);               // D8, D20
    public bool CoQuyenBaoCao => _session.CoQuyen(QuyenHeThong.XemBaoCao);                  // D17
    public bool CoQuyenPhong => _session.CoQuyen(QuyenHeThong.QuanLyPhong);                 // D11, D14
    public bool CoQuyenKhuyenMai => _session.CoQuyen(QuyenHeThong.QuanLyKhuyenMai);         // D12
    public bool CoQuyenDichVu => _session.CoQuyen(QuyenHeThong.QuanLyDichVu);               // D13
    public bool CoQuyenQuanTri => _session.CoQuyen(QuyenHeThong.QuanTriHeThong);            // D15, D16
    public bool CoQuyenDuyet => _session.CoQuyen(QuyenHeThong.DuyetNghiepVu);               // D19 (xử lý ngoại lệ)
    public bool CoQuyenNhatKy => _session.CoQuyen(QuyenHeThong.XemNhatKy);                   // D24
    public bool CoQuyenHoaDon => CoQuyenThanhToan || CoQuyenBaoCao;                         // D20 (Kế toán thu tiền; Quản lý chỉ xem)

    // Tiêu đề nhóm trên thanh bên chỉ hiện khi có ít nhất một mục trong nhóm.
    public bool CoNhomVanHanh => CoQuyenXemPhong || CoQuyenLeTan || CoQuyenKhachHang || CoQuyenDatPhong || CoQuyenHoaDon;
    public bool CoNhomDanhMuc => CoQuyenPhong || CoQuyenKhuyenMai || CoQuyenDichVu;
    public bool CoNhomHeThong => CoQuyenBaoCao || CoQuyenQuanTri || CoQuyenNhatKy;

    // Nhãn vai trò hiển thị trên thanh app-chrome, ví dụ "Nguyễn Văn Quản · Quản lý".
    public string VaiTroHienThi => (string.IsNullOrWhiteSpace(_session.HoTen) ? "" : _session.HoTen + " · ") + _session.VaiTro switch
    {
        "QUAN_LY" => "Quản lý",
        "LE_TAN" => "Lễ tân",
        _ => _session.VaiTro ?? ""
    };

    private void CapNhatQuyenHienThi()
    {
        OnPropertyChanged(nameof(VaiTroHienThi));
        OnPropertyChanged(nameof(CoQuyenXemPhong));
        OnPropertyChanged(nameof(CoQuyenLeTan));
        OnPropertyChanged(nameof(CoQuyenKhachHang));
        OnPropertyChanged(nameof(CoQuyenDatPhong));
        OnPropertyChanged(nameof(CoQuyenThanhToan));
        OnPropertyChanged(nameof(CoQuyenBaoCao));
        OnPropertyChanged(nameof(CoQuyenPhong));
        OnPropertyChanged(nameof(CoQuyenKhuyenMai));
        OnPropertyChanged(nameof(CoQuyenDichVu));
        OnPropertyChanged(nameof(CoQuyenQuanTri));
        OnPropertyChanged(nameof(CoQuyenNhatKy));
        OnPropertyChanged(nameof(CoQuyenDuyet));
        OnPropertyChanged(nameof(CoQuyenHoaDon));
        OnPropertyChanged(nameof(CoNhomVanHanh));
        OnPropertyChanged(nameof(CoNhomDanhMuc));
        OnPropertyChanged(nameof(CoNhomHeThong));
    }

    // Màn hình có đồng hồ làm mới nền (VD sơ đồ phòng) phải dừng khi người dùng chuyển sang màn hình khác.
    partial void OnCurrentViewModelChanging(object value) => (CurrentViewModel as IDisposable)?.Dispose();

    partial void OnCurrentViewModelChanged(object value)
    {
        OnPropertyChanged(nameof(DaDangNhap));
        CapNhatQuyenHienThi();
        ManHinhDangChon = value switch
        {
            QuanLyKhachHangViewModel => "d9",
            DashboardViewModel => "d2",
            DatPhongViewModel => "d3",
            NhanPhongViewModel => "d4",
            DoiPhongViewModel => "d5",
            DichVuSuDungViewModel => "d6",
            TraPhongViewModel => "d7",
            ThanhToanViewModel => "d8",
            PhanQuyenViewModel => "d15",
            SaoLuuViewModel => "d16",
            NhatKyViewModel => "d24",
            BaoCaoViewModel => "d17",
            QuanLyDatPhongViewModel => "d19",
            HoaDonViewModel => "d20",
            DoiMatKhauViewModel => "d21",
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

    // Mỗi tài khoản cần được đưa vào MỘT màn hình có thật ngay sau khi đăng nhập, nếu không CurrentViewModel vẫn còn là
    // DangNhapViewModel -> DaDangNhap = false -> shell không bao giờ hiện ra và người dùng bị kẹt.
    // Chọn màn hình đầu tiên mà QUYỀN của vai trò cho phép: Sơ đồ phòng (Quản lý, Lễ tân) -> Hóa đơn -> ...
    private void DieuHuongSauDangNhap()
    {
        if (CoQuyenXemPhong) HienThiDashboard();
        else if (CoQuyenThanhToan) HienThiHoaDon();
        else if (CoQuyenBaoCao) HienThiBaoCao();
        else if (CoQuyenPhong) HienThiPhong();
        else if (CoQuyenQuanTri) HienThiPhanQuyen();
        else if (CoQuyenKhachHang) HienThiKhachHang();
        else if (CoQuyenDatPhong) HienThiQuanLyDatPhong();
        else HienThiDoiMatKhau();   // vai trò chưa được cấp chức năng nào: vẫn có màn hình để đổi mật khẩu/đăng xuất
    }

    /// <summary>Chuyển tới màn hình có tên chung (từ nút "Mở màn hình" của Sơ đồ phòng, Báo cáo...), nếu vai trò có quyền vào.</summary>
    private void MoTheoTen(string manHinh)
    {
        switch (manHinh)
        {
            case "PHONG": if (CoQuyenPhong) HienThiPhong(); else HienThiDashboard(); break;
            case "LUU_TRU": HienThiDashboard(); break;
            case "DAT_PHONG": HienThiQuanLyDatPhong(); break;
            case "HOA_DON": if (CoQuyenHoaDon) HienThiHoaDon(); else HienThiBaoCao(); break;
            case "SAO_LUU": HienThiSaoLuu(); break;
            case "NHAT_KY": HienThiNhatKy(); break;
            case "NHAN_VIEN": HienThiPhanQuyen(); break;
        }
    }

    [RelayCommand]
    private void HienThiKhachHang()
    {
        if (!CoQuyenKhachHang) return;
        var khach = new QuanLyKhachHangViewModel(_api);
        khach.MoXuLy += MoXuLyTuManHinh;   // việc làm ngay từ hồ sơ khách: dịch vụ / đổi phòng / trả phòng / nhận phòng / thu tiền / đặt phòng
        CurrentViewModel = khach;
    }

    /// <summary>Mở thẳng màn hình xử lý với đúng phiếu đặt / lượt ở / phòng / hóa đơn đã chọn (vẫn kiểm tra quyền; API kiểm tra lại ở từng request).</summary>
    private void MoXuLyTuManHinh(YeuCauMoManHinh y)
    {
        switch (y.ManHinh)
        {
            case "DAT_PHONG":
                if (!CoQuyenDatPhong) return;
                var datPhong = new QuanLyDatPhongViewModel(_api, CoQuyenLeTan, CoQuyenDuyet);
                datPhong.MoXuLy += MoXuLyTuManHinh;
                CurrentViewModel = datPhong;
                if (y.MaPhieuDat is not null) _ = datPhong.MoPhieuAsync(y.MaPhieuDat);
                else if (!string.IsNullOrWhiteSpace(y.TuKhoa)) _ = datPhong.LocTheoTuKhoaAsync(y.TuKhoa);
                break;
            case "DAT_PHONG_MOI":
                if (!CoQuyenLeTan) return;
                var datMoi = new DatPhongViewModel(_api);
                CurrentViewModel = datMoi;
                if (y.MaKhach is not null) _ = datMoi.ChonKhachAsync(y.MaKhach);
                break;
            case "NHAN_PHONG":
                if (!CoQuyenLeTan) return;
                var nhan = new NhanPhongViewModel(_api);
                CurrentViewModel = nhan;
                if (y.MaPhieuDat is not null) _ = nhan.MoPhieuAsync(y.MaPhieuDat);
                else if (y.MaPhong is not null) _ = nhan.MoVangLaiAsync(y.MaPhong);
                break;
            case "TRA_PHONG":
                if (!CoQuyenLeTan) return;
                CurrentViewModel = TaoTraPhong(y.MaPhieuNhan, y.MaPhong);
                break;
            case "DOI_PHONG":
                if (CoQuyenLeTan) CurrentViewModel = new DoiPhongViewModel(_api, y.MaPhieuNhan, y.MaPhong);
                break;
            case "DICH_VU":
                if (CoQuyenLeTan) CurrentViewModel = new DichVuSuDungViewModel(_api, y.MaPhieuNhan, y.MaPhong);
                break;
            case "THANH_TOAN":
                if (CoQuyenThanhToan) CurrentViewModel = new ThanhToanViewModel(_api, y.MaHoaDon);
                break;
            case "SO_DO_PHONG":
                if (CoQuyenXemPhong) HienThiDashboard();
                break;
            case "HOA_DON":
                if (!CoQuyenHoaDon) return;
                var dsHoaDon = new HoaDonViewModel(_api, y.TuKhoa);
                dsHoaDon.MoThanhToan += maHoaDon => CurrentViewModel = new ThanhToanViewModel(_api, maHoaDon);
                CurrentViewModel = dsHoaDon;
                break;
            case "PHONG" or "SAO_LUU":
                MoTheoTen(y.ManHinh);
                break;
        }
    }

    [RelayCommand]
    private void HienThiDashboard()
    {
        if (!CoQuyenXemPhong) return;
        var soDo = new DashboardViewModel(_api);
        soDo.MoManHinh += MoTheoTen;   // nút "Mở Phiếu đặt phòng" trên chi tiết phòng
        soDo.MoXuLy += MoXuLyTuManHinh;   // nút trả phòng / gia hạn / nhận phòng với đúng khách và booking của phòng đang chọn
        CurrentViewModel = soDo;
    }
    [RelayCommand] private void HienThiDatPhong() { if (CoQuyenLeTan) CurrentViewModel = new DatPhongViewModel(_api); }
    [RelayCommand] private void HienThiNhanPhong() { if (CoQuyenLeTan) CurrentViewModel = new NhanPhongViewModel(_api); }
    [RelayCommand] private void HienThiDoiPhong() { if (CoQuyenLeTan) CurrentViewModel = new DoiPhongViewModel(_api); }
    [RelayCommand] private void HienThiDichVuSuDung() { if (CoQuyenLeTan) CurrentViewModel = new DichVuSuDungViewModel(_api); }
    [RelayCommand]
    private void HienThiTraPhong()
    {
        if (!CoQuyenLeTan)
            return;

        CurrentViewModel = TaoTraPhong(null, null);
    }

    private TraPhongViewModel TaoTraPhong(string? maPhieuNhan, string? maPhong)
    {
        var manHinhTraPhong = new TraPhongViewModel(_api, maPhieuNhan, maPhong);
        manHinhTraPhong.HoaDonDaTao += maHoaDon =>
            CurrentViewModel = new ThanhToanViewModel(_api, maHoaDon);
        return manHinhTraPhong;
    }
    [RelayCommand] private void HienThiThanhToan() { if (CoQuyenThanhToan) CurrentViewModel = new ThanhToanViewModel(_api); }

    // D11-D14: mỗi màn hình cần đúng quyền quản lý tương ứng (API cũng chặn 403): chặn luôn ở đây để không mở nhầm màn hình.
    [RelayCommand] private void HienThiPhong() { if (CoQuyenPhong) CurrentViewModel = new QuanLyDanhMucPhongViewModel(_api); }
    [RelayCommand] private void HienThiKhuyenMai() { if (CoQuyenKhuyenMai) CurrentViewModel = new QuanLyKhuyenMaiViewModel(_api); }
    [RelayCommand] private void HienThiDichVu() { if (CoQuyenDichVu) CurrentViewModel = new QuanLyDichVuViewModel(_api); }
    [RelayCommand] private void HienThiCoSoVatChat() { if (CoQuyenPhong) CurrentViewModel = new QuanLyCoSoVatChatViewModel(_api); }
    // D19: mọi người có quyền xem đặt phòng vào được; thu cọc/hủy phiếu chỉ hiện khi có thêm quyền thao tác của lễ tân.
    [RelayCommand]
    private void HienThiQuanLyDatPhong()
    {
        if (!CoQuyenDatPhong) return;
        var datPhong = new QuanLyDatPhongViewModel(_api, CoQuyenLeTan, CoQuyenDuyet);
        datPhong.MoXuLy += MoXuLyTuManHinh;   // nút "Nhận phòng" / "Trả phòng" ngay trên chi tiết phiếu
        CurrentViewModel = datPhong;
    }
    [RelayCommand]
    private void HienThiHoaDon()
    {
        if (!CoQuyenHoaDon)
            return;

        // "Mở thanh toán" trên danh sách hóa đơn chuyển sang D8 với đúng hóa đơn đã chọn (giống luồng từ Trả phòng sang Thanh toán).
        var danhSach = new HoaDonViewModel(_api);
        danhSach.MoThanhToan += maHoaDon => CurrentViewModel = new ThanhToanViewModel(_api, maHoaDon);
        CurrentViewModel = danhSach;
    }

    // Đổi mật khẩu của chính mình (mọi vai trò). API thu hồi các phiên cũ nên sau khi đổi thành công quay về màn hình đăng nhập.
    [RelayCommand]
    private void HienThiDoiMatKhau()
    {
        var manHinh = new DoiMatKhauViewModel(_api);
        manHinh.DaDoiMatKhau += () =>
        {
            _session.Clear();
            _api.ClearAccessToken();
            CurrentViewModel = TaoManHinhDangNhap();
        };
        CurrentViewModel = manHinh;
    }
    [RelayCommand] private void HienThiPhanQuyen() { if (CoQuyenQuanTri) CurrentViewModel = new PhanQuyenViewModel(_api); }
    [RelayCommand] private void HienThiNhatKy() { if (CoQuyenNhatKy) CurrentViewModel = new NhatKyViewModel(_api); }
    [RelayCommand] private void HienThiSaoLuu() { if (CoQuyenQuanTri) CurrentViewModel = new SaoLuuViewModel(_api); }
    [RelayCommand]
    private void HienThiBaoCao()
    {
        if (!CoQuyenBaoCao) return;
        var baoCao = new BaoCaoViewModel(_api);
        CurrentViewModel = baoCao;
    }

    // "Đăng xuất": gọi API để thu hồi refresh token, xóa phiên làm việc, rồi quay về màn hình đăng nhập D1.
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
