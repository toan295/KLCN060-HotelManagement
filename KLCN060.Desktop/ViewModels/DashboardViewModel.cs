using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Một ô chỉ số trên đầu sơ đồ phòng; bấm vào để lọc nhanh các phòng thuộc nhóm đó (Loc = mã bộ lọc, "" = bỏ lọc).</summary>
public sealed record ChiSoSoDo(string Nhan, string GiaTri, string Phu, string Loc, string MucDo = "");

/// <summary>
/// D2 - Sơ đồ trạng thái phòng. Nhìn một màn hình biết ngay tình hình khách sạn: ô chỉ số (công suất, trống sạch / bẩn, có khách, bảo trì, phòng cần xử lý),
/// sơ đồ theo tầng tô màu, khách đang ở / khách sắp đến ngay trên ô phòng. Chọn phòng → vì sao đang ở tình trạng này, ai đổi lúc nào, khách / đặt phòng / sự cố liên quan,
/// việc tiếp theo; người có quyền đổi tình trạng thao tác ngay (có cảnh báo khi ảnh hưởng đặt phòng, không ghi đè thay đổi của người khác). Tự làm mới mỗi 30 giây.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ApiClient _api;
    private readonly System.Windows.Threading.DispatcherTimer _dongHo;
    private List<PhongDto> _tatCa = [];
    private int _lanTaiChiTiet;

    public ObservableCollection<PhongDto> DanhSachPhong { get; } = [];
    public ObservableCollection<string> TrangThaiDuocPhep { get; } = [];
    public ObservableCollection<ChiSoSoDo> ChiSo { get; } = [];

    /// <summary>Sơ đồ phòng gom theo tầng (mỗi tầng một hàng ô phòng) để nhìn tình trạng từng tầng.</summary>
    public ObservableCollection<TangPhongViewModel> PhongTheoTang { get; } = [];

    public static IReadOnlyList<LuaChon> BoLocTuyChon { get; } =
    [
        new("", "Tất cả phòng"), new("CAN_XU_LY", "Chỉ phòng cần xử lý (có cảnh báo)"), new("VC", "Trống, đã dọn (VC)"), new("VD", "Trống, chưa dọn (VD)"),
        new("VI", "Trống, đã kiểm tra (VI)"), new("OC", "Đang có khách (OC)"), new("OD", "Có khách, cần dọn (OD)"), new("OOO", "Ngừng phục vụ / bảo trì (OOO)")
    ];

    public static IReadOnlyList<LuaChon> LocBanTuyChon { get; } =
    [
        new("", "Mọi khả năng bán"), new("BAN_DUOC", "Bán được ngay"), new("DA_GIU", "Đã giữ cho khách đến"), new("KHONG_BAN", "Không bán được")
    ];
    public static IReadOnlyList<LuaChon> LocKhachTuyChon { get; } =
    [
        new("", "Mọi phòng"), new("DANG_O", "Đang có khách ở"), new("SAP_DEN", "Có khách sắp đến"), new("KHONG_KHACH", "Không có khách")
    ];
    public ObservableCollection<LuaChon> Tangs { get; } = [new("", "Tất cả tầng")];
    public ObservableCollection<LuaChon> LoaiPhongs { get; } = [new("", "Tất cả loại phòng")];
    [ObservableProperty] private string locTang = "";
    [ObservableProperty] private string locLoaiPhong = "";
    [ObservableProperty] private string locBan = "";
    [ObservableProperty] private string locKhach = "";
    [ObservableProperty] private string soHienThi = "";

    [ObservableProperty] private PhongDto? phongDaChon;
    [ObservableProperty] private ChiTietPhongDto? chiTiet;
    [ObservableProperty] private string trangThaiMoi = "";
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private string locTinhTrang = "";
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private string tomTatHienThi = "";
    [ObservableProperty] private bool dangLuu;

    /// <summary>Phiên đã mất kết nối API: sơ đồ đang hiển thị là dữ liệu cũ.</summary>
    [ObservableProperty] private bool duLieuCu;

    public event Action<string>? MoManHinh;

    /// <summary>Mở màn hình xử lý với đúng đối tượng của phòng đang chọn (booking, lượt ở): trả phòng, gia hạn/đổi phòng, nhận phòng...</summary>
    public event Action<YeuCauMoManHinh>? MoXuLy;

    public bool CoQuyenLeTan { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan);
    public bool CoKhachDangOHienTai => CoQuyenLeTan && ChiTiet?.KhachDangO is not null;
    private DatPhongSapToiDto? DatSapToiNhanDuoc => ChiTiet?.DatPhongSapToi.FirstOrDefault(x => x.NgayDon <= DateOnly.FromDateTime(DateTime.Today));
    /// <summary>Phòng đã sẵn sàng và có booking đón hôm nay đã cọc: nhận phòng được ngay.</summary>
    public bool CoTheNhanTheoBooking => CoQuyenLeTan && PhongDaChon?.TinhTrang is "VC" or "VI" && DatSapToiNhanDuoc is { TrangThai: "DA_XAC_NHAN" };
    public bool CoTheNhanVangLai => CoQuyenLeTan && PhongDaChon?.BanDuocNgay == true;
    public bool CoTheMoBooking => CoQuyenMoDatPhong && ChiTiet?.DatPhongSapToi.Count > 0;

    [RelayCommand] private void TraPhongDangO() { if (ChiTiet?.KhachDangO is { } k && PhongDaChon is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("TRA_PHONG", MaPhieuNhan: k.MaPhieuNhan, MaPhong: PhongDaChon.MaPhong)); }
    [RelayCommand] private void GiaHanDoiPhongDangO() { if (ChiTiet?.KhachDangO is { } k && PhongDaChon is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("DOI_PHONG", MaPhieuNhan: k.MaPhieuNhan, MaPhong: PhongDaChon.MaPhong)); }
    [RelayCommand] private void DichVuDangO() { if (ChiTiet?.KhachDangO is { } k && PhongDaChon is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("DICH_VU", MaPhieuNhan: k.MaPhieuNhan, MaPhong: PhongDaChon.MaPhong)); }
    [RelayCommand] private void NhanTheoBooking() { if (DatSapToiNhanDuoc is { } d) MoXuLy?.Invoke(new YeuCauMoManHinh("NHAN_PHONG", MaPhieuDat: d.MaPhieuDat)); }
    [RelayCommand] private void NhanVangLai() { if (PhongDaChon is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("NHAN_PHONG", MaPhong: PhongDaChon.MaPhong)); }
    [RelayCommand] private void MoBookingCuaPhong() { var ma = ChiTiet?.DatPhongSapToi.FirstOrDefault()?.MaPhieuDat ?? ChiTiet?.KhachDangO?.MaPhieuDat; if (ma is not null) MoXuLy?.Invoke(new YeuCauMoManHinh("DAT_PHONG", MaPhieuDat: ma)); }

    /// <summary>Lý do bắt buộc khi khóa bảo trì (OOO) và khi đưa phòng sạch trở lại bẩn (kiểm tra không đạt / phát hiện chưa sạch) để truy vết.</summary>
    public bool CanLyDo => TrangThaiMoi == "OOO" || (TrangThaiMoi == "VD" && PhongDaChon?.TinhTrang is "VC" or "VI");
    public string NhacLyDo => TrangThaiMoi == "OOO" ? "Bắt buộc nhập lý do khi đưa phòng vào bảo trì / khóa phòng (có ghi nhật ký)."
        : "Bắt buộc nhập lý do khi đưa phòng đã dọn trở lại bẩn (VD: còn tóc trong phòng tắm) - có ghi nhật ký.";
    public bool CoThaoTacDuocPhep => TrangThaiDuocPhep.Count > 0;
    partial void OnTrangThaiMoiChanged(string value) { OnPropertyChanged(nameof(CanLyDo)); OnPropertyChanged(nameof(NhacLyDo)); }

    /// <summary>Quản lý điều hành, không tự dọn phòng: phòng bẩn do buồng phòng xác nhận đã dọn (API chặn Quản lý các bước này).</summary>
    public bool LaQuanLy => AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);

    public string ThongTinTinhTrang => PhongDaChon is null ? "" :
        $"{PhongDaChon.TinhTrangHienThi} {PhongDaChon.TuLucHienThi}" + (string.IsNullOrWhiteSpace(PhongDaChon.LyDoTinhTrang) ? "" : $" - {PhongDaChon.LyDoTinhTrang}");

    // Chỉ vai trò có quyền DOI_TINH_TRANG_PHONG đổi được tình trạng phòng; vai trò khác chỉ xem tổng quan,
    // nên ẩn khung "Đổi trạng thái" thay vì để bấm rồi mới báo lỗi.
    // Lễ tân (thao tác quầy, không phải Quản lý) CHỈ XEM sơ đồ phòng: không đổi tình trạng phòng. Dọn/kiểm tra là việc của Buồng phòng; khóa/mở bảo trì là của Quản lý.
    public bool LaLeTan => AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan) && !LaQuanLy;
    public bool CoTheDoiTrangThai => AppSession.Current.CoQuyen(QuyenHeThong.DoiTinhTrangPhong) && !LaLeTan;
    public bool CheDoChiXem => !CoTheDoiTrangThai;
    public string MoTaManHinh => CoTheDoiTrangThai
        ? "Tình trạng từng phòng theo tầng - bấm vào một ô phòng để xem lý do tình trạng, khách / đặt phòng liên quan, lịch sử và đổi trạng thái (nếu được phép)."
        : "Chỉ xem: tình trạng từng phòng theo tầng - bấm vào một ô phòng để xem lý do tình trạng, khách / đặt phòng liên quan và lịch sử. Lễ tân không đổi tình trạng phòng ở đây.";
    public string GhiChuChiXem => LaLeTan
        ? "Chế độ chỉ xem: lễ tân không đổi tình trạng phòng. Phòng bẩn do Buồng phòng dọn và kiểm tra; khóa / mở phòng bảo trì do Quản lý quyết định. Cần xử lý khách thì dùng nút \"Xử lý nhanh\" bên trên."
        : "Tài khoản này chỉ được xem tình trạng phòng (đổi tình trạng cần quyền riêng).";

    // Xác nhận "đã dọn / đã kiểm tra" (VD→VC, VC→VI, OD→OC) là việc của buồng phòng/quản lý (XAC_NHAN_DON_PHONG); lễ tân không đổi tình trạng phòng.
    private static bool LaXacNhanDon(string? cu, string moi) => moi is "VC" or "VI" || (cu == "OD" && moi == "OC");
    public bool CoTheXacNhanDon => AppSession.Current.CoQuyen(QuyenHeThong.XacNhanDonPhong);

    // Khóa phòng bảo trì / mở lại phòng bảo trì (OOO) cần quyền KHOA_MO_PHONG (chỉ quản lý): buồng phòng phát hiện hỏng hóc thì báo sự cố ở mục Buồng phòng.
    private static bool LaKhoaMo(string? cu, string moi) => cu == "OOO" || moi == "OOO";
    public bool CoTheKhoaMo => AppSession.Current.CoQuyen(QuyenHeThong.KhoaMoPhong);
    public bool CoQuyenMoBuongPhong => AppSession.Current.CoQuyen(QuyenHeThong.XemPhong) && (AppSession.Current.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) || AppSession.Current.CoQuyen(QuyenHeThong.XemBaoCao));
    public bool CoQuyenMoDatPhong => AppSession.Current.CoQuyen(QuyenHeThong.XemDatPhong);

    public string GoiYXacNhanDon =>
        CoTheDoiTrangThai && LaQuanLy && PhongDaChon?.TinhTrang is "VD" or "OD" ? "Phòng đang chờ dọn: quản lý không tự xác nhận dọn xong. Hãy giao việc cho nhân viên buồng phòng ở mục Buồng phòng, sau đó kiểm tra chất lượng."
        : CoTheDoiTrangThai && !CoTheXacNhanDon && PhongDaChon?.TinhTrang is "VD" or "OD" ? "Chỉ buồng phòng xác nhận phòng đã dọn xong. Phòng bảo trì do quản lý quyết định."
        : CoTheDoiTrangThai && !CoTheKhoaMo && PhongDaChon?.TinhTrang == "OOO" ? "Phòng đang bảo trì: chỉ quản lý mở lại. Buồng phòng phát hiện hỏng hóc hãy báo sự cố ở mục Buồng phòng."
        : CoTheDoiTrangThai && !CoTheKhoaMo && PhongDaChon is not null ? "Muốn khóa phòng vì hỏng hóc? Hãy báo sự cố ở mục Buồng phòng - quản lý sẽ quyết định khóa phòng."
        : "";
    public bool CoGoiYXacNhanDon => !string.IsNullOrEmpty(GoiYXacNhanDon);

    /// <summary>Gọi khi quyền của phiên vừa được làm mới (API vừa từ chối một thao tác): cập nhật khung "Đổi trạng thái"/"Chế độ xem".</summary>
    public void LamMoiQuyen()
    {
        OnPropertyChanged(nameof(CoTheDoiTrangThai));
        OnPropertyChanged(nameof(CheDoChiXem));
        OnPropertyChanged(nameof(CoTheXacNhanDon));
        OnPropertyChanged(nameof(CoTheKhoaMo));
        OnPropertyChanged(nameof(GoiYXacNhanDon));
        OnPropertyChanged(nameof(CoGoiYXacNhanDon));
    }

    public bool CoChiTiet => ChiTiet is not null;
    public bool CoCanhBaoPhong => !string.IsNullOrEmpty(ChiTiet?.CanhBao);
    public bool CoKhachDangO => ChiTiet?.KhachDangO is not null;
    public bool CoDatPhongSapToi => ChiTiet?.DatPhongSapToi.Count > 0;
    public bool CoSuCo => ChiTiet?.SuCo.Count > 0;
    public bool CoNguoiDangDon => !string.IsNullOrEmpty(ChiTiet?.NguoiDangDon);

    public DashboardViewModel(ApiClient api, string? maPhongMoSan = null)
    {
        _api = api;
        _ = TaiLanDauAsync(maPhongMoSan);
        // Dữ liệu phòng đổi liên tục (lễ tân, buồng phòng thao tác ở máy khác): tự tải lại để sơ đồ không bị cũ.
        _dongHo = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _dongHo.Tick += (_, _) => { if (!DangLuu) _ = TaiDuLieuAsync(); };
        _dongHo.Start();
    }

    /// <summary>Tải lần đầu; nếu được mở từ Tổng quan điều hành thì chọn sẵn phòng cần xem (hiện chi tiết, lịch sử, lý do tình trạng).</summary>
    private async Task TaiLanDauAsync(string? maPhongMoSan)
    {
        await TaiDuLieuAsync();
        if (maPhongMoSan is null) return;
        PhongDaChon = _tatCa.FirstOrDefault(x => x.MaPhong == maPhongMoSan);
        if (PhongDaChon is null) DatThongBao($"Không tìm thấy phòng {maPhongMoSan}.", true);
    }

    public void Dispose() => _dongHo.Stop();

    partial void OnLocTinhTrangChanged(string value) => AplBoLoc();
    partial void OnTuKhoaChanged(string value) => AplBoLoc();
    partial void OnLocTangChanged(string value) => AplBoLoc();
    partial void OnLocLoaiPhongChanged(string value) => AplBoLoc();
    partial void OnLocBanChanged(string value) => AplBoLoc();
    partial void OnLocKhachChanged(string value) => AplBoLoc();

    partial void OnChiTietChanged(ChiTietPhongDto? value)
    {
        OnPropertyChanged(nameof(CoKhachDangOHienTai));
        OnPropertyChanged(nameof(CoTheNhanTheoBooking));
        OnPropertyChanged(nameof(CoTheNhanVangLai));
        OnPropertyChanged(nameof(CoTheMoBooking));
        OnPropertyChanged(nameof(CoChiTiet));
        OnPropertyChanged(nameof(CoCanhBaoPhong));
        OnPropertyChanged(nameof(CoKhachDangO));
        OnPropertyChanged(nameof(CoDatPhongSapToi));
        OnPropertyChanged(nameof(CoSuCo));
        OnPropertyChanged(nameof(CoNguoiDangDon));
    }

    private string? _maDaNap;
    private string? _tinhTrangDaNap;

    partial void OnPhongDaChonChanged(PhongDto? value)
    {
        OnPropertyChanged(nameof(ThongTinTinhTrang));
        OnPropertyChanged(nameof(CanLyDo));
        OnPropertyChanged(nameof(NhacLyDo));
        OnPropertyChanged(nameof(CoTheNhanTheoBooking));
        OnPropertyChanged(nameof(CoTheNhanVangLai));
        // Làm mới tự động trả về bản ghi mới của CÙNG phòng, CÙNG tình trạng: chỉ cập nhật thông tin hiển thị, không reset lựa chọn / lý do người dùng đang nhập.
        if (value is not null && value.MaPhong == _maDaNap && value.TinhTrang == _tinhTrangDaNap)
        {
            _ = TaiChiTietAsync(value);
            return;
        }
        _maDaNap = value?.MaPhong;
        _tinhTrangDaNap = value?.TinhTrang;
        ChiTiet = null;
        _ = TaiChiTietAsync(value);
        TrangThaiDuocPhep.Clear();
        foreach (var trangThai in value?.TinhTrang switch
        {
            "OC" => new[] { "OD" }, "OD" => string.IsNullOrEmpty(value.KhachHienTai) ? new[] { "OC", "VD" } : new[] { "OC" }, "VD" => new[] { "VC", "OOO" },
            "VC" => new[] { "VI", "VD", "OOO" }, "VI" => new[] { "VD", "OOO" }, "OOO" => new[] { "VD", "VC" }, _ => []
        }) if ((CoTheXacNhanDon || !LaXacNhanDon(value?.TinhTrang, trangThai)) && (CoTheKhoaMo || !LaKhoaMo(value?.TinhTrang, trangThai))
                && !(LaQuanLy && value?.TinhTrang is "VD" or "OD" && trangThai is "VC" or "VI" or "OC")) TrangThaiDuocPhep.Add(trangThai);
        TrangThaiMoi = TrangThaiDuocPhep.FirstOrDefault() ?? "";
        OnPropertyChanged(nameof(CoThaoTacDuocPhep));
        OnPropertyChanged(nameof(GoiYXacNhanDon));
        OnPropertyChanged(nameof(CoGoiYXacNhanDon));
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            var ma = PhongDaChon?.MaPhong;
            _tatCa = await _api.GetRoomsAsync();
            CapNhatLuaChonLoc();
            DuLieuCu = false;
            CapNhatLuc = $"Cập nhật {DateTime.Now:HH:mm:ss}";
            TinhChiSo();
            AplBoLoc();
            if (ma is not null)
            {
                // Giữ phòng đang chọn trỏ tới bản ghi mới nhất; nếu tình trạng đã bị người khác đổi thì thông tin / thao tác được nạp lại theo tình trạng thật.
                var moi = DanhSachPhong.FirstOrDefault(x => x.MaPhong == ma) ?? _tatCa.FirstOrDefault(x => x.MaPhong == ma);
                if (moi is not null) PhongDaChon = moi;
            }
        }
        catch (LoiYeuCauApi exception)
        {
            DuLieuCu = _tatCa.Count > 0;
            DatThongBao(DuLieuCu ? $"Không làm mới được sơ đồ ({exception.Message}). Đang hiển thị dữ liệu lúc {CapNhatLuc.Replace("Cập nhật ", "")}." : exception.Message, true);
        }
        catch (System.Net.Http.HttpRequestException)
        {
            DuLieuCu = _tatCa.Count > 0;
            DatThongBao("Mất kết nối tới máy chủ. Sơ đồ đang hiển thị dữ liệu cũ - bấm Tải lại khi có mạng.", true);
        }
    }

    private void TinhChiSo()
    {
        var tong = _tatCa.Count;
        int Dem(string tt) => _tatCa.Count(x => x.TinhTrang == tt);
        var vc = Dem("VC"); var vd = Dem("VD"); var vi = Dem("VI"); var oc = Dem("OC"); var od = Dem("OD"); var ooo = Dem("OOO");
        var banDuoc = tong - ooo;
        var congSuat = banDuoc <= 0 ? 0 : (oc + od) * 100.0 / banDuoc;
        var canXuLy = _tatCa.Count(x => x.CoCanhBao);
        var nguyHiem = _tatCa.Count(x => x.LaNguyHiem);
        ChiSo.Clear();
        ChiSo.Add(new("CÔNG SUẤT", $"{congSuat:0}%", $"{oc + od}/{banDuoc} phòng bán được đang có khách", ""));
        var daGiu = _tatCa.Count(x => x.DaGiuChoKhachDen);
        ChiSo.Add(new("TRỐNG, SẠCH", $"{vc + vi}", $"{vc} đã dọn · {vi} đã kiểm tra · {daGiu} đã giữ cho khách đến hôm nay → {vc + vi - daGiu} bán được cho khách vãng lai", "SAN_SANG", "TOT"));
        ChiSo.Add(new("TRỐNG, BẨN", $"{vd}", "chờ buồng phòng dọn", "VD", vd > 0 ? "CANH_BAO" : ""));
        ChiSo.Add(new("CÓ KHÁCH", $"{oc + od}", $"{od} phòng cần dọn", "CO_KHACH"));
        ChiSo.Add(new("BẢO TRÌ", $"{ooo}", ooo > 0 ? "đang khóa, không bán được" : "không có", "OOO", ooo > 0 ? "CANH_BAO" : ""));
        ChiSo.Add(new("CẦN XỬ LÝ", $"{canXuLy}", nguyHiem > 0 ? $"{nguyHiem} phòng ảnh hưởng khách sắp đến" : "phòng có cảnh báo", "CAN_XU_LY", nguyHiem > 0 ? "NGUY_HIEM" : canXuLy > 0 ? "CANH_BAO" : "TOT"));
        TomTatHienThi = $"{tong} phòng = {vc + vi} trống sạch ({vc + vi - daGiu} bán được + {daGiu} đã giữ) + {vd} chờ dọn + {oc + od} có khách + {ooo} bảo trì";
    }

    /// <summary>Danh sách tầng và loại phòng của bộ lọc lấy từ chính dữ liệu đang có; giữ lựa chọn cũ nếu còn.</summary>
    private void CapNhatLuaChonLoc()
    {
        var chonTang = LocTang; var chonLoai = LocLoaiPhong;
        Tangs.Clear(); Tangs.Add(new("", "Tất cả tầng"));
        foreach (var t in _tatCa.Select(x => x.Tang).Distinct().OrderBy(x => x)) Tangs.Add(new(t.ToString(), $"Tầng {t}"));
        LoaiPhongs.Clear(); LoaiPhongs.Add(new("", "Tất cả loại phòng"));
        foreach (var l in _tatCa.Select(x => x.TenLoaiPhong).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) LoaiPhongs.Add(new(l, l));
        LocTang = Tangs.Any(x => x.Ma == chonTang) ? chonTang : "";
        LocLoaiPhong = LoaiPhongs.Any(x => x.Ma == chonLoai) ? chonLoai : "";
    }

    private void AplBoLoc()
    {
        IEnumerable<PhongDto> q = _tatCa;
        q = LocTinhTrang switch
        {
            "" => q,
            "CAN_XU_LY" => q.Where(x => x.CoCanhBao),
            "SAN_SANG" => q.Where(x => x.TinhTrang is "VC" or "VI"),
            "CO_KHACH" => q.Where(x => x.TinhTrang is "OC" or "OD"),
            var tt => q.Where(x => x.TinhTrang == tt)
        };
        if (!string.IsNullOrEmpty(LocTang)) q = q.Where(x => x.Tang.ToString() == LocTang);
        if (!string.IsNullOrEmpty(LocLoaiPhong)) q = q.Where(x => x.TenLoaiPhong == LocLoaiPhong);
        q = LocBan switch
        {
            "BAN_DUOC" => q.Where(x => x.BanDuocNgay),
            "DA_GIU" => q.Where(x => x.DaGiuChoKhachDen),
            "KHONG_BAN" => q.Where(x => !x.BanDuocNgay && !x.DaGiuChoKhachDen),
            _ => q
        };
        q = LocKhach switch
        {
            "DANG_O" => q.Where(x => x.CoKhachDangO),
            "SAP_DEN" => q.Where(x => x.CoKhachSapDen),
            "KHONG_KHACH" => q.Where(x => !x.CoKhachDangO && !x.CoKhachSapDen),
            _ => q
        };
        if (!string.IsNullOrWhiteSpace(TuKhoa))
        {
            // Không phân biệt hoa thường và không cần gõ dấu (gõ "nguyen" vẫn ra "Nguyễn").
            var k = CaTrucViewModel.BoDau(TuKhoa.Trim());
            bool Khop(string? t) => !string.IsNullOrEmpty(t) && CaTrucViewModel.BoDau(t).Contains(k, StringComparison.OrdinalIgnoreCase);
            q = q.Where(x => Khop(x.TenPhong) || Khop(x.KhachHienTai) || Khop(x.KhachSapDen) || Khop(x.TenLoaiPhong) || Khop(x.MaPhieuDatSapDen));
        }
        var ma = PhongDaChon?.MaPhong;
        DanhSachPhong.Clear();
        foreach (var p in q) DanhSachPhong.Add(p);
        SoHienThi = DanhSachPhong.Count == _tatCa.Count ? $"{_tatCa.Count} phòng" : $"Hiển thị {DanhSachPhong.Count} / {_tatCa.Count} phòng (đang lọc)";
        PhongTheoTang.Clear();
        foreach (var nhom in DanhSachPhong.GroupBy(x => x.Tang).OrderBy(x => x.Key))
            PhongTheoTang.Add(new TangPhongViewModel(nhom.Key, nhom.OrderBy(x => x.TenPhong).ToList(), _tatCa.Count(x => x.Tang == nhom.Key)));
        // Giữ phòng đang chọn trỏ tới bản ghi mới nhất (nếu còn trong danh sách).
        if (ma is not null)
        {
            var moi = DanhSachPhong.FirstOrDefault(x => x.MaPhong == ma);
            if (moi is not null && !ReferenceEquals(moi, PhongDaChon)) PhongDaChon = moi;
        }
    }

    private async Task TaiChiTietAsync(PhongDto? phong)
    {
        if (phong is null) return;
        var lan = ++_lanTaiChiTiet;
        try
        {
            var ct = await _api.GetChiTietPhongAsync(phong.MaPhong);
            if (lan == _lanTaiChiTiet && PhongDaChon?.MaPhong == phong.MaPhong) ChiTiet = ct;
        }
        catch (LoiYeuCauApi) { /* chi tiết chỉ là thông tin bổ sung: sơ đồ vẫn dùng được */ }
        catch (System.Net.Http.HttpRequestException) { }
    }

    [RelayCommand] private void ChonPhong(PhongDto phong) => PhongDaChon = phong;
    [RelayCommand] private void LocNhanh(string loc) => LocTinhTrang = LocTinhTrang == loc ? "" : loc;
    [RelayCommand] private void XoaBoLoc() { LocTinhTrang = ""; TuKhoa = ""; LocTang = ""; LocLoaiPhong = ""; LocBan = ""; LocKhach = ""; }
    [RelayCommand] private void MoBuongPhong() => MoManHinh?.Invoke("BUONG_PHONG");
    [RelayCommand] private void MoDatPhong() => MoManHinh?.Invoke("DAT_PHONG");

    [RelayCommand]
    private async Task CapNhatTrangThaiAsync()
    {
        if (DangLuu || !CoTheDoiTrangThai || PhongDaChon is null || string.IsNullOrEmpty(TrangThaiMoi)) return;
        var phong = PhongDaChon;
        var cu = phong.TinhTrang;
        var moi = TrangThaiMoi;
        // Đưa phòng vào bảo trì / khóa phòng (OOO) bắt buộc ghi lý do để truy vết.
        if (moi == "OOO" && string.IsNullOrWhiteSpace(GhiChu)) { DatThongBao("Cần nhập lý do khi đưa phòng vào bảo trì / khóa phòng.", true); return; }
        if (moi == "VD" && cu is "VC" or "VI" && string.IsNullOrWhiteSpace(GhiChu)) { DatThongBao("Cần nhập lý do khi đưa phòng đã dọn trở lại bẩn (VD: còn tóc trong phòng tắm).", true); return; }

        // Cảnh báo + xác nhận khi thao tác ảnh hưởng đặt phòng / khả năng bán phòng.
        var canhBao = new List<string>();
        var dat = ChiTiet?.MaPhong == phong.MaPhong ? ChiTiet.DatPhongSapToi : [];
        if (moi == "OOO" && dat.Count > 0)
            canhBao.Add("Phòng đang được giữ cho các đặt phòng sau - khóa phòng sẽ khiến không nhận khách được:\n   • " + string.Join("\n   • ", dat.Take(3).Select(d => d.HienThi)) + "\n   Hãy đổi phòng cho khách trước khi khóa.");
        else if (moi == "OOO") canhBao.Add("Phòng sẽ ngừng bán cho tới khi được mở lại.");
        if (moi is "VD" && cu is "VC" or "VI" && !string.IsNullOrEmpty(phong.KhachSapDen))
            canhBao.Add($"Phòng đã sẵn sàng đón khách {phong.KhachSapDen} (đón {phong.NgayDonSapDen:dd/MM}) - chuyển lại thành bẩn sẽ làm trễ việc nhận phòng.");
        if (cu == "OOO" && ChiTiet?.SuCo.Count > 0)
            canhBao.Add($"Phòng còn {ChiTiet.SuCo.Count} sự cố chưa xử lý xong.");
        if (canhBao.Count > 0 && !HopThoai.XacNhan("Xác nhận đổi trạng thái", $"Đổi phòng {phong.TenPhong}: {TrangThaiPhongConverterMoTa(cu)} → {TrangThaiPhongConverterMoTa(moi)}\n\n{string.Join("\n\n", canhBao)}\n\nTiếp tục?"))
            return;

        DangLuu = true;
        try
        {
            await _api.CapNhatTinhTrangPhongAsync(phong.MaPhong, moi, GhiChu, cu);
            GhiChu = "";
            DatThongBao($"Đã đổi phòng {phong.TenPhong} sang {TrangThaiPhongConverterMoTa(moi).ToLowerInvariant()}.", false);
            DangLuu = false;
            await TaiDuLieuAsync();
            PhongDaChon = DanhSachPhong.FirstOrDefault(x => x.MaPhong == phong.MaPhong) ?? _tatCa.FirstOrDefault(x => x.MaPhong == phong.MaPhong);
        }
        catch (LoiYeuCauApi exception)
        {
            DatThongBao(exception.Message, true);
            DangLuu = false;
            await TaiDuLieuAsync();   // có thể người khác vừa đổi: hiện lại tình trạng thật của phòng
        }
        finally { DangLuu = false; }
    }

    private static string TrangThaiPhongConverterMoTa(string? tt) => KLCN060.Desktop.Converters.TrangThaiPhongConverter.MoTa(tt);

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }
}

/// <summary>Một tầng trong sơ đồ phòng: tiêu đề "Tầng N · k phòng" và các ô phòng của tầng.</summary>
public sealed record TangPhongViewModel(int Tang, IReadOnlyList<PhongDto> Phongs, int TongPhongTang = 0)
{
    public string TieuDe => TongPhongTang > Phongs.Count && TongPhongTang > 0
        ? $"Tầng {Tang}  ·  {Phongs.Count}/{TongPhongTang} phòng"
        : $"Tầng {Tang}  ·  {Phongs.Count} phòng"
          + (Phongs.Count(x => x.TinhTrang is "VC" or "VI") is var sanSang and > 0 ? $"  ·  {sanSang} sẵn sàng" : "")
          + (Phongs.Count(x => x.TinhTrang is "VD" or "OD") is var ban and > 0 ? $"  ·  {ban} cần dọn" : "")
          + (Phongs.Count(x => x.TinhTrang == "OOO") is var bt and > 0 ? $"  ·  {bt} bảo trì" : "");
}
