using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class ManHinhChinhViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly AppSession _session;
    private System.Windows.Threading.DispatcherTimer? _dongHoCanhBao;
    [ObservableProperty] private object currentViewModel;
    [ObservableProperty] private string manHinhDangChon = "";

    /// <summary>Số cảnh báo đang có (phòng bảo trì lâu, công nợ quá hạn...) hiện ở thanh trên cho người có quyền xem báo cáo.</summary>
    [ObservableProperty] private int soCanhBao;
    [ObservableProperty] private int soCanhBaoNghiemTrong;
    public bool CoCanhBao => SoCanhBao > 0;
    public bool CoCanhBaoNghiemTrong => SoCanhBaoNghiemTrong > 0;
    partial void OnSoCanhBaoChanged(int value) { OnPropertyChanged(nameof(CoCanhBao)); OnPropertyChanged(nameof(ChuThichCanhBao)); }
    /// <summary>Giải thích con số trên ô cảnh báo: đếm số LOẠI cảnh báo (mỗi loại có thể gồm nhiều mục, số mục ghi trong ngoặc ở bảng điều hành).</summary>
    public string ChuThichCanhBao => $"{SoCanhBao} loại cảnh báo đang có" + (SoCanhBaoNghiemTrong > 0 ? $", trong đó {SoCanhBaoNghiemTrong} nghiêm trọng" : "") + " - bấm để xem chi tiết. Mỗi loại có thể gồm nhiều mục (số mục ghi trong ngoặc).";

    /// <summary>
    /// NƠI DUY NHẤT cập nhật ô đếm cảnh báo: cả đồng hồ nền lẫn màn Tổng quan điều hành đều gọi hàm này với cùng một danh sách,
    /// nên con số không thể nhảy giữa hai cách đếm khác nhau (trước đây mỗi nơi tự đếm). Kế toán chỉ đếm cảnh báo tài chính xử lý được.
    /// </summary>
    public void CapNhatSoCanhBao(IEnumerable<CanhBaoDto> canhBaos)
    {
        var danhSach = LaKeToan ? DieuHanhViewModel.LocCanhBaoKeToan(canhBaos) : canhBaos.ToList();
        SoCanhBaoNghiemTrong = danhSach.Count(x => x.MucDo == "NGHIEM_TRONG");
        SoCanhBao = danhSach.Count;
        GhiNhanCanhBao(danhSach);
    }

    /// <summary>Thông báo nổi khi xuất hiện cảnh báo MỚI (hoặc tăng thêm) sau lần kiểm tra trước: hiện ở góc màn hình cho tới khi người dùng đóng hoặc bấm "Xem".</summary>
    [ObservableProperty] private string thongBaoCanhBaoMoi = "";
    public bool CoThongBaoCanhBaoMoi => !string.IsNullOrEmpty(ThongBaoCanhBaoMoi);
    partial void OnThongBaoCanhBaoMoiChanged(string value) => OnPropertyChanged(nameof(CoThongBaoCanhBaoMoi));
    private Dictionary<string, int>? _canhBaoLanTruoc;   // null = chưa tải lần nào (lần đầu chỉ ghi nhận, không báo)

    [RelayCommand] private void DongThongBaoCanhBao() => ThongBaoCanhBaoMoi = "";
    [RelayCommand] private void XemThongBaoCanhBao() { ThongBaoCanhBaoMoi = ""; HienThiDieuHanh(); }

    /// <summary>Cảnh báo (mức nghiêm trọng / cảnh báo) mới xuất hiện hoặc tăng số lượng so với lần kiểm tra trước; mức thông tin không làm hiện thông báo nổi.</summary>
    public static List<string> TimCanhBaoMoi(IReadOnlyDictionary<string, int>? lanTruoc, IEnumerable<CanhBaoDto> hienTai)
    {
        var moi = new List<string>();
        if (lanTruoc is null) return moi;
        foreach (var cb in hienTai.Where(x => x.MucDo is "NGHIEM_TRONG" or "CANH_BAO"))
        {
            if (!lanTruoc.TryGetValue(cb.Ma, out var truoc)) moi.Add(cb.TieuDe);
            else if (cb.SoLuong > truoc) moi.Add($"{cb.TieuDe} (+{cb.SoLuong - truoc})");
        }
        return moi;
    }

    /// <summary>Cập nhật cảnh báo đã biết và hiện thông báo nổi nếu có cảnh báo mới.</summary>
    public void GhiNhanCanhBao(IReadOnlyList<CanhBaoDto> canhBaos)
    {
        var moi = TimCanhBaoMoi(_canhBaoLanTruoc, canhBaos);
        _canhBaoLanTruoc = canhBaos.ToDictionary(x => x.Ma, x => x.SoLuong);
        if (moi.Count > 0)
            ThongBaoCanhBaoMoi = "Cảnh báo mới: " + string.Join("; ", moi.Take(3)) + (moi.Count > 3 ? $" và {moi.Count - 3} cảnh báo khác" : "");
    }
    partial void OnSoCanhBaoNghiemTrongChanged(int value) { OnPropertyChanged(nameof(CoCanhBaoNghiemTrong)); OnPropertyChanged(nameof(ChuThichCanhBao)); }

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

    // Access token het han (30 phut) VA lam moi bang refresh token cung that bai (ApiClient.SessionExpired) ->
    // truoc day Desktop khong co duong nao thoat khoi trang thai loi lien tuc, phai dong app thu cong.
    // Su kien co the phat tu 1 continuation nen khong chac chan dang o UI thread -> dieu phoi qua Dispatcher.
    private void TrenPhienHetHan() => ChayTrenUi(() =>
    {
        DungTheoDoiCanhBao();
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

    // Khung shell (app-chrome + sidebar) chỉ hiện sau khi đăng nhập thành công - đúng bản thiết kế
    // ("D1: standalone, no app chrome/sidebar (login screen precedes the main app shell)").
    public bool DaDangNhap => CurrentViewModel is not DangNhapViewModel;

    // ----- Chức năng nào hiện trên thanh bên: theo QUYỀN của vai trò (D15), không theo tên vai trò -----
    public bool CoQuyenXemPhong => _session.CoQuyen(QuyenHeThong.XemPhong);                 // D2
    public bool CoQuyenLeTan => _session.CoQuyen(QuyenHeThong.ThaoTacLeTan);                // D3-D7
    public bool CoQuyenKhachHang => _session.CoQuyen(QuyenHeThong.XemKhachHang);            // D9
    public bool CoQuyenDatPhong => _session.CoQuyen(QuyenHeThong.XemDatPhong);              // D19
    public bool CoQuyenThanhToan => _session.CoQuyen(QuyenHeThong.ThanhToan);               // D8, D20
    public bool CoQuyenDoiSoat => _session.CoQuyen(QuyenHeThong.XemDoiSoat);                // D18
    public bool CoQuyenBaoCao => _session.CoQuyen(QuyenHeThong.XemBaoCao);                  // D17
    public bool CoQuyenPhong => _session.CoQuyen(QuyenHeThong.QuanLyPhong);                 // D11, D14
    public bool CoQuyenKhuyenMai => _session.CoQuyen(QuyenHeThong.QuanLyKhuyenMai);         // D12
    public bool CoQuyenDichVu => _session.CoQuyen(QuyenHeThong.QuanLyDichVu);               // D13
    public bool CoQuyenQuanTri => _session.CoQuyen(QuyenHeThong.QuanTriHeThong);            // D15, D16
    public bool CoQuyenGia => _session.CoQuyen(QuyenHeThong.QuanLyGia);                     // D25
    public bool CoQuyenDuyet => _session.CoQuyen(QuyenHeThong.DuyetNghiepVu);               // D19 (xử lý ngoại lệ)
    public bool CoQuyenNhatKy => !LaKeToan && (_session.CoQuyen(QuyenHeThong.XemNhatKy) || _session.CoQuyen(QuyenHeThong.XemNhatKyTaiChinh));   // D24 (kế toán chỉ thấy nhật ký tài chính - API lọc)
    // D27 Ca trực: màn hình dành cho lễ tân/nhân viên trực ca - KHÔNG có ở vai trò Quản lý (API cũng chặn vai trò QUAN_LY ở /api/v1/frontdesk).
    // Quản lý nắm tình hình trong ngày qua Bảng điều hành (D22) và Sơ đồ phòng (D2). Các vai trò khác giữ nguyên: mỗi tab chỉ hiện phần dữ liệu có quyền xem.
    public bool LaQuanLy => _session.VaiTro == "QUAN_LY";
    // Kế toán chỉ có MỘT màn hình điều hành (D22, số liệu tiền); không có Ca trực (lễ tân), Buồng phòng, Nhật ký hoạt động.
    public bool LaKeToan => _session.VaiTro == "KE_TOAN";
    public bool CoQuyenCaTruc => !LaQuanLy && !LaKeToan && (_session.CoQuyen(QuyenHeThong.XemDatPhong) || _session.CoQuyen(QuyenHeThong.XemLuuTru));
    public bool CoQuyenBuongPhong => !LaKeToan && (_session.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) || _session.CoQuyen(QuyenHeThong.XuLySuCo) || _session.CoQuyen(QuyenHeThong.XemPhong)
        || _session.CoQuyen(QuyenHeThong.ThaoTacLeTan) || _session.CoQuyen(QuyenHeThong.QuanLyPhong) || _session.CoQuyen(QuyenHeThong.XemBaoCao));   // D28 (mỗi tab chỉ hiện phần có quyền)
    public bool CoQuyenHoaDon => CoQuyenThanhToan || CoQuyenBaoCao;                         // D20 (Kế toán thu tiền; Quản lý chỉ xem)
    public bool CoQuyenDieuHanh => CoQuyenBaoCao;                                           // D22

    // Tiêu đề nhóm trên thanh bên chỉ hiện khi có ít nhất một mục trong nhóm.
    public bool CoNhomDieuHanh => CoQuyenDieuHanh || CoQuyenCaTruc;
    public bool CoNhomVanHanh => CoQuyenBuongPhong || CoQuyenXemPhong || CoQuyenLeTan || CoQuyenKhachHang || CoQuyenDatPhong || CoQuyenHoaDon || CoQuyenDoiSoat;
    public bool CoNhomDanhMuc => CoQuyenPhong || CoQuyenKhuyenMai || CoQuyenDichVu || CoQuyenGia;
    public bool CoNhomHeThong => CoQuyenBaoCao || CoQuyenQuanTri || CoQuyenNhatKy;

    // Nhãn vai trò hiển thị trên thanh app-chrome, khớp mẫu "Quản lý · Trần Văn Long" trong bản thiết kế
    // nhưng dùng đúng tên đăng nhập thật của phiên hiện tại thay vì tên minh hoạ.
    public string VaiTroHienThi => (string.IsNullOrWhiteSpace(_session.HoTen) ? "" : _session.HoTen + " · ") + _session.VaiTro switch
    {
        "QUAN_LY" => "Quản lý",
        "LE_TAN" => "Lễ tân",
        "BUONG_PHONG" => "Buồng phòng",
        "KE_TOAN" => "Kế toán",
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
        OnPropertyChanged(nameof(CoQuyenDoiSoat));
        OnPropertyChanged(nameof(CoQuyenBaoCao));
        OnPropertyChanged(nameof(CoQuyenPhong));
        OnPropertyChanged(nameof(CoQuyenKhuyenMai));
        OnPropertyChanged(nameof(CoQuyenDichVu));
        OnPropertyChanged(nameof(CoQuyenQuanTri));
        OnPropertyChanged(nameof(CoQuyenGia));
        OnPropertyChanged(nameof(CoQuyenDuyet));
        OnPropertyChanged(nameof(CoQuyenNhatKy));
        OnPropertyChanged(nameof(CoQuyenCaTruc));
        OnPropertyChanged(nameof(CoQuyenBuongPhong));
        OnPropertyChanged(nameof(CoQuyenHoaDon));
        OnPropertyChanged(nameof(CoQuyenDieuHanh));
        OnPropertyChanged(nameof(CoNhomDieuHanh));
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
            BaoCaoViewModel => "d17",
            DoiSoatChuyenKhoanViewModel => "d18",
            DieuHanhViewModel => "d22",
            NhatKyViewModel => "d24",
            CaTrucViewModel => "d27",
            BuongPhongViewModel => "d28",
            BangGiaViewModel => "d25",
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
    // DangNhapViewModel -> DaDangNhap = false -> shell (app-chrome + sidebar) không bao giờ hiện ra và người dùng bị kẹt.
    // Chọn màn hình đầu tiên mà QUYỀN của vai trò cho phép (Quản lý -> bảng điều hành; Lễ tân -> Bảng điều hành ca trực; Buồng phòng -> sơ đồ phòng; Kế toán -> tài chính).
    private void DieuHuongSauDangNhap()
    {
        BatDauTheoDoiCanhBao();
        // Quản lý (có cả báo cáo lẫn quyền Quản lý nghiệp vụ) bắt đầu ở bảng điều hành: thấy ngay tình hình và cảnh báo.
        if (CoQuyenDieuHanh && (CoQuyenDuyet || LaKeToan)) HienThiDieuHanh();   // Kế toán cũng vào Tổng quan điều hành (bản số liệu tiền)
        // Lễ tân bắt đầu ca ở Bảng điều hành (D27): thấy ngay khách đến/đi, phòng, cọc, công nợ và việc cần xử lý.
        else if (CoQuyenCaTruc && CoQuyenLeTan) HienThiCaTruc();
        else if (CoQuyenPhong) HienThiPhong();
        else if (CoQuyenXemPhong) HienThiDashboard();
        else if (CoQuyenBaoCao) HienThiBaoCao();
        else if (CoQuyenThanhToan) HienThiHoaDon();
        else if (CoQuyenDoiSoat) HienThiDoiSoat();
        else if (CoQuyenQuanTri) HienThiPhanQuyen();
        else if (CoQuyenKhachHang) HienThiKhachHang();
        else if (CoQuyenDatPhong) HienThiQuanLyDatPhong();
        else HienThiDoiMatKhau();   // vai trò chưa được cấp chức năng nào: vẫn có màn hình để đổi mật khẩu/đăng xuất
    }

    // ----- Điều hành (Quản lý) -----
    [RelayCommand]
    private void HienThiDieuHanh()
    {
        if (!CoQuyenDieuHanh) return;
        var manHinh = new DieuHanhViewModel(_api, LaKeToan);
        manHinh.MoManHinh += MoTheoCanhBao;
        manHinh.DaTaiDanhSachCanhBao += canhBaos => ChayTrenUi(() => CapNhatSoCanhBao(canhBaos));
        CurrentViewModel = manHinh;
    }

    [RelayCommand] private void HienThiNhatKy() { if (!CoQuyenNhatKy) return; var nk = new NhatKyViewModel(_api); nk.MoManHinh += MoTheoCanhBao; CurrentViewModel = nk; }
    [RelayCommand] private void HienThiBangGia() { if (CoQuyenGia) CurrentViewModel = new BangGiaViewModel(_api); }

    /// <summary>Bấm "Mở" ở một cảnh báo: chuyển tới màn hình xử lý tương ứng (nếu vai trò có quyền vào màn hình đó).</summary>
    private void MoTheoCanhBao(string manHinh)
    {
        switch (manHinh)
        {
            case "PHONG": if (CoQuyenPhong) HienThiPhong(); else HienThiDashboard(); break;
            case "BUONG_PHONG": HienThiBuongPhong(); break;
            case "LUU_TRU": HienThiDashboard(); break;
            case "DAT_PHONG": HienThiQuanLyDatPhong(); break;
            case "HOA_DON": if (CoQuyenHoaDon) HienThiHoaDon(); else HienThiBaoCao(); break;
            case "DOI_SOAT": HienThiDoiSoat(); break;
            case "SAO_LUU": HienThiSaoLuu(); break;
            case "NHAN_VIEN": HienThiPhanQuyen(); break;
        }
    }

    // ----- Cảnh báo trên thanh trên: cập nhật mỗi phút cho người có quyền xem báo cáo -----
    private void BatDauTheoDoiCanhBao()
    {
        DungTheoDoiCanhBao();
        if (!CoQuyenDieuHanh) return;
        _ = LamMoiCanhBaoAsync();
        _dongHoCanhBao = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _dongHoCanhBao.Tick += (_, _) => _ = LamMoiCanhBaoAsync();
        _dongHoCanhBao.Start();
    }

    private void DungTheoDoiCanhBao()
    {
        _dongHoCanhBao?.Stop();
        _dongHoCanhBao = null;
        SoCanhBao = 0;
        SoCanhBaoNghiemTrong = 0;
        ThongBaoCanhBaoMoi = "";
        _canhBaoLanTruoc = null;
    }

    private async Task LamMoiCanhBaoAsync()
    {
        if (!DaDangNhap || !CoQuyenDieuHanh) return;
        try
        {
            var canhBao = await _api.GetCanhBaoAsync();
            CapNhatSoCanhBao(canhBao.CanhBaoDangCo);
        }
        catch (LoiYeuCauApi) { /* giữ số cũ */ }
        catch (System.Net.Http.HttpRequestException) { }
    }

    [RelayCommand]
    private void HienThiKhachHang()
    {
        if (!CoQuyenKhachHang) return;
        var khach = new QuanLyKhachHangViewModel(_api);
        khach.MoXuLy += MoTuTongQuanDieuHanh;   // việc làm ngay từ hồ sơ khách: dịch vụ / đổi phòng / trả phòng / nhận phòng / thu tiền / đặt phòng
        CurrentViewModel = khach;
    }
    [RelayCommand]
    private void HienThiCaTruc()
    {
        if (!CoQuyenCaTruc) return;
        var tongQuan = new CaTrucViewModel(_api);
        tongQuan.MoManHinh += MoTuTongQuanDieuHanh;
        CurrentViewModel = tongQuan;
    }

    /// <summary>Từ Tổng quan điều hành (lễ tân): mở thẳng màn hình xử lý với đúng booking / lượt ở / phòng / hóa đơn đã chọn (vẫn kiểm tra quyền; API kiểm tra lại ở từng thao tác).</summary>
    private void MoTuTongQuanDieuHanh(YeuCauMoManHinh y)
    {
        switch (y.ManHinh)
        {
            case "DAT_PHONG":
                if (!CoQuyenDatPhong) return;
                var datPhong = new QuanLyDatPhongViewModel(_api, CoQuyenLeTan, CoQuyenDuyet);
                datPhong.MoXuLy += MoTuTongQuanDieuHanh;
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
                if (!CoQuyenXemPhong) return;
                var soDo = new DashboardViewModel(_api, y.MaPhong);
                soDo.MoManHinh += MoTheoCanhBao;
                soDo.MoXuLy += MoTuTongQuanDieuHanh;
                CurrentViewModel = soDo;
                break;
            case "BUONG_PHONG":
                HienThiBuongPhong();
                break;
            case "HOA_DON":
                if (!CoQuyenHoaDon) return;
                var dsHoaDon = new HoaDonViewModel(_api, y.TuKhoa);
                dsHoaDon.MoThanhToan += maHoaDon => CurrentViewModel = new ThanhToanViewModel(_api, maHoaDon);
                CurrentViewModel = dsHoaDon;
                break;
        }
    }
    [RelayCommand] private void HienThiBuongPhong() { if (CoQuyenBuongPhong) CurrentViewModel = new BuongPhongViewModel(_api); }
    [RelayCommand]
    private void HienThiDashboard()
    {
        if (!CoQuyenXemPhong) return;
        var soDo = new DashboardViewModel(_api);
        soDo.MoManHinh += MoTheoCanhBao;   // nút "Mở Buồng phòng" / "Mở Phiếu đặt phòng" trên chi tiết phòng
        soDo.MoXuLy += MoTuTongQuanDieuHanh;   // nút trả phòng / gia hạn / nhận phòng với đúng khách và booking của phòng đang chọn
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
        datPhong.MoXuLy += MoTuTongQuanDieuHanh;   // nút "Nhận phòng" / "Trả phòng" ngay trên chi tiết phiếu
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
            DungTheoDoiCanhBao();
            _session.Clear();
            _api.ClearAccessToken();
            CurrentViewModel = TaoManHinhDangNhap();
        };
        CurrentViewModel = manHinh;
    }
    [RelayCommand] private void HienThiPhanQuyen() { if (CoQuyenQuanTri) CurrentViewModel = new PhanQuyenViewModel(_api); }
    [RelayCommand] private void HienThiSaoLuu() { if (CoQuyenQuanTri) CurrentViewModel = new SaoLuuViewModel(_api); }
    [RelayCommand] private void HienThiDoiSoat() { if (CoQuyenDoiSoat) CurrentViewModel = new DoiSoatChuyenKhoanViewModel(_api); }
    [RelayCommand]
    private void HienThiBaoCao()
    {
        if (!CoQuyenBaoCao) return;
        var baoCao = new BaoCaoViewModel(_api) { AnTabVanHanh = LaKeToan };   // Kế toán không xem báo cáo vận hành buồng phòng / bảo trì / lễ tân
        baoCao.MoManHinh += MoTheoCanhBao;   // nút "Mở màn hình" ở tab Vấn đề cần chú ý
        CurrentViewModel = baoCao;
    }

    // "← Đăng xuất" (bản thiết kế D2-D17): gọi API để thu hồi refresh token, xoá phiên làm việc,
    // rồi quay về màn hình đăng nhập D1 - trước đây chưa có cách nào thoát khỏi phiên đã đăng nhập.
    [RelayCommand]
    private async Task DangXuatAsync()
    {
        try { await _api.LogoutAsync(); }
        catch (LoiYeuCauApi) { /* best-effort: vẫn đăng xuất phía client kể cả khi gọi API thất bại */ }

        DungTheoDoiCanhBao();
        _session.Clear();
        _api.ClearAccessToken();
        CurrentViewModel = TaoManHinhDangNhap();
    }
}
