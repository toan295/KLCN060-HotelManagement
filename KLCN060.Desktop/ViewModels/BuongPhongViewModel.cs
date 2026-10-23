using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Hộp thoại xác nhận cho thao tác khó hoàn tác; kiểm thử thay bằng hàm trả sẵn true/false để không bị treo chờ bấm.</summary>
public static class HopThoai
{
    public static Func<string, string, bool> XacNhan { get; set; } = (tieuDe, noiDung) =>
        System.Windows.MessageBox.Show(noiDung, tieuDe, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
}

/// <summary>Tiện ích lọc bảng dùng chung cho các tab buồng phòng (lọc phía máy khách trên danh sách đã tải, không cần gọi lại API).</summary>
public static class LocBang
{
    /// <summary>Gắn bộ lọc vào danh sách; gọi <c>Refresh()</c> trên kết quả mỗi khi tiêu chí đổi.</summary>
    public static System.ComponentModel.ICollectionView Gan<T>(ObservableCollection<T> nguon, Func<T, bool> khop)
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(nguon);
        view.Filter = o => o is T t && khop(t);
        return view;
    }

    /// <summary>Tìm không phân biệt hoa thường và không cần gõ dấu tiếng Việt; từ khóa rỗng = khớp tất cả.</summary>
    public static bool Chua(string? tuKhoa, params string?[] truong)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa)) return true;
        var k = CaTrucViewModel.BoDau(tuKhoa.Trim());
        return truong.Any(t => !string.IsNullOrEmpty(t) && CaTrucViewModel.BoDau(t).Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    public static int Dem(System.ComponentModel.ICollectionView view) => view.Cast<object>().Count();

    public static string TomTat(int hienThi, int tong, string donVi)
        => hienThi == tong ? $"{tong} {donVi}" : $"Hiển thị {hienThi} / {tong} {donVi} (đang lọc)";
}

/// <summary>Thông báo thành công / lỗi của một tab (dùng chung cho các ViewModel con của D28).</summary>
public abstract partial class TabBuongPhongViewModel : ObservableObject
{
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;

    // Mỗi lần chỉ một lượt tải dữ liệu chạy (bấm "Tải lại" liên tiếp / vừa mở tab vừa làm mới không được xen kẽ Clear/Add trên cùng danh sách).
    private readonly SemaphoreSlim _khoaTai = new(1, 1);
    protected async Task TaiTuanTuAsync(Func<Task> tai)
    {
        await _khoaTai.WaitAsync();
        try { await tai(); }
        finally { _khoaTai.Release(); }
    }

    protected void Loi(string noiDung) { ThongBao = noiDung; ThongBaoLaLoi = true; }
    protected void Ok(string noiDung) { ThongBao = noiDung; ThongBaoLaLoi = false; }
}

/// <summary>
/// D28 - Buồng phòng: việc dọn phòng, báo sự cố trong phòng, kiểm đồ dùng / minibar, đồ khách bỏ quên (Lost &amp; Found); quản lý thêm danh mục đồ dùng và xem báo cáo;
/// lễ tân xử lý minibar chờ tính phí. Mỗi tab chỉ hiện khi tài khoản có quyền tương ứng (XEM_PHONG / GHI_NHAN_BUONG_PHONG / THAO_TAC_LE_TAN / QUAN_LY_PHONG / XEM_BAO_CAO).
/// </summary>
public partial class BuongPhongViewModel : ObservableObject, IDisposable
{
    private readonly System.Windows.Threading.DispatcherTimer _dongHo;
    private readonly AppSession _session = AppSession.Current;

    // Quyền chốt một lần lúc mở màn hình (quyền thật vẫn do API kiểm tra ở từng thao tác).
    public bool CoTabViecDon { get; }
    public bool CoTabSuCo { get; }
    public bool CoTabKiemPhong { get; }
    public bool CoTabMinibar { get; }
    public bool CoTabDoBoQuen { get; }
    public bool CoTabDanhMuc { get; }
    public bool CoTabBaoCao { get; }
    public bool CoTabTongQuan { get; }
    public bool CoTabNhatKy { get; }

    /// <summary>
    /// Quản lý điều hành buồng phòng: GIAO việc, KIỂM TRA chất lượng, xử lý sự cố, khóa/mở phòng, xem báo cáo. Quản lý không tự nhận dọn phòng, không báo sự cố,
    /// không tự kiểm đồ dùng/minibar trong phòng - các việc tại phòng là của nhân viên buồng phòng (API cũng chặn các thao tác này với Quản lý).
    /// </summary>
    public bool LaQuanLy { get; }

    /// <summary>Lễ tân (có thao tác quầy, không phải Quản lý): chỉ thấy Minibar chờ tính phí và Đồ bỏ quên (chỉ đọc).</summary>
    public bool LaLeTan { get; }

    public string MoTaManHinh => LaLeTan
        ? "Minibar do Buồng phòng ghi nhận đang chờ tính vào folio khách (tính phí hoặc bỏ qua có lý do) và đồ khách bỏ quên (chỉ xem thông tin)."
        : "Việc dọn phòng, báo sự cố trong phòng, kiểm đồ dùng / minibar và đồ khách bỏ quên.";

    public ViecDonPhongViewModel? ViecDon { get; }
    public SuCoPhongViewModel? SuCo { get; }
    public KiemPhongViewModel? KiemPhong { get; }
    public MinibarChoTinhPhiViewModel? Minibar { get; }
    public DoBoQuenViewModel? DoBoQuen { get; }
    public DanhMucVatDungViewModel? DanhMuc { get; }
    public BaoCaoBuongPhongViewModel? BaoCao { get; }
    public TongQuanBuongPhongViewModel? TongQuan { get; }
    public NhatKyBuongPhongViewModel? NhatKy { get; }

    [ObservableProperty] private string tabDangChon = "";

    public BuongPhongViewModel(ApiClient api)
    {
        LaQuanLy = _session.CoQuyen(QuyenHeThong.DuyetNghiepVu);
        // Lễ tân KHÔNG tham gia dọn phòng, sự cố phòng, kiểm đồ dùng/minibar trong phòng và nhật ký buồng phòng: các phần đó là của Buồng phòng (nhận việc),
        // Quản lý (giao việc, xử lý sự cố, nhật ký). Lễ tân chỉ còn: minibar chờ tính phí vào folio và xem đồ bỏ quên (chỉ đọc).
        LaLeTan = _session.CoQuyen(QuyenHeThong.ThaoTacLeTan) && !LaQuanLy;
        CoTabViecDon = _session.CoQuyen(QuyenHeThong.XemPhong) && !LaQuanLy && !LaLeTan;     // "Việc dọn phòng" là hàng đợi của nhân viên; Quản lý giao việc ở tab Tổng quan
        CoTabSuCo = (_session.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) || _session.CoQuyen(QuyenHeThong.XuLySuCo)) && !LaLeTan;
        CoTabKiemPhong = _session.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) && !LaQuanLy && !LaLeTan;
        CoTabMinibar = _session.CoQuyen(QuyenHeThong.ThaoTacLeTan) || _session.CoQuyen(QuyenHeThong.DuyetNghiepVu);
        CoTabDoBoQuen = _session.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) || _session.CoQuyen(QuyenHeThong.ThaoTacLeTan);
        CoTabDanhMuc = _session.CoQuyen(QuyenHeThong.QuanLyPhong);
        // Báo cáo / tổng quan / nhật ký buồng phòng là công cụ điều hành của Quản lý (kế toán chỉ làm nghiệp vụ kế toán nên không xem).
        CoTabBaoCao = LaQuanLy && _session.CoQuyen(QuyenHeThong.XemBaoCao);
        CoTabTongQuan = CoTabBaoCao && _session.CoQuyen(QuyenHeThong.XemPhong);     // bảng điều hành của Quản lý
        CoTabNhatKy = CoTabBaoCao;    // nhật ký buồng phòng chỉ hiển thị ở tài khoản Quản lý
        if (CoTabViecDon) ViecDon = new ViecDonPhongViewModel(api);
        if (CoTabSuCo) SuCo = new SuCoPhongViewModel(api);
        if (CoTabKiemPhong) KiemPhong = new KiemPhongViewModel(api);
        if (CoTabMinibar) Minibar = new MinibarChoTinhPhiViewModel(api);
        if (CoTabDoBoQuen) DoBoQuen = new DoBoQuenViewModel(api);
        if (CoTabDanhMuc) DanhMuc = new DanhMucVatDungViewModel(api);
        if (CoTabBaoCao) BaoCao = new BaoCaoBuongPhongViewModel(api);
        if (CoTabTongQuan) TongQuan = new TongQuanBuongPhongViewModel(api, ChonTab);
        if (CoTabNhatKy) NhatKy = new NhatKyBuongPhongViewModel(api);
        if (ViecDon is not null)
        {
            ViecDon.BaoSuCoChoPhong += ma => { if (SuCo is null) return; SuCo.PhongBao = SuCo.Phongs.FirstOrDefault(p => p.MaPhong == ma); ChonTab("SU_CO"); };
            ViecDon.KiemDoDungChoPhong += ma => { if (KiemPhong is null) return; ChonTab("KIEM_PHONG"); KiemPhong.PhongDaChon = KiemPhong.Phongs.FirstOrDefault(p => p.MaPhong == ma); };
        }
        // Số liệu buồng phòng đổi liên tục (buồng phòng dọn xong, minibar mới): tự tải lại tab đang xem, các tab này giữ nguyên dòng đang chọn.
        _dongHo = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _dongHo.Tick += (_, _) =>
        {
            switch (TabDangChon)
            {
                case "VIEC_DON": _ = ViecDon?.TaiCommand.ExecuteAsync(null); break;
                case "MINIBAR": _ = Minibar?.TaiCommand.ExecuteAsync(null); break;
                case "TONG_QUAN": _ = TongQuan?.TaiCommand.ExecuteAsync(null); break;
            }
        };
        _dongHo.Start();
        tabDangChon = CoTabTongQuan ? "TONG_QUAN" : CoTabViecDon ? "VIEC_DON" : CoTabSuCo ? "SU_CO" : CoTabMinibar ? "MINIBAR" : CoTabDoBoQuen ? "DO_BO_QUEN" : CoTabDanhMuc ? "DANH_MUC" : CoTabBaoCao ? "BAO_CAO" : "";
    }

    public void Dispose() => _dongHo.Stop();

    [RelayCommand] private void ChonTab(string tab) => TabDangChon = tab;

    partial void OnTabDangChonChanged(string value)
    {
        // Chuyển tab thì làm mới dữ liệu của tab đó (số liệu buồng phòng thay đổi liên tục giữa các ca).
        switch (value)
        {
            case "TONG_QUAN": _ = TongQuan?.TaiCommand.ExecuteAsync(null); break;
            case "NHAT_KY": _ = NhatKy?.TaiCommand.ExecuteAsync(null); break;
            case "VIEC_DON": _ = ViecDon?.TaiCommand.ExecuteAsync(null); break;
            case "SU_CO": _ = SuCo?.TaiCommand.ExecuteAsync(null); break;
            case "MINIBAR": _ = Minibar?.TaiCommand.ExecuteAsync(null); break;
            case "DO_BO_QUEN": _ = DoBoQuen?.TaiCommand.ExecuteAsync(null); break;
        }
    }
}

// ============================================================ Việc dọn phòng

public partial class ViecDonPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;
    private readonly string _tenDN = AppSession.Current.TenDangNhap ?? "";
    private readonly bool _coQuyenDuyet = AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);

    public bool CoQuyenXacNhan { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XacNhanDonPhong);
    public ObservableCollection<ViecDonPhongDto> Viec { get; } = [];
    [ObservableProperty] private ViecDonPhongDto? viecDaChon;
    [ObservableProperty] private string tomTat = "";
    [ObservableProperty] private string capNhatLuc = "";

    /// <summary>Tình hình toàn bộ phòng theo vòng đời buồng phòng: trả lời "phòng nào nhận khách được" mà không cần mở sơ đồ phòng.</summary>
    [ObservableProperty] private string tinhHinhPhong = "";
    [ObservableProperty] private string tuKhoaLoc = "";
    [ObservableProperty] private bool chiPhongGiuChoKhach;
    [ObservableProperty] private string locViec = "";
    [ObservableProperty] private string soHienThi = "";

    public static IReadOnlyList<LuaChon> LocViecTuyChon { get; } =
    [
        new("", "Tất cả việc dọn"), new("CUA_TOI", "Việc của tôi (được giao / đang dọn)"), new("CHUA_AI", "Chưa ai nhận"), new("DANG_DON", "Đang có người dọn")
    ];

    public bool CoQuyenBaoSuCo { get; } = AppSession.Current.CoQuyen(QuyenHeThong.GhiNhanBuongPhong);
    public bool CoViecDaChon => ViecDaChon is not null;

    /// <summary>Lễ tân bấm "Báo sự cố phòng này" / "Kiểm đồ dùng phòng này": chuyển sang tab tương ứng với phòng đã chọn sẵn.</summary>
    public event Action<string>? BaoSuCoChoPhong;
    public event Action<string>? KiemDoDungChoPhong;

    public ViecDonPhongViewModel(ApiClient api)
    {
        _api = api;
        System.Windows.Data.CollectionViewSource.GetDefaultView(Viec).Filter = o => o is ViecDonPhongDto v
            && (!ChiPhongGiuChoKhach || v.KhachDenHomNay)
            && (LocViec switch { "CUA_TOI" => v.NguoiDon == _tenDN, "CHUA_AI" => v.NguoiDon is null, "DANG_DON" => v.NguoiDon is not null && v.BatDauDonLuc is not null, _ => true })
            && (string.IsNullOrWhiteSpace(TuKhoaLoc) || new[] { v.TenPhong, v.TenLoaiPhong, v.LoaiViecHienThi, v.NguoiDonHienThi, v.GhiChuHienThi }
                .Any(t => CaTrucViewModel.BoDau(t ?? "").Contains(CaTrucViewModel.BoDau(TuKhoaLoc.Trim()), StringComparison.OrdinalIgnoreCase)));
        _ = TaiAsync();
    }

    partial void OnTuKhoaLocChanged(string value) => System.Windows.Data.CollectionViewSource.GetDefaultView(Viec).Refresh();
    partial void OnChiPhongGiuChoKhachChanged(bool value) => System.Windows.Data.CollectionViewSource.GetDefaultView(Viec).Refresh();
    partial void OnLocViecChanged(string value) => System.Windows.Data.CollectionViewSource.GetDefaultView(Viec).Refresh();

    [RelayCommand] private void XoaLoc() { TuKhoaLoc = ""; ChiPhongGiuChoKhach = false; LocViec = ""; }

    [RelayCommand] private void BaoSuCo() { if (ViecDaChon is not null) BaoSuCoChoPhong?.Invoke(ViecDaChon.MaPhong); }
    [RelayCommand] private void KiemDoDung() { if (ViecDaChon is not null) KiemDoDungChoPhong?.Invoke(ViecDaChon.MaPhong); }

    // Nhận dọn phòng chưa ai nhận, hoặc bắt đầu dọn phòng Quản lý đã giao cho chính mình.
    public bool CoTheNhan => CoQuyenXacNhan && ViecDaChon is { } v && (v.NguoiDon is null || (v.BatDauDonLuc is null && v.NguoiDon == _tenDN));
    public string NutNhan => ViecDaChon is { NguoiDon: not null, BatDauDonLuc: null } ? "Bắt đầu dọn (việc được giao)" : "Nhận dọn phòng này";
    public bool CoTheBoNhan => ViecDaChon is { NguoiDon: not null } v && (v.NguoiDon == _tenDN || _coQuyenDuyet);
    public bool CoTheXacNhanXong => CoQuyenXacNhan && ViecDaChon is not null;
    public string NutXacNhanXong => ViecDaChon?.TinhTrang == "OD" ? "Đã dọn xong (phòng có khách)" : "Đã dọn xong (phòng sạch)";

    partial void OnViecDaChonChanged(ViecDonPhongDto? value)
    {
        OnPropertyChanged(nameof(CoViecDaChon));
        OnPropertyChanged(nameof(CoTheNhan));
        OnPropertyChanged(nameof(NutNhan));
        OnPropertyChanged(nameof(CoTheBoNhan));
        OnPropertyChanged(nameof(CoTheXacNhanXong));
        OnPropertyChanged(nameof(NutXacNhanXong));
    }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            var ma = ViecDaChon?.MaPhong;
            var bang = await _api.GetViecDonPhongAsync();
            Viec.Clear();
            foreach (var v in bang.Viec) Viec.Add(v);
            ViecDaChon = ma is null ? null : Viec.FirstOrDefault(x => x.MaPhong == ma);
            SoHienThi = $"{bang.Viec.Count} phòng cần dọn";
            TinhHinhPhong = $"Tổng {bang.TongPhong} phòng = {bang.PhongSanSang} sẵn sàng nhận khách ({bang.PhongSanSang - bang.PhongDaKiemTra} đã dọn + {bang.PhongDaKiemTra} đã kiểm tra) + {bang.PhongCoKhach} đang có khách + {bang.TongCanDon} cần dọn + {bang.PhongBaoTri} bảo trì";
            TomTat = $"{bang.TongCanDon} phòng cần dọn ({bang.PhongTrongCanDon} trống, {bang.PhongCoKhachCanDon} có khách) · {bang.PhongKhan} khẩn · {bang.DangDon} đang có người dọn · hôm nay đã dọn {bang.DaDonHomNay} (bạn {bang.DaDonHomNayCuaToi})";
            CapNhatLuc = $"Cập nhật {DateTime.Now:HH:mm:ss}";
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task NhanDonAsync()
    {
        if (ViecDaChon is null) { Loi("Chọn phòng cần dọn trước."); return; }
        try
        {
            var ma = ViecDaChon.MaPhong;
            await _api.NhanDonPhongAsync(ma);
            await TaiAsync();
            Ok($"Bạn đang dọn phòng {ma} (đã ghi giờ bắt đầu).");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }

    [RelayCommand]
    private async Task BoNhanAsync()
    {
        if (ViecDaChon is null) { Loi("Chọn phòng trước."); return; }
        try
        {
            var ma = ViecDaChon.MaPhong;
            await _api.BoNhanDonPhongAsync(ma);
            await TaiAsync();
            Ok($"Đã bỏ nhận dọn phòng {ma}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }

    /// <summary>Xác nhận đã dọn xong: phòng trống VD → VC, phòng có khách OD → OC (chỉ buồng phòng/quản lý - quyền XAC_NHAN_DON_PHONG).</summary>
    [RelayCommand]
    private async Task XacNhanDonXongAsync()
    {
        if (ViecDaChon is null) { Loi("Chọn phòng vừa dọn xong trước."); return; }
        try
        {
            var ma = ViecDaChon.MaPhong;
            await _api.CapNhatTinhTrangPhongAsync(ma, ViecDaChon.TinhTrang == "OD" ? "OC" : "VC", "Dọn xong");
            await TaiAsync();
            Ok($"Phòng {ma} đã được xác nhận dọn xong.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }
}

// ============================================================ Sự cố phòng

public partial class SuCoPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;
    private readonly string _tenDN = AppSession.Current.TenDangNhap ?? "";

    public static IReadOnlyList<LuaChon> DanhSachLoai { get; } =
    [
        new("DIEU_HOA", "Điều hòa"), new("DIEN_CHIEU_SANG", "Điện / chiếu sáng"), new("NUOC", "Nước / đường ống"), new("TV_THIET_BI_DIEN_TU", "TV / thiết bị điện tử"),
        new("NOI_THAT", "Nội thất"), new("THIEU_TRANG_THIET_BI", "Thiếu trang thiết bị"), new("VE_SINH", "Vệ sinh"), new("KHAC", "Khác")
    ];
    public static IReadOnlyList<LuaChon> DanhSachMucDo { get; } = [new("THAP", "Thấp"), new("TRUNG_BINH", "Trung bình"), new("CAO", "Cao"), new("KHAN_CAP", "Khẩn cấp")];

    public static IReadOnlyList<LuaChon> LocMucDoTuyChon { get; } = [new("", "Mọi mức độ"), .. DanhSachMucDo];
    public static IReadOnlyList<LuaChon> LocLoaiTuyChon { get; } = [new("", "Mọi loại sự cố"), .. DanhSachLoai];
    public static IReadOnlyList<LuaChon> LocTrangThaiTuyChon { get; } =
    [
        new("", "Mọi trạng thái"), new("MOI", "Mới báo"), new("DA_TIEP_NHAN", "Đã tiếp nhận"), new("DANG_XU_LY", "Đang xử lý"), new("DA_XU_LY", "Đã xử lý"), new("DA_HUY", "Đã hủy")
    ];

    public bool CoQuyenXuLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XuLySuCo);
    /// <summary>Chỉ nhân viên buồng phòng / lễ tân báo sự cố; Quản lý là người tiếp nhận và xử lý nên không có biểu mẫu báo.</summary>
    public bool CoQuyenBao => !CoQuyenXuLy;
    public ObservableCollection<SuCoPhongDto> DanhSach { get; } = [];
    private readonly System.ComponentModel.ICollectionView _viewSuCo;
    [ObservableProperty] private string tuKhoaSuCo = "";
    [ObservableProperty] private string locMucDo = "";
    [ObservableProperty] private string locLoaiSuCo = "";
    [ObservableProperty] private string locTrangThaiSuCo = "";
    [ObservableProperty] private string soSuCoHienThi = "";
    public ObservableCollection<PhongDto> Phongs { get; } = [];

    [ObservableProperty] private SuCoPhongDto? suCoDaChon;
    [ObservableProperty] private bool chiDangMo = true;
    [ObservableProperty] private PhongDto? phongBao;
    [ObservableProperty] private LuaChon? loaiBao;
    [ObservableProperty] private LuaChon? mucDoBao;
    [ObservableProperty] private string tieuDeBao = "";
    [ObservableProperty] private string moTaBao = "";
    [ObservableProperty] private string ghiChuXuLy = "";
    [ObservableProperty] private string tomTat = "";

    public SuCoPhongViewModel(ApiClient api)
    {
        _api = api;
        // Biểu mẫu báo sự cố bắt đầu trống: người báo tự chọn loại và mức độ (không điền sẵn dữ liệu mẫu).
        _viewSuCo = LocBang.Gan(DanhSach, (SuCoPhongDto x) => LocBang.Chua(TuKhoaSuCo, x.MaHienThi, x.TenPhong, x.TieuDe, x.MoTa, x.NguoiBaoHienThi, x.NguoiXuLyHienThi)
            && (LocMucDo == "" || x.MucDo == LocMucDo) && (LocLoaiSuCo == "" || x.Loai == LocLoaiSuCo) && (LocTrangThaiSuCo == "" || x.TrangThai == LocTrangThaiSuCo));
        _ = TaiAsync();
    }

    partial void OnTuKhoaSuCoChanged(string value) => LamMoiLoc();
    partial void OnLocMucDoChanged(string value) => LamMoiLoc();
    partial void OnLocLoaiSuCoChanged(string value) => LamMoiLoc();
    partial void OnLocTrangThaiSuCoChanged(string value) => LamMoiLoc();
    private void LamMoiLoc() { _viewSuCo.Refresh(); SoSuCoHienThi = LocBang.TomTat(LocBang.Dem(_viewSuCo), DanhSach.Count, "sự cố"); }
    [RelayCommand] private void XoaLoc() { TuKhoaSuCo = ""; LocMucDo = ""; LocLoaiSuCo = ""; LocTrangThaiSuCo = ""; }

    partial void OnChiDangMoChanged(bool value) { _ = TaiAsync(); }
    partial void OnSuCoDaChonChanged(SuCoPhongDto? value)
    {
        OnPropertyChanged(nameof(CoSuCoDaChon));
        OnPropertyChanged(nameof(CoTheTiepNhan));
        OnPropertyChanged(nameof(CoTheXuLy));
        OnPropertyChanged(nameof(CoTheXong));
        OnPropertyChanged(nameof(CoTheMoLai));
        OnPropertyChanged(nameof(CoTheHuy));
    }

    // Quản lý: tiếp nhận (mới) → đang xử lý → đã xử lý (ghi cách xử lý); mở lại; hủy. Người báo chỉ hủy được sự cố báo nhầm khi chưa ai tiếp nhận.
    public bool CoSuCoDaChon => SuCoDaChon is not null;
    public bool CoTheTiepNhan => CoQuyenXuLy && SuCoDaChon is { TrangThai: "MOI" };
    public bool CoTheXuLy => CoQuyenXuLy && SuCoDaChon is { TrangThai: "MOI" or "DA_TIEP_NHAN" };
    public bool CoTheXong => CoQuyenXuLy && SuCoDaChon is { TrangThai: "MOI" or "DA_TIEP_NHAN" or "DANG_XU_LY" };
    public bool CoTheMoLai => CoQuyenXuLy && SuCoDaChon is { TrangThai: "DA_XU_LY" };
    public bool CoTheHuy => SuCoDaChon is { DangMo: true } s && (CoQuyenXuLy || (s.TrangThai == "MOI" && s.NguoiBao == _tenDN));

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            if (Phongs.Count == 0)
                foreach (var p in (await _api.GetRoomsAsync()).OrderBy(x => x.TenPhong, StringComparer.Ordinal)) Phongs.Add(p);
            var ma = SuCoDaChon?.MaSuCo;
            var ds = await _api.GetSuCoAsync(ChiDangMo ? "DANG_MO" : null);
            DanhSach.Clear();
            foreach (var x in ds) DanhSach.Add(x);
            SuCoDaChon = ma is null ? null : DanhSach.FirstOrDefault(x => x.MaSuCo == ma);
            LamMoiLoc();
            var mucCao = ds.Count(x => x.DangMo && x.MucDo is "CAO" or "KHAN_CAP");
            TomTat = $"{ds.Count(x => x.DangMo)} sự cố chưa xử lý xong" + (mucCao > 0 ? $" · {mucCao} mức cao/khẩn cấp" : "");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task BaoAsync()
    {
        if (PhongBao is null) { Loi("Chọn phòng có sự cố."); return; }
        if (LoaiBao is null || MucDoBao is null) { Loi("Chọn loại sự cố và mức độ."); return; }
        if (string.IsNullOrWhiteSpace(TieuDeBao)) { Loi("Nhập tiêu đề sự cố (ngắn gọn, nêu hỏng hóc gì)."); return; }
        try
        {
            var kq = await _api.BaoSuCoAsync(new YeuCauBaoSuCo(PhongBao.MaPhong, LoaiBao.Ma, TieuDeBao.Trim(), string.IsNullOrWhiteSpace(MoTaBao) ? null : MoTaBao.Trim(), MucDoBao.Ma));
            TieuDeBao = "";
            MoTaBao = "";
            await TaiAsync();
            SuCoDaChon = DanhSach.FirstOrDefault(x => x.MaSuCo == kq.MaSuCo);
            Ok($"Đã gửi sự cố {kq.MaHienThi}: phòng {kq.TenPhong} - {kq.TieuDe} (mức {kq.MucDoHienThi.ToLowerInvariant()}). Quản lý sẽ tiếp nhận.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task DoiTrangThaiAsync(string trangThai)
    {
        if (SuCoDaChon is null) { Loi("Chọn sự cố trước."); return; }
        var ghiChu = string.IsNullOrWhiteSpace(GhiChuXuLy) ? null : GhiChuXuLy.Trim();
        if (trangThai is "DA_XU_LY" && ghiChu is null) { Loi("Nhập cách đã xử lý sự cố vào ô ghi chú."); return; }
        if (trangThai is "DA_HUY" && ghiChu is null) { Loi("Nhập lý do hủy sự cố vào ô ghi chú."); return; }
        if (trangThai is "DANG_XU_LY" && SuCoDaChon.TrangThai == "DA_XU_LY" && ghiChu is null) { Loi("Nhập lý do mở lại sự cố (chưa khắc phục được) vào ô ghi chú."); return; }
        try
        {
            var ma = SuCoDaChon.MaSuCo;
            var kq = await _api.DoiTrangThaiSuCoAsync(ma, trangThai, ghiChu);
            GhiChuXuLy = "";
            await TaiAsync();
            Ok($"Sự cố {kq.MaHienThi}: {kq.TrangThaiHienThi}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }
}
