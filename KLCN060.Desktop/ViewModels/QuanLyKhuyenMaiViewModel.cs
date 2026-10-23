using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D12 - Quản lý khuyến mãi: mô tả đầy đủ (loại, giá trị, thời gian, phạm vi loại phòng, điều kiện số đêm / giá trị, lượt), trạng thái do máy chủ tính
/// (chưa bắt đầu / đang áp dụng / tạm ngưng / đã kết thúc / hết lượt), tìm-lọc, tạm ngưng thay cho xóa khi đã dùng, xem các đặt phòng đã dùng và
/// kiểm tra "vì sao booking này được / không được hưởng". Chương trình đã dùng bị khóa các trường làm thay đổi cách tính.
/// </summary>
public partial class QuanLyKhuyenMaiViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private bool _dangHoanTac;
    private List<KhuyenMaiDto> _tatCa = [];
    private (string Ten, string Loai, string GiaTri, DateTime? Bd, DateTime? Kt, string DieuKien, string Luot, string LoaiPhong, string Dem, string Tien) _goc = ("", "PHAN_TRAM", "", null, null, "", "", "", "", "");

    public ObservableCollection<KhuyenMaiDto> Promotions { get; } = [];
    public ObservableCollection<LoaiPhongDto> LoaiPhongs { get; } = [];
    public ObservableCollection<KhuyenMaiDaDungDto> DaDung { get; } = [];
    public ObservableCollection<DieuKienKhuyenMaiDto> KetQuaKiemTra { get; } = [];

    // ---- thống kê + bộ lọc
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locTrangThai = "";
    [ObservableProperty] private string locLoai = "";
    [ObservableProperty] private string thongKe = "";
    [ObservableProperty] private string tieuDe = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool messageLaLoi = true;
    [ObservableProperty] private bool dangTai;

    // ---- form
    [ObservableProperty] private KhuyenMaiDto? selectedPromotion;
    [ObservableProperty] private bool dangThem;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string type = "PHAN_TRAM";
    [ObservableProperty] private string giaTriText = "";
    [ObservableProperty] private DateTime? startDate = DateTime.Today;
    [ObservableProperty] private DateTime? endDate = DateTime.Today.AddDays(30);
    [ObservableProperty] private string condition = "";
    [ObservableProperty] private string luotText = "";
    [ObservableProperty] private string loaiPhongMa = "";
    [ObservableProperty] private string demText = "";
    [ObservableProperty] private string tienToiThieuText = "";
    [ObservableProperty] private string lyDoTamNgung = "";

    [ObservableProperty] private string loiTen = "";
    [ObservableProperty] private string loiGiaTri = "";
    [ObservableProperty] private string loiNgay = "";
    [ObservableProperty] private string loiKhac = "";

    // ---- kiểm tra booking
    [ObservableProperty] private string maPhieuKiemTra = "";
    [ObservableProperty] private string ketLuanKiemTra = "";

    public bool CoTheQuanLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyKhuyenMai);

    public bool CoForm => DangThem || SelectedPromotion is not null;
    public bool KhongCoForm => !CoForm;
    public bool DaSuDung => !DangThem && SelectedPromotion?.DaSuDung == true;
    public bool KhongKhoa => !DaSuDung;
    public bool CoGiaTri => Type != "QUA_TANG";
    /// <summary>Ô giá trị là số tiền (tự chèn dấu chấm hàng nghìn khi gõ); với phần trăm thì giữ nguyên số thập phân.</summary>
    public bool GiaTriLaTien => Type == "SO_TIEN_CO_DINH";
    public string NhanGiaTri => Type == "PHAN_TRAM" ? "Phần trăm giảm (%) *" : "Số tiền giảm (VND) *";
    public string GoiYGiaTri => Type == "PHAN_TRAM" ? "Từ 0,1 đến 100 (%). Tính trên tiền phòng thuộc phạm vi." : "Tối đa 100.000.000đ; không vượt quá tiền phòng thuộc phạm vi.";
    public string NhanDieuKien => Type == "QUA_TANG" ? "Mô tả quà tặng * (bắt buộc)" : "Điều kiện / ghi chú hiển thị cho khách";
    public bool CoThayDoi => CoForm && (Name, Type, GiaTriText, StartDate, EndDate, Condition, LuotText, LoaiPhongMa, DemText, TienToiThieuText) != _goc;
    public bool CoTheTamNgung => !DangThem && SelectedPromotion is { TamNgung: false } && CoTheQuanLy;
    public bool CoTheTiepTuc => !DangThem && SelectedPromotion is { TamNgung: true } && CoTheQuanLy;
    public bool CoTheXoa => !DangThem && SelectedPromotion?.CoTheXoa == true && CoTheQuanLy;
    public string LyDoKhoa => SelectedPromotion?.LyDoKhoa ?? "";
    public bool CoLyDoKhoa => !DangThem && !string.IsNullOrEmpty(SelectedPromotion?.LyDoKhoa);
    public bool CoDaDung => DaDung.Count > 0;
    public bool ChuaCoDaDung => DaDung.Count == 0;
    public bool KhongThemMoi => !DangThem;
    public bool TrongDanhSach => !DangTai && Promotions.Count == 0;
    public string TieuDeForm => DangThem ? "Thêm chương trình khuyến mãi" : SelectedPromotion is null ? "Chương trình khuyến mãi" : $"{SelectedPromotion.TenKM} ({SelectedPromotion.MaKM})";
    public string TrangThaiForm => SelectedPromotion is null || DangThem ? "" : SelectedPromotion.TrangThaiHienThi + (SelectedPromotion.TamNgung && !string.IsNullOrEmpty(SelectedPromotion.LyDoTamNgung) ? $": {SelectedPromotion.LyDoTamNgung}" : "");
    public string QuyTacCongDon => "Quy tắc: mỗi đặt phòng chỉ áp dụng MỘT khuyến mãi (không cộng dồn). Đơn giá đã gồm bảng giá theo mùa / cuối tuần / lễ; khuyến mãi trừ trên tiền phòng thuộc phạm vi, tối đa bằng tiền đó. Điều kiện xét theo NGÀY ĐẶT PHÒNG. Đặt phòng đã áp dụng giữ nguyên khi chương trình tạm ngưng hoặc hết hạn.";

    public QuanLyKhuyenMaiViewModel(ApiClient api)
    {
        _api = api;
        _ = LoadAsync();
    }

    private void DatThongBao(string noiDung, bool laLoi) { Message = noiDung; MessageLaLoi = laLoi; }

    // ------------------------------------------------------------------ nạp + lọc
    [RelayCommand]
    private async Task LoadAsync()
    {
        DangTai = true;
        try
        {
            var ds = await _api.GetPromotionsAsync();
            if (LoaiPhongs.Count == 0)
                foreach (var l in await _api.GetRoomTypesAsync()) LoaiPhongs.Add(l);
            _tatCa = ds;
            var maChon = SelectedPromotion?.MaKM;
            ApDungLoc();
            if (maChon is not null && !DangThem)
            {
                _dangHoanTac = true; SelectedPromotion = Promotions.FirstOrDefault(x => x.MaKM == maChon) ?? _tatCa.FirstOrDefault(x => x.MaKM == maChon); _dangHoanTac = false;
                CapNhatTrangThai();
            }
        }
        catch (Exception e) when (e is LoiYeuCauApi or HttpRequestException)
        {
            DatThongBao(e is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {e.Message}", true);
        }
        finally { DangTai = false; OnPropertyChanged(nameof(TrongDanhSach)); }
    }

    private void ApDungLoc()
    {
        IEnumerable<KhuyenMaiDto> q = _tatCa;
        if (!string.IsNullOrWhiteSpace(TuKhoa))
            foreach (var t in TuKhoa.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = t;
                q = q.Where(x => x.TenKM.Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaKM.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || (x.DieuKien?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        if (LocTrangThai != "") q = q.Where(x => x.TrangThai == LocTrangThai);
        if (LocLoai != "") q = q.Where(x => x.LoaiKM == LocLoai);
        var ds = q.OrderBy(x => x.TrangThai == "DANG_AP_DUNG" ? 0 : x.TrangThai == "CHUA_BAT_DAU" ? 1 : x.TrangThai == "TAM_NGUNG" ? 2 : 3).ThenBy(x => x.NgayKetThuc).ThenBy(x => x.MaKM).ToList();
        Promotions.Clear();
        foreach (var x in ds) Promotions.Add(x);
        TieuDe = ds.Count == _tatCa.Count ? $"{ds.Count} chương trình" : $"{ds.Count}/{_tatCa.Count} chương trình khớp bộ lọc";
        var dang = _tatCa.Count(x => x.TrangThai == "DANG_AP_DUNG");
        ThongKe = $"Đang áp dụng {dang} · chưa bắt đầu {_tatCa.Count(x => x.TrangThai == "CHUA_BAT_DAU")} · tạm ngưng {_tatCa.Count(x => x.TrangThai == "TAM_NGUNG")} · đã kết thúc / hết lượt {_tatCa.Count(x => x.TrangThai is "DA_KET_THUC" or "HET_LUOT")} · tổng đã giảm {_tatCa.Sum(x => x.TongTienDaGiam):N0}đ trên {_tatCa.Sum(x => x.SoPhieuDaApDung)} đặt phòng";
        OnPropertyChanged(nameof(TrongDanhSach));
    }

    partial void OnTuKhoaChanged(string value) => ApDungLoc();
    partial void OnLocTrangThaiChanged(string value) => ApDungLoc();
    partial void OnLocLoaiChanged(string value) => ApDungLoc();

    [RelayCommand]
    private void XoaBoLoc() { TuKhoa = ""; LocTrangThai = ""; LocLoai = ""; }

    [RelayCommand]
    private void LocNhanh(string trangThai) => LocTrangThai = LocTrangThai == trangThai ? "" : trangThai;

    // ------------------------------------------------------------------ form
    private void CapNhatTrangThai()
    {
        foreach (var p in new[] { nameof(CoForm), nameof(KhongCoForm), nameof(DaSuDung), nameof(KhongKhoa), nameof(CoThayDoi), nameof(CoTheTamNgung), nameof(CoTheTiepTuc),
                     nameof(CoTheXoa), nameof(LyDoKhoa), nameof(CoLyDoKhoa), nameof(TieuDeForm), nameof(TrangThaiForm), nameof(KhongThemMoi) })
            OnPropertyChanged(p);
    }

    partial void OnNameChanged(string value) { LoiTen = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnGiaTriTextChanged(string value) { LoiGiaTri = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnStartDateChanged(DateTime? value) { LoiNgay = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnEndDateChanged(DateTime? value) { LoiNgay = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnConditionChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnLuotTextChanged(string value) { LoiKhac = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnLoaiPhongMaChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnDemTextChanged(string value) { LoiKhac = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnTienToiThieuTextChanged(string value) { LoiKhac = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnTypeChanged(string value)
    {
        LoiGiaTri = "";
        if (value != "SO_TIEN_CO_DINH" && GiaTriText.Contains('.')) GiaTriText = GiaTriText.Replace(".", "");   // đang là "150.000" (tiền) mà đổi sang % thì bỏ dấu chấm nghìn
        foreach (var p in new[] { nameof(GiaTriLaTien), nameof(CoGiaTri), nameof(NhanGiaTri), nameof(GoiYGiaTri), nameof(NhanDieuKien), nameof(CoThayDoi) }) OnPropertyChanged(p);
    }
    partial void OnDangThemChanged(bool value) => CapNhatTrangThai();

    partial void OnSelectedPromotionChanged(KhuyenMaiDto? oldValue, KhuyenMaiDto? newValue)
    {
        if (_dangHoanTac) { CapNhatTrangThai(); return; }
        if (CoThayDoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Chương trình đang sửa chưa được lưu. Chuyển sang chương trình khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; SelectedPromotion = oldValue; _dangHoanTac = false;
            return;
        }
        DangThem = false;
        NapForm(newValue);
        _ = NapDaDungAsync(newValue);
    }

    private void NapForm(KhuyenMaiDto? p)
    {
        if (p is null) { Name = ""; Type = "PHAN_TRAM"; GiaTriText = ""; StartDate = DateTime.Today; EndDate = DateTime.Today.AddDays(30); Condition = ""; LuotText = ""; LoaiPhongMa = ""; DemText = ""; TienToiThieuText = ""; }
        else
        {
            Name = p.TenKM; Type = p.LoaiKM;
            GiaTriText = p.LoaiKM == "PHAN_TRAM" ? ((p.PhanTramKM ?? p.GiaTri)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "") : p.LoaiKM == "SO_TIEN_CO_DINH" ? KLCN060.Desktop.Controls.MoneyInput.Dinh(p.GiaTri ?? 0) : "";
            StartDate = p.NgayBatDau.ToDateTime(TimeOnly.MinValue); EndDate = p.NgayKetThuc.ToDateTime(TimeOnly.MinValue);
            Condition = p.DieuKien ?? ""; LuotText = p.SoLuongGioiHan?.ToString() ?? ""; LoaiPhongMa = p.MaLoaiApDung ?? "";
            DemText = p.SoDemToiThieu?.ToString() ?? ""; TienToiThieuText = p.TongTienToiThieu is { } t ? KLCN060.Desktop.Controls.MoneyInput.Dinh(t) : "";
        }
        LyDoTamNgung = ""; KetLuanKiemTra = ""; MaPhieuKiemTra = ""; KetQuaKiemTra.Clear();
        LoiTen = LoiGiaTri = LoiNgay = LoiKhac = "";
        _goc = (Name, Type, GiaTriText, StartDate, EndDate, Condition, LuotText, LoaiPhongMa, DemText, TienToiThieuText);
        CapNhatTrangThai();
    }

    private async Task NapDaDungAsync(KhuyenMaiDto? p)
    {
        DaDung.Clear();
        { OnPropertyChanged(nameof(CoDaDung)); OnPropertyChanged(nameof(ChuaCoDaDung)); }
        if (p is null) return;
        try
        {
            var ds = await _api.GetKhuyenMaiDaDungAsync(p.MaKM);
            if (SelectedPromotion?.MaKM != p.MaKM) return;
            foreach (var x in ds) DaDung.Add(x);
            { OnPropertyChanged(nameof(CoDaDung)); OnPropertyChanged(nameof(ChuaCoDaDung)); }
        }
        catch (LoiYeuCauApi) { /* danh sách đã dùng chỉ để tham khảo */ }
    }

    [RelayCommand]
    private void ThemMoi()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý khuyến mãi.", true); return; }
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm chương trình mới?")) return;
        _dangHoanTac = true; SelectedPromotion = null; _dangHoanTac = false;
        NapForm(null);
        DaDung.Clear(); { OnPropertyChanged(nameof(CoDaDung)); OnPropertyChanged(nameof(ChuaCoDaDung)); }
        DangThem = true;
        DatThongBao("", false);
    }

    [RelayCommand]
    private void XoaPhamVi() => LoaiPhongMa = "";

    [RelayCommand]
    private void HoanTac()
    {
        if (DangThem) { DangThem = false; NapForm(null); return; }
        NapForm(SelectedPromotion);
    }

    private static decimal? DocSo(string s) => decimal.TryParse(s.Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private bool HopLe(out YeuCauKhuyenMai? yeuCau)
    {
        yeuCau = null;
        LoiTen = Name.Trim().Length < 3 ? "Tên chương trình tối thiểu 3 ký tự." : Name.Trim().Length > 100 ? "Tên tối đa 100 ký tự." : "";
        decimal? giaTri = null;
        LoiGiaTri = "";
        if (Type != "QUA_TANG")
        {
            giaTri = Type == "PHAN_TRAM"
                ? (decimal.TryParse(GiaTriText.Replace(",", ".").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var phanTram) ? phanTram : null)
                : DocSo(GiaTriText);
            if (giaTri is null || giaTri <= 0) LoiGiaTri = "Nhập giá trị giảm lớn hơn 0.";
            else if (Type == "PHAN_TRAM" && giaTri > 100) LoiGiaTri = "Phần trăm giảm không vượt quá 100%.";
            else if (Type == "SO_TIEN_CO_DINH" && giaTri > 100_000_000m) LoiGiaTri = "Số tiền giảm tối đa 100.000.000đ.";
        }
        else if (string.IsNullOrWhiteSpace(Condition)) LoiGiaTri = "Khuyến mãi quà tặng cần mô tả quà ở ô bên dưới.";
        LoiNgay = StartDate is null || EndDate is null ? "Chọn ngày bắt đầu và ngày kết thúc." : EndDate < StartDate ? "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu." : "";
        int? luot = null; int? dem = null; decimal? tien = null; LoiKhac = "";
        if (!string.IsNullOrWhiteSpace(LuotText)) { if (int.TryParse(LuotText.Trim(), out var l) && l >= 1) luot = l; else LoiKhac = "Số lượt giới hạn phải là số nguyên từ 1 (bỏ trống nếu không giới hạn)."; }
        if (!string.IsNullOrWhiteSpace(DemText)) { if (int.TryParse(DemText.Trim(), out var d) && d is >= 1 and <= 365) dem = d; else LoiKhac = "Số đêm tối thiểu từ 1 đến 365 (bỏ trống nếu không yêu cầu)."; }
        if (!string.IsNullOrWhiteSpace(TienToiThieuText)) { if (DocSo(TienToiThieuText) is { } t && t > 0) tien = t; else LoiKhac = "Giá trị tối thiểu phải lớn hơn 0 (bỏ trống nếu không yêu cầu)."; }
        if (LoiTen != "" || LoiGiaTri != "" || LoiNgay != "" || LoiKhac != "") return false;

        yeuCau = new YeuCauKhuyenMai(Name.Trim(), Type == "PHAN_TRAM" ? giaTri : null, DateOnly.FromDateTime(StartDate!.Value), DateOnly.FromDateTime(EndDate!.Value),
            string.IsNullOrWhiteSpace(Condition) ? null : Condition.Trim(), Type, Type == "QUA_TANG" ? null : giaTri, luot,
            string.IsNullOrEmpty(LoaiPhongMa) ? null : LoaiPhongMa, dem, tien);
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý khuyến mãi.", true); return; }
        if (!DangThem && SelectedPromotion is null) { DatThongBao("Chọn một chương trình để sửa hoặc bấm \"Thêm chương trình\".", true); return; }
        if (!DangThem && !CoThayDoi) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }
        if (!HopLe(out var req) || req is null) { DatThongBao("Vui lòng sửa các ô báo lỗi màu đỏ rồi lưu lại.", true); return; }
        try
        {
            string ma;
            if (DangThem) { var moi = await _api.CreatePromotionAsync(req); ma = moi.MaKM; DatThongBao($"Đã tạo chương trình {moi.TenKM} ({ma}).", false); }
            else { ma = SelectedPromotion!.MaKM; await _api.UpdatePromotionAsync(ma, req); DatThongBao($"Đã lưu chương trình {req.TenKM}.", false); }
            DangThem = false;
            _dangHoanTac = true; SelectedPromotion = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; SelectedPromotion = _tatCa.FirstOrDefault(x => x.MaKM == ma); _dangHoanTac = false;
            NapForm(SelectedPromotion);
            _ = NapDaDungAsync(SelectedPromotion);
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa lưu được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task TamNgungAsync()
    {
        var p = SelectedPromotion;
        if (p is null || DangThem) return;
        if (string.IsNullOrWhiteSpace(LyDoTamNgung)) { DatThongBao("Nhập lý do tạm ngưng (bắt buộc).", true); return; }
        if (!HopThoai.XacNhan("Tạm ngưng khuyến mãi", $"Tạm ngưng \"{p.TenKM}\"? Các đặt phòng MỚI sẽ không được áp dụng; đặt phòng đã áp dụng giữ nguyên. Có thể tiếp tục lại bất cứ lúc nào.")) return;
        try { await _api.TamNgungKhuyenMaiAsync(p.MaKM, LyDoTamNgung.Trim()); DatThongBao($"Đã tạm ngưng {p.TenKM}.", false); await LoadAsync(); NapForm(SelectedPromotion); }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa tạm ngưng được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task TiepTucAsync()
    {
        var p = SelectedPromotion;
        if (p is null || DangThem) return;
        try { await _api.TiepTucKhuyenMaiAsync(p.MaKM); DatThongBao($"Đã cho {p.TenKM} áp dụng trở lại (trong thời gian hiệu lực).", false); await LoadAsync(); NapForm(SelectedPromotion); }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa tiếp tục được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        var p = SelectedPromotion;
        if (p is null || DangThem) { DatThongBao("Chọn một chương trình để xóa.", true); return; }
        if (!p.CoTheXoa) { DatThongBao(p.LyDoKhoa ?? "Chương trình này không xóa được.", true); return; }
        if (!HopThoai.XacNhan("Xóa khuyến mãi", $"Xóa chương trình \"{p.TenKM}\" ({p.MaKM})? Chương trình chưa được dùng nên xóa được, nhưng thao tác không hoàn tác.")) return;
        try
        {
            await _api.XoaKhuyenMaiAsync(p.MaKM);
            DatThongBao($"Đã xóa chương trình {p.TenKM}.", false);
            _dangHoanTac = true; SelectedPromotion = null; _dangHoanTac = false;
            NapForm(null); DaDung.Clear(); { OnPropertyChanged(nameof(CoDaDung)); OnPropertyChanged(nameof(ChuaCoDaDung)); }
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa xóa được: " + e.Message, true); await LoadAsync(); }
    }

    [RelayCommand]
    private async Task KiemTraBookingAsync()
    {
        var p = SelectedPromotion;
        if (p is null || DangThem) { DatThongBao("Chọn một chương trình đã lưu để kiểm tra.", true); return; }
        if (string.IsNullOrWhiteSpace(MaPhieuKiemTra)) { DatThongBao("Nhập mã đặt phòng cần kiểm tra (VD BK20261013-0001).", true); return; }
        try
        {
            var kq = await _api.KiemTraKhuyenMaiAsync(p.MaKM, MaPhieuKiemTra.Trim());
            KetQuaKiemTra.Clear();
            foreach (var d in kq.DieuKien) KetQuaKiemTra.Add(d);
            KetLuanKiemTra = kq.KetLuan;
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi e) { KetQuaKiemTra.Clear(); KetLuanKiemTra = ""; DatThongBao(e.Message, true); }
    }
}
