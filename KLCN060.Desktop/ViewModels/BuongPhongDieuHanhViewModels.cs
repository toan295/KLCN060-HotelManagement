using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Một ô chỉ số trên bảng điều hành buồng phòng (MucDo: "" | "CANH_BAO" | "NGUY_HIEM" | "TOT" để tô màu).</summary>
public sealed record TheChiSoBuongPhong(string Nhan, string GiaTri, string Phu, string MucDo = "");

/// <summary>
/// D28 / tab "Tổng quan" - bảng điều hành buồng phòng của Quản lý: nhìn một màn hình biết bao nhiêu phòng, phòng nào bẩn / đang dọn / chờ kiểm tra / sẵn sàng,
/// phòng nào sắp có khách và cần ưu tiên, ai dọn phòng nào, phòng nào đang khóa bảo trì; Quản lý GIAO việc, KIỂM TRA chất lượng, khóa / mở phòng ngay tại đây
/// (Quản lý không tự nhận dọn phòng - việc dọn là của nhân viên buồng phòng).
/// </summary>
public partial class TongQuanBuongPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;
    private readonly Action<string>? _chuyenTab;
    private readonly bool _coQuyenDuyet = AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);
    private readonly bool _coQuyenXacNhan = AppSession.Current.CoQuyen(QuyenHeThong.XacNhanDonPhong);
    private readonly bool _coQuyenKhoaMo = AppSession.Current.CoQuyen(QuyenHeThong.KhoaMoPhong);
    private List<PhongBuongPhongDto> _tatCaCanXuLy = [];

    public static IReadOnlyList<LuaChon> LocUuTienTuyChon { get; } =
    [
        new("", "Mọi mức ưu tiên"), new("KHAN", "Khẩn (khách đến hôm nay)"), new("CAO", "Cao"), new("BINH_THUONG", "Bình thường")
    ];
    public static IReadOnlyList<LuaChon> LocNgayDenTuyChon { get; } =
    [
        new("", "Hôm nay và ngày mai"), new("HOM_NAY", "Chỉ khách đến hôm nay (kể cả quá ngày đón)"), new("NGAY_MAI", "Chỉ khách đến ngày mai")
    ];

    public static IReadOnlyList<LuaChon> LocGiaiDoanTuyChon { get; } =
    [
        new("", "Tất cả phòng cần xử lý"), new("KHAN", "Chỉ phòng khẩn / ưu tiên cao"), new("BAN", "Bẩn - chưa ai nhận"), new("DA_GIAO", "Đã giao - chưa bắt đầu"),
        new("DANG_DON", "Đang dọn"), new("CHO_KIEM_TRA", "Đã dọn - chờ kiểm tra")
    ];

    public ObservableCollection<TheChiSoBuongPhong> ChiSo { get; } = [];
    public ObservableCollection<PhongBuongPhongDto> CanXuLy { get; } = [];
    public ObservableCollection<PhongBuongPhongDto> SapCheckIn { get; } = [];
    public ObservableCollection<PhongBuongPhongDto> PhongKhoa { get; } = [];
    public ObservableCollection<NhanVienBuongPhongDto> NhanVienCoTheGiao { get; } = [];
    public ObservableCollection<LuaChon> Tangs { get; } = [new("", "Tất cả tầng")];
    public ObservableCollection<LuaChon> TangKhoas { get; } = [new("", "Tất cả tầng")];
    public ObservableCollection<LuaChon> LoaiPhongs { get; } = [new("", "Tất cả loại phòng")];
    public ObservableCollection<LuaChon> NguoiDons { get; } = [new("", "Tất cả người dọn")];
    private List<PhongBuongPhongDto> _tatCaSapCheckIn = [];
    private List<PhongBuongPhongDto> _tatCaPhongKhoa = [];

    [ObservableProperty] private string subTab = "CAN_XU_LY";
    [ObservableProperty] private string locGiaiDoan = "";
    [ObservableProperty] private string locTang = "";
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private PhongBuongPhongDto? phongDaChon;
    [ObservableProperty] private PhongBuongPhongDto? phongKhoaDaChon;
    [ObservableProperty] private NhanVienBuongPhongDto? nhanVienGiao;
    [ObservableProperty] private string lyDo = "";
    [ObservableProperty] private string tomTat = "";
    [ObservableProperty] private string locUuTien = "";
    [ObservableProperty] private string locLoaiPhong = "";
    [ObservableProperty] private string locNguoiDon = "";
    [ObservableProperty] private string soHienThi = "";

    // Bộ lọc riêng của "Khách sắp đến" và "Phòng bảo trì"
    [ObservableProperty] private string tuKhoaSapDen = "";
    [ObservableProperty] private string locNgayDen = "";
    [ObservableProperty] private bool chiCanChuY;
    [ObservableProperty] private string soSapDen = "";
    [ObservableProperty] private string tuKhoaKhoa = "";
    [ObservableProperty] private string locTangKhoa = "";
    [ObservableProperty] private string soKhoa = "";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private bool dangXuLy;

    public bool CoQuyenGiaoViec => _coQuyenDuyet;
    public bool CoQuyenKiemTra => _coQuyenXacNhan;
    public bool CoQuyenKhoaMo => _coQuyenKhoaMo;

    public TongQuanBuongPhongViewModel(ApiClient api, Action<string>? chuyenTab = null)
    {
        _api = api;
        _chuyenTab = chuyenTab;
        _ = TaiAsync();
    }

    [RelayCommand] private void ChonSubTab(string tab) => SubTab = tab;
    [RelayCommand] private void MoTab(string tab) => _chuyenTab?.Invoke(tab);

    partial void OnLocGiaiDoanChanged(string value) => LocDanhSach();
    partial void OnLocTangChanged(string value) => LocDanhSach();
    partial void OnTuKhoaChanged(string value) => LocDanhSach();
    partial void OnLocUuTienChanged(string value) => LocDanhSach();
    partial void OnLocLoaiPhongChanged(string value) => LocDanhSach();
    partial void OnLocNguoiDonChanged(string value) => LocDanhSach();
    partial void OnTuKhoaSapDenChanged(string value) => LocSapDen();
    partial void OnLocNgayDenChanged(string value) => LocSapDen();
    partial void OnChiCanChuYChanged(bool value) => LocSapDen();
    partial void OnTuKhoaKhoaChanged(string value) => LocKhoa();
    partial void OnLocTangKhoaChanged(string value) => LocKhoa();

    /// <summary>Đưa mọi bộ lọc của bảng "Phòng cần xử lý" về mặc định.</summary>
    [RelayCommand]
    private void XoaLoc()
    {
        LocGiaiDoan = ""; LocTang = ""; LocUuTien = ""; LocLoaiPhong = ""; LocNguoiDon = ""; TuKhoa = "";
    }

    [RelayCommand] private void XoaLocSapDen() { TuKhoaSapDen = ""; LocNgayDen = ""; ChiCanChuY = false; }
    [RelayCommand] private void XoaLocKhoa() { TuKhoaKhoa = ""; LocTangKhoa = ""; }

    partial void OnPhongDaChonChanged(PhongBuongPhongDto? value) => CapNhatNut();
    partial void OnPhongKhoaDaChonChanged(PhongBuongPhongDto? value) => CapNhatNut();
    partial void OnNhanVienGiaoChanged(NhanVienBuongPhongDto? value) => CapNhatNut();
    partial void OnLyDoChanged(string value) => CapNhatNut();
    partial void OnDangXuLyChanged(bool value) => CapNhatNut();

    public bool CoPhongDaChon => PhongDaChon is not null;
    public bool CoTheGiao => !DangXuLy && CoQuyenGiaoViec && PhongDaChon is { CoTheGiao: true } && NhanVienGiao is not null;
    public bool CoTheThuHoi => !DangXuLy && PhongDaChon is { NguoiDon: not null } p && (CoQuyenGiaoViec || p.NguoiDon == AppSession.Current.TenDangNhap);
    public bool CoTheKiemTra => !DangXuLy && CoQuyenKiemTra && PhongDaChon is { GiaiDoan: "CHO_KIEM_TRA" };
    public bool CoTheKhoa => !DangXuLy && CoQuyenKhoaMo && PhongDaChon is { TinhTrang: "VC" or "VD" or "VI" } && !string.IsNullOrWhiteSpace(LyDo);
    public bool CoTheMoLai => !DangXuLy && CoQuyenKhoaMo && PhongKhoaDaChon is not null;
    public string GoiYBuocTiep => PhongDaChon is null ? "" : PhongDaChon.HanhDongTiepTheo;
    public string ChiTietPhongDaChon => PhongDaChon is null ? "Chọn một phòng ở bảng phía trên, hệ thống sẽ gợi ý bước tiếp theo (giao việc, kiểm tra chất lượng hoặc khóa bảo trì)." :
        $"Phòng {PhongDaChon.TenPhong} · tầng {PhongDaChon.Tang} · {PhongDaChon.TenLoaiPhong} · {PhongDaChon.GiaiDoanHienThi}" +
        (string.IsNullOrEmpty(PhongDaChon.NguoiDonHienThi) ? "" : $" · {PhongDaChon.NguoiDonHienThi}") +
        (string.IsNullOrEmpty(PhongDaChon.KhachSapDenHienThi) ? "" : $" · khách sắp đến: {PhongDaChon.KhachSapDenHienThi}");

    private void CapNhatNut()
    {
        OnPropertyChanged(nameof(CoPhongDaChon));
        OnPropertyChanged(nameof(CoTheGiao));
        OnPropertyChanged(nameof(CoTheThuHoi));
        OnPropertyChanged(nameof(CoTheKiemTra));
        OnPropertyChanged(nameof(CoTheKhoa));
        OnPropertyChanged(nameof(CoTheMoLai));
        OnPropertyChanged(nameof(ChiTietPhongDaChon));
        OnPropertyChanged(nameof(GoiYBuocTiep));
    }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            var maChon = PhongDaChon?.MaPhong;
            var maKhoa = PhongKhoaDaChon?.MaPhong;
            var t = await _api.GetTongQuanBuongPhongAsync();

            ChiSo.Clear();
            ChiSo.Add(new("TỔNG PHÒNG", $"{t.TongPhong}", $"{t.VC + t.VI} nhận được ngay · {t.OC + t.OD} có khách"));
            ChiSo.Add(new("BẨN - CHƯA AI NHẬN", $"{t.ChuaAiNhan}", $"{t.VD} trống bẩn · {t.OD} có khách cần dọn", t.ChuaAiNhan > 0 ? "CANH_BAO" : "TOT"));
            ChiSo.Add(new("ĐÃ GIAO", $"{t.DaGiaoChuaBatDau}", "được giao, chưa bắt đầu", t.DaGiaoChuaBatDau > 0 ? "CANH_BAO" : ""));
            ChiSo.Add(new("ĐANG DỌN", $"{t.DangDon}", $"hôm nay đã dọn xong {t.DaDonHomNay}"));
            ChiSo.Add(new("CHỜ KIỂM TRA", $"{t.ChoKiemTra}", $"đã dọn (VC) · hôm nay đã kiểm tra {t.DaKiemTraHomNay}", t.ChoKiemTra > 0 ? "CANH_BAO" : ""));
            ChiSo.Add(new("SẴN SÀNG", $"{t.SanSang}", "đã kiểm tra (VI)", "TOT"));
            ChiSo.Add(new("PHÒNG KHẨN", $"{t.PhongKhan}", "khách đến hôm nay, phòng chưa dọn xong", t.PhongKhan > 0 ? "NGUY_HIEM" : "TOT"));
            ChiSo.Add(new("BẢO TRÌ (OOO)", $"{t.OOO}", t.OOO > 0 ? "đang khóa bán" : "không có", t.OOO > 0 ? "CANH_BAO" : ""));

            _tatCaCanXuLy = t.CanXuLy;
            CapNhatDanhSachTang(t.CanXuLy);
            CapNhatLuaChonLoc(t.CanXuLy);
            LocDanhSach();

            _tatCaSapCheckIn = t.SapCheckIn; LocSapDen();
            _tatCaPhongKhoa = t.PhongKhoa; CapNhatTangKhoa(); LocKhoa();
            var dangChonNv = NhanVienGiao?.TenDN;
            NhanVienCoTheGiao.Clear(); foreach (var n in t.NhanVien.Where(x => x.DangHoatDong)) NhanVienCoTheGiao.Add(n);
            NhanVienGiao = NhanVienCoTheGiao.FirstOrDefault(x => x.TenDN == dangChonNv);

            PhongDaChon = maChon is null ? null : CanXuLy.FirstOrDefault(x => x.MaPhong == maChon);
            PhongKhoaDaChon = maKhoa is null ? null : PhongKhoa.FirstOrDefault(x => x.MaPhong == maKhoa);

            TomTat = $"{t.TongPhong} phòng · {t.CanXuLy.Count} phòng cần xử lý ({t.ChuaAiNhan} chưa ai nhận, {t.DaGiaoChuaBatDau} đã giao, {t.DangDon} đang dọn, {t.ChoKiemTra} chờ kiểm tra) · {t.PhongKhan} khẩn";
            CapNhatLuc = $"Cập nhật {DateTime.Now:HH:mm:ss}";
            CapNhatNut();
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    /// <summary>Danh sách "Loại phòng" và "Người dọn" của bộ lọc lấy từ chính dữ liệu đang có; giữ lựa chọn cũ nếu còn.</summary>
    private void CapNhatLuaChonLoc(IEnumerable<PhongBuongPhongDto> phong)
    {
        var dsPhong = phong.ToList();
        var chonLoai = LocLoaiPhong; var chonNguoi = LocNguoiDon;
        LoaiPhongs.Clear(); LoaiPhongs.Add(new("", "Tất cả loại phòng"));
        foreach (var l in dsPhong.Select(x => x.TenLoaiPhong).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) LoaiPhongs.Add(new(l, l));
        NguoiDons.Clear(); NguoiDons.Add(new("", "Tất cả người dọn")); NguoiDons.Add(new("__CHUA_AI", "Chưa có người dọn"));
        foreach (var n in dsPhong.Select(x => x.NguoiDonLoc).Where(x => x.Length > 0).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) NguoiDons.Add(new(n, n));
        LocLoaiPhong = LoaiPhongs.Any(x => x.Ma == chonLoai) ? chonLoai : "";
        LocNguoiDon = NguoiDons.Any(x => x.Ma == chonNguoi) ? chonNguoi : "";
    }

    private void CapNhatDanhSachTang(IEnumerable<PhongBuongPhongDto> phong)
    {
        var chon = LocTang;
        var cacTang = phong.Select(x => x.Tang).Distinct().OrderBy(x => x).ToList();
        if (Tangs.Count - 1 == cacTang.Count && Tangs.Skip(1).Select(x => x.Ma).SequenceEqual(cacTang.Select(x => x.ToString()))) return;
        Tangs.Clear();
        Tangs.Add(new("", "Tất cả tầng"));
        foreach (var t in cacTang) Tangs.Add(new(t.ToString(), $"Tầng {t}"));
        LocTang = Tangs.Any(x => x.Ma == chon) ? chon : "";
    }

    private void LocDanhSach()
    {
        var maChon = PhongDaChon?.MaPhong;
        IEnumerable<PhongBuongPhongDto> q = _tatCaCanXuLy;
        q = LocGiaiDoan switch
        {
            "KHAN" => q.Where(x => x.MucUuTien is "KHAN" or "CAO"),
            "BAN" => q.Where(x => x.GiaiDoan is "BAN" or "BAN_CO_KHACH"),
            "" => q,
            var gd => q.Where(x => x.GiaiDoan == gd)
        };
        if (!string.IsNullOrEmpty(LocTang)) q = q.Where(x => x.Tang.ToString() == LocTang);
        if (!string.IsNullOrWhiteSpace(TuKhoa))
        {
            var k = TuKhoa.Trim();
            q = q.Where(x => x.TenPhong.Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaPhong.Contains(k, StringComparison.OrdinalIgnoreCase)
                             || (x.HoTenNguoiDon ?? x.NguoiDon ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)
                             || (x.KhachSapDen ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(LocUuTien)) q = q.Where(x => x.MucUuTien == LocUuTien);
        if (!string.IsNullOrEmpty(LocLoaiPhong)) q = q.Where(x => x.TenLoaiPhong == LocLoaiPhong);
        if (!string.IsNullOrEmpty(LocNguoiDon))
            q = LocNguoiDon == "__CHUA_AI" ? q.Where(x => x.NguoiDonLoc.Length == 0) : q.Where(x => x.NguoiDonLoc == LocNguoiDon);
        var ds = q.ToList();
        CanXuLy.Clear();
        foreach (var p in ds) CanXuLy.Add(p);
        SoHienThi = ds.Count == _tatCaCanXuLy.Count ? $"{ds.Count} phòng" : $"Hiển thị {ds.Count} / {_tatCaCanXuLy.Count} phòng (đang lọc)";
        PhongDaChon = maChon is null ? null : CanXuLy.FirstOrDefault(x => x.MaPhong == maChon);
    }

    private static string BoDau(string? s) => CaTrucViewModel.BoDau(s ?? "");
    private static bool Khop(string? tuKhoa, params string?[] truong)
        => string.IsNullOrWhiteSpace(tuKhoa) || truong.Any(t => BoDau(t).Contains(BoDau(tuKhoa.Trim()), StringComparison.OrdinalIgnoreCase));

    private void LocSapDen()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Today);
        var ds = _tatCaSapCheckIn.Where(x => Khop(TuKhoaSapDen, x.KhachSapDen, x.MaPhieuDatSapDen, x.TenPhong, x.NguoiDonLoc)
            && (LocNgayDen == "" || (LocNgayDen == "HOM_NAY" ? x.NgayDonSapDen <= homNay : x.NgayDonSapDen > homNay))
            && (!ChiCanChuY || x.MucUuTien != "BINH_THUONG")).ToList();
        SapCheckIn.Clear();
        foreach (var p in ds) SapCheckIn.Add(p);
        SoSapDen = ds.Count == _tatCaSapCheckIn.Count ? $"{ds.Count} phòng có khách sắp đến" : $"Hiển thị {ds.Count} / {_tatCaSapCheckIn.Count} (đang lọc)";
    }

    private void CapNhatTangKhoa()
    {
        var chon = LocTangKhoa;
        var cacTang = _tatCaPhongKhoa.Select(x => x.Tang).Distinct().OrderBy(x => x).ToList();
        TangKhoas.Clear();
        TangKhoas.Add(new("", "Tất cả tầng"));
        foreach (var t in cacTang) TangKhoas.Add(new(t.ToString(), $"Tầng {t}"));
        LocTangKhoa = TangKhoas.Any(x => x.Ma == chon) ? chon : "";
    }

    private void LocKhoa()
    {
        var maChon = PhongKhoaDaChon?.MaPhong;
        var ds = _tatCaPhongKhoa.Where(x => Khop(TuKhoaKhoa, x.TenPhong, x.LyDoKhoa, x.NguoiKhoa)
            && (LocTangKhoa == "" || x.Tang.ToString() == LocTangKhoa)).ToList();
        PhongKhoa.Clear();
        foreach (var p in ds) PhongKhoa.Add(p);
        SoKhoa = ds.Count == _tatCaPhongKhoa.Count ? $"{ds.Count} phòng đang khóa bảo trì" : $"Hiển thị {ds.Count} / {_tatCaPhongKhoa.Count} (đang lọc)";
        PhongKhoaDaChon = maChon is null ? null : PhongKhoa.FirstOrDefault(x => x.MaPhong == maChon);
    }

    private async Task ChayAsync(Func<Task<string>> hanhDong)
    {
        if (DangXuLy) return;
        DangXuLy = true;
        try { Ok(await hanhDong()); LyDo = ""; }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
        finally { DangXuLy = false; }
        await TaiAsync();
    }

    [RelayCommand]
    private Task GiaoViecAsync()
    {
        if (!CoTheGiao || PhongDaChon is null || NhanVienGiao is null) { Loi("Chọn phòng bẩn và nhân viên được giao việc."); return Task.CompletedTask; }
        var phong = PhongDaChon; var nv = NhanVienGiao;
        return ChayAsync(async () =>
        {
            await _api.GiaoViecDonPhongAsync(phong.MaPhong, nv.TenDN);
            return $"Đã giao phòng {phong.TenPhong} cho {nv.HoTen}. Nhân viên sẽ thấy việc này ở tab \"Việc dọn phòng\" và bấm nhận dọn để bắt đầu.";
        });
    }

    [RelayCommand]
    private Task ThuHoiAsync()
    {
        if (PhongDaChon?.NguoiDon is null) { Loi("Phòng này chưa có ai nhận hoặc được giao dọn."); return Task.CompletedTask; }
        var phong = PhongDaChon;
        var dangDon = phong.BatDauDonLuc is not null;
        if (!HopThoai.XacNhan("Thu hồi việc dọn phòng", $"Thu hồi việc dọn phòng {phong.TenPhong} khỏi {phong.HoTenNguoiDon ?? phong.NguoiDon}?" + (dangDon ? "\n\nNgười này đang dọn dở phòng." : "")))
            return Task.CompletedTask;
        return ChayAsync(async () =>
        {
            await _api.BoNhanDonPhongAsync(phong.MaPhong);
            return $"Đã thu hồi việc dọn phòng {phong.TenPhong}.";
        });
    }

    [RelayCommand]
    private Task KiemTraDatAsync()
    {
        if (!CoTheKiemTra || PhongDaChon is null) { Loi("Chọn phòng ở giai đoạn \"Đã dọn - chờ kiểm tra\"."); return Task.CompletedTask; }
        var phong = PhongDaChon; var ghiChu = string.IsNullOrWhiteSpace(LyDo) ? "Kiểm tra đạt" : "Kiểm tra đạt: " + LyDo.Trim();
        return ChayAsync(async () =>
        {
            await _api.CapNhatTinhTrangPhongAsync(phong.MaPhong, "VI", ghiChu);
            return $"Phòng {phong.TenPhong} đã kiểm tra đạt - sẵn sàng đón khách.";
        });
    }

    [RelayCommand]
    private Task KiemTraKhongDatAsync()
    {
        if (!CoTheKiemTra || PhongDaChon is null) { Loi("Chọn phòng ở giai đoạn \"Đã dọn - chờ kiểm tra\"."); return Task.CompletedTask; }
        if (string.IsNullOrWhiteSpace(LyDo)) { Loi("Nhập lý do không đạt (VD: còn tóc trong phòng tắm) rồi bấm lại - phòng sẽ quay về danh sách cần dọn."); return Task.CompletedTask; }
        var phong = PhongDaChon; var ghiChu = "Kiểm tra không đạt: " + LyDo.Trim();
        return ChayAsync(async () =>
        {
            await _api.CapNhatTinhTrangPhongAsync(phong.MaPhong, "VD", ghiChu);
            return $"Phòng {phong.TenPhong} kiểm tra không đạt, đã chuyển lại thành phòng bẩn cần dọn.";
        });
    }

    [RelayCommand]
    private Task KhoaBaoTriAsync()
    {
        if (PhongDaChon is null) { Loi("Chọn phòng cần khóa bảo trì."); return Task.CompletedTask; }
        if (!CoTheKhoa) { Loi("Nhập lý do khóa phòng bảo trì (bắt buộc) và chọn phòng đang trống (không có khách)."); return Task.CompletedTask; }
        var phong = PhongDaChon; var lyDo = LyDo.Trim();
        var canhBao = phong.KhachSapDen is null ? "" : $"\n\nPhòng đang được giữ cho khách {phong.KhachSapDenHienThi} - cần đổi phòng cho khách trước.";
        if (!HopThoai.XacNhan("Khóa phòng bảo trì", $"Khóa phòng {phong.TenPhong} (OOO)?\nLý do: {lyDo}{canhBao}"))
            return Task.CompletedTask;
        return ChayAsync(async () =>
        {
            await _api.CapNhatTinhTrangPhongAsync(phong.MaPhong, "OOO", lyDo);
            return $"Đã khóa phòng {phong.TenPhong} để bảo trì.";
        });
    }

    [RelayCommand]
    private Task MoLaiPhongAsync()
    {
        if (!CoTheMoLai || PhongKhoaDaChon is null) { Loi("Chọn phòng đang bảo trì cần mở lại."); return Task.CompletedTask; }
        var phong = PhongKhoaDaChon;
        var canhBao = phong.SuCoDangMo > 0 ? $"\n\nPhòng còn {phong.SuCoDangMo} sự cố chưa xử lý xong." : "";
        if (!HopThoai.XacNhan("Mở lại phòng", $"Mở lại phòng {phong.TenPhong} sau bảo trì? Phòng sẽ ở trạng thái bẩn (VD) để buồng phòng dọn trước khi bán.{canhBao}"))
            return Task.CompletedTask;
        var ghiChu = string.IsNullOrWhiteSpace(LyDo) ? "Đã xong bảo trì, mở lại phòng" : "Mở lại phòng: " + LyDo.Trim();
        return ChayAsync(async () =>
        {
            await _api.CapNhatTinhTrangPhongAsync(phong.MaPhong, "VD", ghiChu);
            return $"Đã mở lại phòng {phong.TenPhong}; phòng chờ buồng phòng dọn.";
        });
    }
}

/// <summary>D28 / tab "Nhật ký buồng phòng": ai làm gì, phòng nào, lúc nào, tình trạng trước → sau, lý do.</summary>
public partial class NhatKyBuongPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;
    public ObservableCollection<NhatKyBuongPhongDto> DanhSach { get; } = [];
    public ObservableCollection<LuaChon> Phongs { get; } = [new("", "Tất cả phòng")];
    public ObservableCollection<LuaChon> HanhDongs { get; } = [new("", "Mọi thao tác")];
    public ObservableCollection<LuaChon> NguoiThucHiens { get; } = [new("", "Mọi người thực hiện")];
    private readonly System.ComponentModel.ICollectionView _viewNhatKy;
    [ObservableProperty] private string tuKhoaNhatKy = "";
    [ObservableProperty] private string locHanhDong = "";
    [ObservableProperty] private string locNguoi = "";
    [ObservableProperty] private string soHienThi = "";

    [ObservableProperty] private string maPhongLoc = "";
    [ObservableProperty] private DateTime tuNgay = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime denNgay = DateTime.Today;
    [ObservableProperty] private string tomTat = "";

    public NhatKyBuongPhongViewModel(ApiClient api)
    {
        _api = api;
        _viewNhatKy = LocBang.Gan(DanhSach, (NhatKyBuongPhongDto x) => LocBang.Chua(TuKhoaNhatKy, x.NguoiHienThi, x.HanhDong, x.MaPhong, x.GhiChuHienThi, x.ThayDoiHienThi)
            && (string.IsNullOrEmpty(LocHanhDong) || x.HanhDong == LocHanhDong) && (string.IsNullOrEmpty(LocNguoi) || x.NguoiHienThi == LocNguoi));
    }

    partial void OnTuKhoaNhatKyChanged(string value) => LamMoiLoc();
    partial void OnLocHanhDongChanged(string value) => LamMoiLoc();
    partial void OnLocNguoiChanged(string value) => LamMoiLoc();
    private void LamMoiLoc() { _viewNhatKy.Refresh(); SoHienThi = DanhSach.Count == 0 ? "" : LocBang.TomTat(LocBang.Dem(_viewNhatKy), DanhSach.Count, "hoạt động"); }
    [RelayCommand] private void XoaLoc() { TuKhoaNhatKy = ""; LocHanhDong = ""; LocNguoi = ""; }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            if (Phongs.Count == 1)
                foreach (var p in (await _api.GetRoomsAsync()).OrderBy(x => x.TenPhong, StringComparer.Ordinal)) Phongs.Add(new(p.MaPhong, $"Phòng {p.TenPhong}"));
            if (TuNgay.Date > DenNgay.Date) { Loi("Từ ngày phải trước hoặc bằng đến ngày."); return; }
            var ds = await _api.GetNhatKyBuongPhongAsync(string.IsNullOrEmpty(MaPhongLoc) ? null : MaPhongLoc, DateOnly.FromDateTime(TuNgay), DateOnly.FromDateTime(DenNgay));
            var chonHd = LocHanhDong; var chonNguoi = LocNguoi;
            DanhSach.Clear();
            foreach (var x in ds) DanhSach.Add(x);
            HanhDongs.Clear(); HanhDongs.Add(new("", "Mọi thao tác"));
            foreach (var h in ds.Select(x => x.HanhDong).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) HanhDongs.Add(new(h, h));
            NguoiThucHiens.Clear(); NguoiThucHiens.Add(new("", "Mọi người thực hiện"));
            foreach (var n in ds.Select(x => x.NguoiHienThi).Distinct().OrderBy(x => x, StringComparer.CurrentCulture)) NguoiThucHiens.Add(new(n, n));
            LocHanhDong = HanhDongs.Any(x => x.Ma == chonHd) ? chonHd : "";
            LocNguoi = NguoiThucHiens.Any(x => x.Ma == chonNguoi) ? chonNguoi : "";
            LamMoiLoc();
            TomTat = ds.Count == 0 ? "Không có hoạt động buồng phòng nào trong khoảng này." : $"{ds.Count} hoạt động, mới nhất trước" + (ds.Count >= 200 ? " (chỉ hiện 200 dòng gần nhất - thu hẹp khoảng ngày để xem thêm)" : "");
            ThongBao = "";
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }
}
