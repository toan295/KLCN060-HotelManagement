using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// Một ô chỉ số của Tổng quan điều hành. Bấm vào ô: nếu có <see cref="Tab"/> thì chuyển tới tab chi tiết, nếu có <see cref="ManHinh"/> thì mở màn hình xử lý.
/// <see cref="GiaiThich"/> cho biết chỉ số đếm cái gì (hiện khi rê chuột) để lễ tân không nhầm giữa các con số gần giống nhau.
/// </summary>
public sealed record ChiSoCaTruc(string Nhan, string GiaTri, string Phu, string MucDo = "", string Tab = "", string ManHinh = "", string GiaiThich = "", string Mau = "")
{
    public bool BamDuoc => Tab != "" || ManHinh != "";
}

/// <summary>Yêu cầu mở một màn hình xử lý với đúng đối tượng đã chọn (booking, lượt lưu trú, phòng, hóa đơn).</summary>
public sealed record YeuCauMoManHinh(string ManHinh, string? MaPhieuDat = null, string? MaPhieuNhan = null, string? MaPhong = null, string? MaHoaDon = null, string? MaKhach = null, string? TuKhoa = null);

/// <summary>
/// D27 - Bảng điều hành (màn hình đầu tiên của lễ tân trong ca): phòng trống / có khách / sắp check-in / sắp check-out / chờ dọn / bảo trì, đặt cọc và công nợ cần thu,
/// không đến, quá hạn trả, minibar/đồ bỏ quên, sự cố; các danh sách khách đến, khách đi, khách đang ở, phòng trống theo ngày, khách không đến.
/// Mỗi cảnh báo và mỗi dòng danh sách có nút mở thẳng màn hình xử lý với đúng đối tượng đã chọn. Tự làm mới định kỳ.
/// Mỗi phần chỉ hiện khi tài khoản có quyền xem đúng loại dữ liệu đó (XEM_DAT_PHONG / XEM_LUU_TRU / XEM_PHONG), không cần quyền báo cáo của Quản lý.
/// </summary>
public partial class CaTrucViewModel : ObservableObject, IDisposable
{
    private const int GiayTuLamMoi = 30;

    private readonly ApiClient _api;
    private readonly System.Windows.Threading.DispatcherTimer _dongHo;
    private int _lanTai;
    private bool _dangTai;

    public bool CoQuyenDat { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XemDatPhong);
    public bool CoQuyenLuuTru { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XemLuuTru);
    public bool CoQuyenPhong { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XemPhong);
    public bool CoQuyenThaoTac { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan);
    public bool CoQuyenThanhToan { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThanhToan);
    public bool CoTongQuan => CoQuyenDat || CoQuyenLuuTru;

    public ObservableCollection<ChiSoCaTruc> ChiSo { get; } = [];
    public ObservableCollection<ViecCanXuLyDto> CanXuLy { get; } = [];
    /// <summary>Việc đang hiển thị: sau khi lọc chỉ lấy một trang đầu (xem thêm từng 20 việc) để danh sách hàng trăm việc vẫn gọn.</summary>
    public ObservableCollection<ViecCanXuLyDto> ViecHienThi { get; } = [];
    private const int SoViecMoiTrang = 20;
    private int _soViecToiDa = SoViecMoiTrang;
    [ObservableProperty] private int viecConLai;
    public bool CoViecConLai => ViecConLai > 0;
    public bool CoTheThuGonViec => _soViecToiDa > SoViecMoiTrang;
    public string NutXemThemViec => $"Xem thêm {Math.Min(SoViecMoiTrang, ViecConLai)} việc (còn {ViecConLai})";
    partial void OnViecConLaiChanged(int value) { OnPropertyChanged(nameof(CoViecConLai)); OnPropertyChanged(nameof(NutXemThemViec)); OnPropertyChanged(nameof(CoTheThuGonViec)); }
    [RelayCommand] private void XemThemViec() { _soViecToiDa += SoViecMoiTrang; CapNhatViecHienThi(); }
    [RelayCommand] private void ThuGonViec() { _soViecToiDa = SoViecMoiTrang; CapNhatViecHienThi(); }

    private bool KhopViec(ViecCanXuLyDto v) => (!ChiViecGap || v.MucDo != "THONG_TIN") && (LocLoaiViec == "" || v.Loai == LocLoaiViec)
        && Khop(v.MoTa, v.MaThamChieu, v.LoaiHienThi, v.MaPhong);

    private void CapNhatViecHienThi()
    {
        var khop = CanXuLy.Where(KhopViec).ToList();
        ViecHienThi.Clear();
        foreach (var v in khop.Take(_soViecToiDa)) ViecHienThi.Add(v);
        ViecConLai = khop.Count - ViecHienThi.Count;
        OnPropertyChanged(nameof(CoTheThuGonViec));
    }
    public ObservableCollection<KhachDenDto> KhachDen { get; } = [];
    public ObservableCollection<KhachDiDto> KhachDi { get; } = [];
    public ObservableCollection<KhachDiDto> KhachDangO { get; } = [];
    public ObservableCollection<PhongTrongDto> PhongTrong { get; } = [];
    public ObservableCollection<PhieuDatTomTatDto> KhongDenDaDanhDau { get; } = [];
    public ObservableCollection<PhieuDatTomTatDto> KhongDenChoXuLy { get; } = [];

    /// <summary>Mở màn hình xử lý với đúng đối tượng (xử lý ở ManHinhChinhViewModel, nơi kiểm tra quyền và chuyển màn hình).</summary>
    public event Action<YeuCauMoManHinh>? MoManHinh;

    [ObservableProperty] private string tabDangChon = "TONG_QUAN";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool duLieuCu;
    [ObservableProperty] private DateTime ngayXem = DateTime.Today;
    [ObservableProperty] private DateTime phongTrongTu = DateTime.Today;
    [ObservableProperty] private DateTime phongTrongDen = DateTime.Today.AddDays(1);
    [ObservableProperty] private string tomTatKhachDen = "";
    [ObservableProperty] private string tomTatKhachDi = "";
    [ObservableProperty] private string tomTatPhongTrong = "";
    [ObservableProperty] private string tomTatKhongDen = "";
    [ObservableProperty] private string tuKhoaLoc = "";
    [ObservableProperty] private bool chiViecGap;
    // Bộ lọc nâng cao theo từng danh sách (kết hợp với ô "Tìm nhanh" và mọi lọc chạy phía máy khách trên dữ liệu đã tải).
    [ObservableProperty] private string locLoaiViec = "";
    [ObservableProperty] private string locKhachDen = "";
    [ObservableProperty] private string locKhachDi = "";
    [ObservableProperty] private string locDangO = "";
    [ObservableProperty] private string locPhongTrong = "";
    [ObservableProperty] private string soHienThi = "";

    public ObservableCollection<LuaChon> LoaiViecTuyChon { get; } = [new("", "Mọi loại việc")];
    public static IReadOnlyList<LuaChon> LocKhachDenTuyChon { get; } =
    [
        new("", "Tất cả phiếu đến"), new("NHAN_NGAY", "Nhận được ngay"), new("CHO_PHONG", "Chờ phòng sẵn sàng"), new("CHUA_COC", "Chưa thu cọc"), new("DA_NHAN", "Đã nhận phòng")
    ];
    public static IReadOnlyList<LuaChon> LocKhachDiTuyChon { get; } =
    [
        new("", "Tất cả phòng trả"), new("CON_O", "Còn ở (chưa trả)"), new("QUA_HAN", "Quá hạn trả"), new("DA_TRA", "Đã trả")
    ];
    public static IReadOnlyList<LuaChon> LocDangOTuyChon { get; } =
    [
        new("", "Tất cả khách đang ở"), new("QUA_HAN", "Quá hạn trả"), new("TRA_HOM_NAY", "Trả hôm nay"), new("O_TIEP", "Còn ở sau hôm nay")
    ];
    public static IReadOnlyList<LuaChon> LocPhongTrongTuyChon { get; } =
    [
        new("", "Mọi phòng trống"), new("NHAN_NGAY", "Nhận ngay được (đã dọn)"), new("CHUA_DON", "Chưa dọn")
    ];
    [ObservableProperty] private bool dangTai;

    public bool CoViecCanXuLy => CanXuLy.Count > 0;
    public string TomTatViec { get; private set; } = "";

    public CaTrucViewModel(ApiClient api)
    {
        _api = api;
        CanXuLy.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CoViecCanXuLy));
        TabDangChon = CoTongQuan ? "TONG_QUAN" : CoQuyenPhong ? "PHONG_TRONG" : "TONG_QUAN";
        CaiBoLoc();
        _ = TaiAsync();
        // Tình hình ca thay đổi liên tục (máy khác nhận/trả phòng, buồng phòng dọn xong): tự làm mới để số liệu không cũ.
        _dongHo = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(GiayTuLamMoi) };
        _dongHo.Tick += (_, _) => { if (!_dangTai) _ = TaiAsync(); };
        _dongHo.Start();
    }

    public void Dispose() => _dongHo.Stop();

    [RelayCommand] private void ChonTab(string tab) { if (!string.IsNullOrEmpty(tab)) TabDangChon = tab; }

    partial void OnNgayXemChanged(DateTime value) { _ = TaiKhachDenDiAsync(); }
    partial void OnTuKhoaLocChanged(string value) => LamMoiBoLoc();
    partial void OnChiViecGapChanged(bool value) => LamMoiBoLoc();
    partial void OnLocLoaiViecChanged(string value) => LamMoiBoLoc();
    partial void OnLocKhachDenChanged(string value) => LamMoiBoLoc();
    partial void OnLocKhachDiChanged(string value) => LamMoiBoLoc();
    partial void OnLocDangOChanged(string value) => LamMoiBoLoc();
    partial void OnLocPhongTrongChanged(string value) => LamMoiBoLoc();
    partial void OnTabDangChonChanged(string value)
    {
        CapNhatSoHienThi();
        OnPropertyChanged(nameof(LocKhachDenHien)); OnPropertyChanged(nameof(LocKhachDiHien)); OnPropertyChanged(nameof(LocDangOHien));
        OnPropertyChanged(nameof(LocPhongTrongHien)); OnPropertyChanged(nameof(LocTongQuanHien));
    }
    // Ô lọc nâng cao nào hiện thì tùy tab đang xem.
    public bool LocKhachDenHien => TabDangChon == "KHACH_DEN";
    public bool LocKhachDiHien => TabDangChon == "KHACH_DI";
    public bool LocDangOHien => TabDangChon == "DANG_O";
    public bool LocPhongTrongHien => TabDangChon == "PHONG_TRONG";
    public bool LocTongQuanHien => TabDangChon == "TONG_QUAN";

    [RelayCommand] private void XoaLoc() { TuKhoaLoc = ""; ChiViecGap = false; LocLoaiViec = ""; LocKhachDen = ""; LocKhachDi = ""; LocDangO = ""; LocPhongTrong = ""; }

    /// <summary>Tải lại tất cả các phần mà tài khoản có quyền xem (mỗi phần độc lập: một phần lỗi không làm mất các phần còn lại; chỉ lần tải mới nhất được hiển thị).</summary>
    [RelayCommand]
    private async Task TaiAsync()
    {
        if (_dangTai) return;
        _dangTai = true;
        DangTai = true;
        var lan = ++_lanTai;
        var loi = false;
        try
        {
            if (CoTongQuan) loi |= !await TaiTongQuanAsync(lan);
            loi |= !await TaiKhachDenDiAsync();
            if (CoQuyenLuuTru) loi |= !await TaiDangOAsync();
            if (CoQuyenDat) loi |= !await TaiKhongDenAsync();
            if (CoQuyenPhong) loi |= !await TimPhongTrongCoLoiAsync();
        }
        finally
        {
            _dangTai = false;
            DangTai = false;
        }

        CapNhatSoHienThi();
        if (loi)
        {
            // Giữ số liệu cũ nhưng nói rõ là cũ, để lễ tân không ra quyết định theo số đã lỗi thời.
            DuLieuCu = true;
            CapNhatLuc = $"Dữ liệu cũ - lần tải lúc {DateTime.Now:HH:mm:ss} lỗi, bấm Tải lại";
        }
        else
        {
            DuLieuCu = false;
            ThongBao = "";
            CapNhatLuc = $"Cập nhật {DateTime.Now:HH:mm:ss} · tự làm mới mỗi {GiayTuLamMoi} giây";
        }
    }

    private async Task<bool> TaiTongQuanAsync(int lan)
    {
        try
        {
            var t = await _api.GetTongQuanCaTrucAsync();
            if (lan != _lanTai) return true;
            ChiSo.Clear();
            ChiSo.Add(new("BÁN ĐƯỢC NGAY", $"{t.PhongTrongBanDuoc}", $"trong {t.PhongTrong} phòng trống sạch · {t.PhongGiuChoKhachDen} đã giữ cho khách đến hôm nay",
                t.PhongTrongBanDuoc == 0 ? "CHU_Y" : "", Tab: CoQuyenPhong ? "PHONG_TRONG" : "",
                GiaiThich: "Phòng đã dọn/kiểm tra (VC, VI) và chưa được giữ cho booking đến hôm nay: nhận được khách vãng lai ngay. Phòng trống sạch nhưng đã giữ cho booking thì KHÔNG tính ở đây.", Mau: "XANH"));
            ChiSo.Add(new("PHÒNG CÓ KHÁCH", $"{t.PhongDangCoKhach}", $"{t.KhachDangLuuTru} lượt lưu trú" + (t.PhongCoKhachCanDon > 0 ? $" · {t.PhongCoKhachCanDon} cần dọn" : ""),
                Tab: CoQuyenLuuTru ? "DANG_O" : "",
                GiaiThich: "Đếm theo TÌNH TRẠNG PHÒNG (OC + OD). Số lượt lưu trú đếm theo phiếu nhận (mỗi phòng một lượt).", Mau: "CHINH"));
            ChiSo.Add(new("SẮP CHECK-IN HÔM NAY", $"{t.PhongSapCheckIn}", $"{t.BookingChoNhanPhong} booking chờ nhận / {t.BookingDenHomNay} booking đến hôm nay",
                t.BookingChoNhanPhong > 0 ? "CHU_Y" : "", Tab: CoQuyenDat ? "KHACH_DEN" : "",
                GiaiThich: "Đếm PHÒNG chưa nhận của các booking có ngày đón hôm nay (còn hiệu lực). Số booking đếm theo PHIẾU nên có thể nhỏ hơn số phòng khi đặt nhiều phòng.", Mau: "DUONG"));
            ChiSo.Add(new("SẮP CHECK-OUT HÔM NAY", $"{t.PhongSapCheckOut}", $"{t.PhongDaTraHomNay} phòng đã trả hôm nay",
                Tab: CoQuyenLuuTru ? "KHACH_DI" : "",
                GiaiThich: "Phòng khách còn ở có ngày trả dự kiến là hôm nay (giờ trả phòng chuẩn 12:00). KHÔNG gồm phòng quá hạn trả (đếm riêng ở ô Quá hạn trả) và phòng đã trả.", Mau: "VANG"));
            ChiSo.Add(new("QUÁ HẠN TRẢ PHÒNG", $"{t.PhongQuaHanTra}", t.PhongQuaHanTra > 0 ? "khách còn ở sau ngày trả dự kiến (overstay)" : "không có",
                t.PhongQuaHanTra > 0 ? "CANH_BAO" : "", Tab: CoQuyenLuuTru ? "KHACH_DI" : "",
                GiaiThich: "Lượt lưu trú đang ở có ngày trả dự kiến đã qua. Cần trả phòng hoặc gia hạn.", Mau: "DO"));
            ChiSo.Add(new("PHÒNG CHỜ DỌN", $"{t.PhongChoDon}", "trống nhưng chưa dọn" + (t.PhongDangDon > 0 ? $" · {t.PhongDangDon} phòng đang có người dọn" : ""),
                t.PhongChoDon > 0 ? "CHU_Y" : "", ManHinh: CoQuyenPhong ? "SO_DO_PHONG" : "",
                GiaiThich: "Phòng trống tình trạng VD (chưa dọn). Chỉ buồng phòng xác nhận dọn xong, lễ tân chỉ theo dõi. Phòng có khách cần dọn (OD) nằm trong ô Phòng có khách.", Mau: "CAM"));
            ChiSo.Add(new("BẢO TRÌ / SỰ CỐ", $"{t.PhongBaoTri}", $"phòng bảo trì trên tổng {t.TongSoPhong} phòng · {t.SuCoDangMo} sự cố đang mở",
                t.PhongBaoTri > 0 ? "CHU_Y" : "", ManHinh: CoQuyenPhong ? "SO_DO_PHONG" : "",
                GiaiThich: "Phòng bảo trì (OOO) không bán được. Sự cố đang mở là báo hỏng chưa xử lý xong, có thể chưa khóa phòng.", Mau: "XAM"));
            ChiSo.Add(new("CỌC CHỜ THU", $"{t.DatCocChoThu}", $"{t.TongTienCocChoThu:N0}đ · booking đón hôm nay hoặc ngày mai chưa xác nhận cọc",
                t.DatCocChoThu > 0 ? "CHU_Y" : "", Tab: "TONG_QUAN",
                GiaiThich: "Booking ở trạng thái Chờ xác nhận cọc có ngày đón hôm nay/ngày mai. Chưa cọc thì không nhận phòng được.", Mau: "NGOC"));
            ChiSo.Add(new("CÒN PHẢI THU", $"{t.HoaDonConNo}", $"{t.TongConPhaiThu:N0}đ trên {t.HoaDonConNo} hóa đơn còn nợ",
                t.HoaDonConNo > 0 ? "CHU_Y" : "", ManHinh: CoQuyenThanhToan ? "THANH_TOAN" : "",
                GiaiThich: "Hóa đơn có Tổng tiền lớn hơn tổng các lần đã thu (cùng cách tính với màn Hóa đơn). Đã trừ tiền cọc trong Tổng tiền.", Mau: "TIM"));
            ChiSo.Add(new("BOOKING CẦN XỬ LÝ", $"{t.BookingCanXuLy}", "chờ cọc · quá ngày đón · phòng giữ chưa sẵn sàng (mỗi phiếu đếm một lần)",
                t.BookingCanXuLy > 0 ? "CANH_BAO" : "", Tab: "TONG_QUAN",
                GiaiThich: "Số PHIẾU ĐẶT có ít nhất một việc cần xử lý. Một phiếu có cả chờ cọc và phòng chưa sẵn sàng vẫn chỉ đếm một lần.", Mau: "HONG"));
            ChiSo.Add(new("KHÔNG ĐẾN CHỜ XỬ LÝ", $"{t.KhongDenChoXuLy}", "booking quá ngày đón, chưa nhận phòng, chưa đánh dấu",
                t.KhongDenChoXuLy > 0 ? "CANH_BAO" : "", Tab: CoQuyenDat ? "KHONG_DEN" : "",
                GiaiThich: "Booking còn hiệu lực nhưng ngày đón đã qua mà khách chưa nhận phòng. Đánh dấu không đến sẽ giữ cọc theo chính sách.", Mau: "NAU"));
            ChiSo.Add(new("MINIBAR / ĐỒ BỎ QUÊN", $"{t.MinibarChoTinhPhi + t.DoBoQuenDangLuu}", $"{t.MinibarChoTinhPhi} khoản minibar chờ tính phí · {t.DoBoQuenDangLuu} món đồ bỏ quên",
                t.MinibarChoTinhPhi > 0 ? "CHU_Y" : "", ManHinh: "BUONG_PHONG",
                GiaiThich: "Minibar do buồng phòng ghi nhận nhưng chưa tính vào folio sẽ chặn trả phòng. Đồ bỏ quên là món Buồng phòng đã ghi nhận, chưa trả khách (lễ tân chỉ xem).", Mau: "INDIGO"));

            TomTatViec = $"{t.CanXuLy.Count(x => x.MucDo == "NGHIEM_TRONG")} nghiêm trọng · {t.CanXuLy.Count(x => x.MucDo == "CANH_BAO")} cảnh báo · {t.CanXuLy.Count(x => x.MucDo == "THONG_TIN")} thông tin";
            OnPropertyChanged(nameof(TomTatViec));

            var dangChon = LocLoaiViec;
            LoaiViecTuyChon.Clear();
            LoaiViecTuyChon.Add(new("", "Mọi loại việc"));
            foreach (var l in t.CanXuLy.GroupBy(x => x.Loai).Select(g => g.First()).OrderBy(x => x.LoaiHienThi, StringComparer.CurrentCulture)) LoaiViecTuyChon.Add(new(l.Loai, l.LoaiHienThi));
            CanXuLy.Clear();
            foreach (var v in t.CanXuLy) CanXuLy.Add(v);
            LocLoaiViec = LoaiViecTuyChon.Any(x => x.Ma == dangChon) ? dangChon : "";
            CapNhatViecHienThi();
            CapNhatSoHienThi();
            return true;
        }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); return false; }
        catch (System.Net.Http.HttpRequestException) { DatThongBao("Mất kết nối tới máy chủ. Số liệu đang hiển thị có thể đã cũ.", true); return false; }
    }

    private async Task<bool> TaiKhachDenDiAsync()
    {
        var ngay = DateOnly.FromDateTime(NgayXem);
        var ok = true;
        if (CoQuyenDat)
        {
            try
            {
                var ds = await _api.GetKhachDenAsync(ngay);
                KhachDen.Clear();
                foreach (var x in ds) KhachDen.Add(x);
                TomTatKhachDen = $"{ds.Count} phiếu đến ngày {ngay:dd/MM/yyyy} · {ds.Count(x => x.CoTheNhanNgay)} nhận được ngay · {ds.Count(x => x.TrangThai == "CHO_XAC_NHAN")} chờ cọc";
            }
            catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); ok = false; }
            catch (System.Net.Http.HttpRequestException) { ok = false; }
        }
        if (CoQuyenLuuTru)
        {
            try
            {
                var ds = await _api.GetKhachDiAsync(ngay);
                KhachDi.Clear();
                foreach (var x in ds) KhachDi.Add(x);
                TomTatKhachDi = $"{ds.Count(x => x.TrangThai == "DANG_O")} phòng còn ở (dự kiến trả ngày {ngay:dd/MM/yyyy}, kể cả quá hạn nếu là hôm nay) · {ds.Count(x => x.TrangThai == "DA_TRA_PHONG")} phòng đã trả";
            }
            catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); ok = false; }
            catch (System.Net.Http.HttpRequestException) { ok = false; }
        }
        return ok;
    }

    private async Task<bool> TaiDangOAsync()
    {
        try
        {
            var ds = await _api.GetKhachDangOAsync();
            KhachDangO.Clear();
            foreach (var x in ds) KhachDangO.Add(x);
            return true;
        }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); return false; }
        catch (System.Net.Http.HttpRequestException) { return false; }
    }

    private async Task<bool> TaiKhongDenAsync()
    {
        try
        {
            var bc = await _api.GetKhongDenAsync();
            KhongDenDaDanhDau.Clear();
            foreach (var x in bc.DaDanhDau) KhongDenDaDanhDau.Add(x);
            KhongDenChoXuLy.Clear();
            foreach (var x in bc.ChoXuLy) KhongDenChoXuLy.Add(x);
            TomTatKhongDen = $"{bc.ChoXuLy.Count} phiếu quá ngày đón đang chờ xử lý · {bc.DaDanhDau.Count} phiếu đã đánh dấu không đến từ {bc.TuNgay:dd/MM} đến {bc.DenNgay:dd/MM/yyyy}";
            return true;
        }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); return false; }
        catch (System.Net.Http.HttpRequestException) { return false; }
    }

    /// <summary>Tìm phòng trống trong khoảng ngày [từ, đến) (mặc định tối nay); hôm nay và phòng đã dọn thì nhận được ngay.</summary>
    [RelayCommand]
    private async Task TimPhongTrongAsync() => await TimPhongTrongCoLoiAsync();

    private async Task<bool> TimPhongTrongCoLoiAsync()
    {
        if (!CoQuyenPhong) return true;
        var tu = DateOnly.FromDateTime(PhongTrongTu);
        var den = DateOnly.FromDateTime(PhongTrongDen);
        if (den <= tu) { DatThongBao("Ngày trả phải sau ngày nhận.", true); return true; }
        if (tu < DateOnly.FromDateTime(DateTime.Today)) { DatThongBao("Ngày nhận không được ở quá khứ.", true); return true; }
        try
        {
            var ds = await _api.GetPhongTrongTheoNgayAsync(tu, den);
            PhongTrong.Clear();
            foreach (var x in ds) PhongTrong.Add(x);
            TomTatPhongTrong = $"{ds.Count} phòng trống từ {tu:dd/MM/yyyy} đến {den:dd/MM/yyyy}" + (tu == DateOnly.FromDateTime(DateTime.Today) ? $" · {ds.Count(x => x.NhanNgayDuoc)} nhận được ngay (đã dọn)" : "");
            return true;
        }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); return false; }
        catch (System.Net.Http.HttpRequestException) { return false; }
    }

    // ----- Hành động: mở màn hình xử lý với đúng đối tượng -----

    [RelayCommand] private void BamChiSo(ChiSoCaTruc? chiSo)
    {
        if (chiSo is null) return;
        if (chiSo.Tab != "") ChonTab(chiSo.Tab);
        else if (chiSo.ManHinh != "") MoManHinh?.Invoke(new YeuCauMoManHinh(chiSo.ManHinh));
    }

    [RelayCommand] private void XuLyViec(ViecCanXuLyDto? v)
    {
        if (v is null) return;
        MoManHinh?.Invoke(v.ManHinhDich switch
        {
            "DAT_PHONG" => new YeuCauMoManHinh("DAT_PHONG", MaPhieuDat: v.MaThamChieu),
            "TRA_PHONG" => new YeuCauMoManHinh("TRA_PHONG", MaPhieuNhan: v.MaPhieuNhan, MaPhong: v.MaPhong),
            "THANH_TOAN" => new YeuCauMoManHinh("THANH_TOAN", MaHoaDon: v.MaThamChieu),
            "SO_DO_PHONG" => new YeuCauMoManHinh("SO_DO_PHONG", MaPhong: v.MaPhong),
            var khac => new YeuCauMoManHinh(khac)
        });
    }

    [RelayCommand] private void MoPhieu(string? maPhieuDat) { if (!string.IsNullOrEmpty(maPhieuDat)) MoManHinh?.Invoke(new YeuCauMoManHinh("DAT_PHONG", MaPhieuDat: maPhieuDat)); }
    [RelayCommand] private void NhanPhongTheoPhieu(string? maPhieuDat) { if (!string.IsNullOrEmpty(maPhieuDat)) MoManHinh?.Invoke(new YeuCauMoManHinh("NHAN_PHONG", MaPhieuDat: maPhieuDat)); }
    [RelayCommand] private void TraPhong(KhachDiDto? luot) { if (luot is not null) MoManHinh?.Invoke(new YeuCauMoManHinh("TRA_PHONG", MaPhieuNhan: luot.MaPhieuNhan, MaPhong: luot.MaPhong)); }
    [RelayCommand] private void GiaHanDoiPhong(KhachDiDto? luot) { if (luot is not null) MoManHinh?.Invoke(new YeuCauMoManHinh("DOI_PHONG", MaPhieuNhan: luot.MaPhieuNhan, MaPhong: luot.MaPhong)); }
    [RelayCommand] private void DichVu(KhachDiDto? luot) { if (luot is not null) MoManHinh?.Invoke(new YeuCauMoManHinh("DICH_VU", MaPhieuNhan: luot.MaPhieuNhan, MaPhong: luot.MaPhong)); }
    [RelayCommand] private void NhanVangLai(PhongTrongDto? phong) { if (phong is not null) MoManHinh?.Invoke(new YeuCauMoManHinh("NHAN_PHONG", MaPhong: phong.MaPhong)); }
    [RelayCommand] private void XemPhong(string? maPhong) { if (!string.IsNullOrEmpty(maPhong)) MoManHinh?.Invoke(new YeuCauMoManHinh("SO_DO_PHONG", MaPhong: maPhong)); }
    [RelayCommand] private void DatPhongMoi() => MoManHinh?.Invoke(new YeuCauMoManHinh("DAT_PHONG_MOI"));

    // ----- Tìm nhanh trong các danh sách (không phân biệt hoa thường và dấu tiếng Việt) -----

    private void CaiBoLoc()
    {
        CollectionViewSource.GetDefaultView(KhachDen).Filter = o => o is KhachDenDto d && Khop(d.MaPhieuDat, d.HoTenKhach, d.SoDTKhach, d.PhongHienThi) && LocKhachDen switch
        {
            "NHAN_NGAY" => d.CoTheNhanNgay,
            "CHO_PHONG" => d.TrangThai == "DA_XAC_NHAN" && !d.CoTheNhanNgay,
            "CHUA_COC" => d.TrangThai == "CHO_XAC_NHAN",
            "DA_NHAN" => d.TrangThai is "DA_NHAN_PHONG" or "DA_TRA_PHONG",
            _ => true
        };
        CollectionViewSource.GetDefaultView(KhachDi).Filter = o => o is KhachDiDto d && Khop(d.MaPhong, d.HoTenKhach, d.SoDTKhach, d.MaPhieuDat) && LocKhachDi switch
        {
            "CON_O" => d.DangO,
            "QUA_HAN" => d.DangO && d.QuaHan,
            "DA_TRA" => !d.DangO,
            _ => true
        };
        CollectionViewSource.GetDefaultView(KhachDangO).Filter = o => o is KhachDiDto d && Khop(d.MaPhong, d.HoTenKhach, d.SoDTKhach, d.MaPhieuDat) && LocDangO switch
        {
            "QUA_HAN" => d.QuaHan,
            "TRA_HOM_NAY" => d.NgayTraDuKien == DateOnly.FromDateTime(DateTime.Today),
            "O_TIEP" => d.NgayTraDuKien > DateOnly.FromDateTime(DateTime.Today),
            _ => true
        };
        CollectionViewSource.GetDefaultView(PhongTrong).Filter = o => o is PhongTrongDto p && Khop(p.MaPhong, p.TenPhong, p.TenLoai) && LocPhongTrong switch
        {
            "NHAN_NGAY" => p.NhanNgayDuoc,
            "CHUA_DON" => p.TinhTrang == "VD",
            _ => true
        };
        CollectionViewSource.GetDefaultView(KhongDenChoXuLy).Filter = o => o is PhieuDatTomTatDto p && Khop(p.MaPhieuDat, p.HoTenKhach, p.SoDTKhach);
        CollectionViewSource.GetDefaultView(KhongDenDaDanhDau).Filter = o => o is PhieuDatTomTatDto p && Khop(p.MaPhieuDat, p.HoTenKhach, p.SoDTKhach);
    }

    private void LamMoiBoLoc()
    {
        _soViecToiDa = SoViecMoiTrang;
        CapNhatViecHienThi();
        foreach (var ds in new System.Collections.IEnumerable[] { KhachDen, KhachDi, KhachDangO, PhongTrong, KhongDenChoXuLy, KhongDenDaDanhDau })
            CollectionViewSource.GetDefaultView(ds).Refresh();
        CapNhatSoHienThi();
    }

    /// <summary>Dòng "Hiển thị x / y" của tab đang xem để lễ tân biết danh sách đang bị lọc (không tưởng là mất dữ liệu).</summary>
    private void CapNhatSoHienThi()
    {
        static int Dem(System.Collections.IEnumerable ds) => CollectionViewSource.GetDefaultView(ds).Cast<object>().Count();
        string Mo(System.Collections.IEnumerable ds, int tong, string donVi) { var hien = Dem(ds); return hien == tong ? "" : $"Đang lọc: hiển thị {hien} / {tong} {donVi}"; }
        SoHienThi = TabDangChon switch
        {
            "TONG_QUAN" => CanXuLy.Count(KhopViec) == CanXuLy.Count ? "" : $"Đang lọc: hiển thị {CanXuLy.Count(KhopViec)} / {CanXuLy.Count} việc",
            "KHACH_DEN" => Mo(KhachDen, KhachDen.Count, "phiếu"),
            "KHACH_DI" => Mo(KhachDi, KhachDi.Count, "phòng"),
            "DANG_O" => Mo(KhachDangO, KhachDangO.Count, "phòng"),
            "PHONG_TRONG" => Mo(PhongTrong, PhongTrong.Count, "phòng"),
            "KHONG_DEN" => Mo(KhongDenChoXuLy, KhongDenChoXuLy.Count, "phiếu"),
            _ => ""
        };
    }

    private bool Khop(params string?[] truong)
    {
        var khoa = BoDau(TuKhoaLoc.Trim());
        if (khoa.Length == 0) return true;
        return truong.Any(t => !string.IsNullOrEmpty(t) && BoDau(t).Contains(khoa, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Bỏ dấu tiếng Việt để "nguyen" khớp "Nguyễn"; đ/Đ thành d.</summary>
    public static string BoDau(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c == 'đ' ? 'd' : c == 'Đ' ? 'D' : c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }
}
