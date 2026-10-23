using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D14 - Quản lý cơ sở vật chất: sổ thiết bị theo phòng/kho với vòng đời Tốt → Hỏng chờ sửa → Đang sửa → (Đã thay mới) → Ngừng sử dụng → Đã thanh lý,
/// điều chuyển giữa phòng/kho, tổng quan (bao nhiêu thiết bị, hỏng bao nhiêu, phòng nào có vấn đề, hỏng quá hạn), tìm-lọc, lịch sử từng thiết bị
/// (ai · lúc nào · trước → sau · lý do) và bảo vệ dữ liệu chưa lưu.
/// </summary>
public partial class QuanLyCoSoVatChatViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private bool _dangHoanTac;
    private List<CsvcThietBiDto> _tatCa = [];
    private (string Ten, string SoLuong, string TinhTrang, string GhiChu, string MaPhong) _goc = ("", "1", "TOT", "", "");

    public ObservableCollection<CsvcThietBiDto> ThietBis { get; } = [];
    public ObservableCollection<CsvcPhongCanChuYDto> PhongCanChuY { get; } = [];
    /// <summary>Vị trí để thêm / điều chuyển: "" = kho / dùng chung, còn lại là từng phòng.</summary>
    public ObservableCollection<LuaChon> ViTriTuyChon { get; } = [];
    public ObservableCollection<LuaChon> LocViTriTuyChon { get; } = [];
    public static IReadOnlyList<LuaChon> TinhTrangTuyChon { get; } =
    [
        new("TOT", "Tốt"), new("HONG_CHO_SUA", "Hỏng, chờ sửa"), new("HONG_DANG_SUA", "Hỏng, đang sửa"),
        new("DA_THAY_MOI", "Đã thay mới"), new("NGUNG_SU_DUNG", "Ngừng sử dụng"), new("DA_THANH_LY", "Đã thanh lý")
    ];
    public string GiaiThichTinhTrang => "Tốt: dùng bình thường · Hỏng, chờ sửa / đang sửa: cần xử lý · Đã thay mới: vừa thay thiết bị mới (dùng được) · Ngừng sử dụng: còn trong sổ nhưng không dùng nữa (cần lý do) · Đã thanh lý: loại khỏi tài sản, chỉ để truy vết (cần lý do, không sửa được nữa).";

    // ---- tổng quan + bộ lọc
    [ObservableProperty] private string thongKe = "";
    [ObservableProperty] private string canhBao = "";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private string tieuDe = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool messageLaLoi = true;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locNhanh = "";      // "", CAN_XU_LY, TOT, KHONG_DUNG
    [ObservableProperty] private string locViTri = "";      // "" tất cả; KHO; mã phòng
    [ObservableProperty] private bool hienThanhLy;
    [ObservableProperty] private bool coTrangThai;
    public bool CoTheQuanLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyPhong);
    public bool TrongDanhSach => !DangTai && ThietBis.Count == 0;
    public bool CoPhongCanChuY => PhongCanChuY.Count > 0;
    private void DatThongBao(string noiDung, bool laLoi) { Message = noiDung; MessageLaLoi = laLoi; }

    partial void OnTuKhoaChanged(string value) => ApDungLoc();
    partial void OnLocNhanhChanged(string value) => ApDungLoc();
    partial void OnLocViTriChanged(string value) => ApDungLoc();
    partial void OnHienThanhLyChanged(bool value) => ApDungLoc();
    [RelayCommand] private void XoaBoLoc() { TuKhoa = ""; LocNhanh = ""; LocViTri = ""; HienThanhLy = false; }
    [RelayCommand] private void ChonLocNhanh(string ma) => LocNhanh = LocNhanh == ma ? "" : ma;
    [RelayCommand]
    private void XemPhong(CsvcPhongCanChuYDto? p) { if (p is null) return; LocViTri = p.MaPhong; LocNhanh = "CAN_XU_LY"; }

    // ---- form
    [ObservableProperty] private CsvcThietBiDto? selected;
    [ObservableProperty] private bool dangThem;
    [ObservableProperty] private string ten = "";
    [ObservableProperty] private string soLuongText = "1";
    [ObservableProperty] private string tinhTrang = "TOT";
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private string maPhongForm = "";
    [ObservableProperty] private string lyDo = "";
    [ObservableProperty] private string loiTen = "";
    [ObservableProperty] private string loiSoLuong = "";
    [ObservableProperty] private string loiLyDo = "";
    // ---- điều chuyển
    [ObservableProperty] private string maPhongDich = "";
    [ObservableProperty] private string soLuongChuyenText = "";
    [ObservableProperty] private string lyDoChuyen = "";
    [ObservableProperty] private string loiChuyen = "";

    public bool CoForm => DangThem || Selected is not null;
    public bool KhongCoForm => !CoForm;
    public bool DaChon => Selected is not null && !DangThem;
    public bool DaThanhLy => DaChon && Selected!.TinhTrang == "DA_THANH_LY";
    public bool ChoSua => DaChon && !DaThanhLy && CoTheQuanLy;
    public bool CoThayDoi => CoForm && (Ten, SoLuongText, TinhTrang, GhiChu, MaPhongForm) != _goc;
    public bool CoTheXoa => DaChon && Selected!.CoTheXoa && CoTheQuanLy;
    public bool CoLyDoKhongXoa => DaChon && !Selected!.CoTheXoa;
    public string LyDoKhongXoa => Selected?.LyDoKhongXoa ?? "";
    public bool CoGoiY => DaChon && !string.IsNullOrEmpty(Selected!.GoiY);
    public string GoiY => Selected?.GoiY ?? "";
    public string TieuDeForm => DangThem ? "Thêm thiết bị" : Selected is null ? "Thiết bị" : $"{Selected.Ten} ({Selected.MaSo})";
    public string ThongTinThietBi => Selected is null || DangThem ? "" :
        $"Đang ở: {Selected.ViTriHienThi}\nTình trạng: {Selected.TinhTrangHienThi}, {Selected.TuLucHienThi}" + (string.IsNullOrEmpty(Selected.LyDoTinhTrang) ? "" : $"\nLý do: {Selected.LyDoTinhTrang}")
        + (string.IsNullOrEmpty(Selected.CapNhatHienThi) ? "" : "\n" + Selected.CapNhatHienThi);
    public bool ChoThemVaoViTri => DangThem;
    public bool CoNhieuHonMot => DaChon && Selected!.SoLuong > 1;

    public QuanLyCoSoVatChatViewModel(ApiClient api) { _api = api; _ = LoadAsync(); }

    // ------------------------------------------------------------------ nạp + lọc
    [RelayCommand]
    private async Task LoadAsync()
    {
        DangTai = true;
        try
        {
            var tq = await _api.GetCsvcTongQuanAsync();
            _tatCa = tq.ThietBis;
            if (ViTriTuyChon.Count == 0)
            {
                ViTriTuyChon.Add(new("", "Kho / dùng chung (không gắn phòng)"));
                LocViTriTuyChon.Add(new("", "Tất cả vị trí")); LocViTriTuyChon.Add(new("KHO", "Kho / dùng chung"));
                foreach (var p in await _api.GetRoomsAsync())
                {
                    ViTriTuyChon.Add(new(p.MaPhong, $"{p.TenPhong} (tầng {p.Tang})"));
                    LocViTriTuyChon.Add(new(p.MaPhong, $"{p.TenPhong} (tầng {p.Tang})"));
                }
            }
            var t = tq.TongHop;
            var khac = t.TheoTinhTrang.Where(x => x.TinhTrang is "NGUNG_SU_DUNG" or "DA_THANH_LY" && x.SoLuong > 0).Select(x => $"{x.SoLuong} {TinhTrangCsvcText.HienThi(x.TinhTrang).ToLower()}").ToList();
            ThongKe = $"{t.TongSoLuong} thiết bị đang quản lý ({t.TongDong} dòng): {t.SoLuongDangDung} đang dùng được · {t.SoLuongHong} hỏng/đang sửa · {t.SoLuongTrongKho} trong kho" + (khac.Count > 0 ? " · " + string.Join(" · ", khac) : "");
            var cb = new List<string>();
            if (t.SoPhongCoThietBiHong > 0) cb.Add($"{t.SoPhongCoThietBiHong} phòng có thiết bị hỏng");
            if (t.HongQuaHan > 0) cb.Add($"{t.HongQuaHan} thiết bị hỏng quá {t.HongQuaHanNgay} ngày chưa xử lý xong");
            if (t.SoPhongChuaKhaiBao > 0) cb.Add($"{t.SoPhongChuaKhaiBao} phòng chưa khai báo thiết bị ({string.Join(", ", tq.PhongChuaKhaiBao.Take(5))}{(tq.PhongChuaKhaiBao.Count > 5 ? "…" : "")})");
            CanhBao = cb.Count == 0 ? "Không có thiết bị nào cần chú ý." : "Cần chú ý: " + string.Join(" · ", cb) + ".";
            CoTrangThai = true;
            CapNhatLuc = $"Cập nhật {tq.CapNhatLuc:HH:mm:ss}";
            PhongCanChuY.Clear(); foreach (var p in tq.PhongCanChuY) PhongCanChuY.Add(p);
            OnPropertyChanged(nameof(CoPhongCanChuY));

            var maChon = Selected?.MaSo;
            ApDungLoc();
            if (maChon is not null && !DangThem)
            {
                _dangHoanTac = true; Selected = _tatCa.FirstOrDefault(x => x.MaSo == maChon); _dangHoanTac = false;
                CapNhatTrangThai();
            }
        }
        catch (Exception e) when (e is LoiYeuCauApi or HttpRequestException)
        {
            DatThongBao(e is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {e.Message}", true);
        }
        finally { DangTai = false; OnPropertyChanged(nameof(TrongDanhSach)); }
    }

    private static string BoDau(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D');
    }

    private void ApDungLoc()
    {
        IEnumerable<CsvcThietBiDto> q = _tatCa;
        if (!HienThanhLy && LocNhanh != "KHONG_DUNG") q = q.Where(x => x.TinhTrang != "DA_THANH_LY");
        if (!string.IsNullOrWhiteSpace(TuKhoa))
            foreach (var t in TuKhoa.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = BoDau(t);
                q = q.Where(x => BoDau(x.Ten).Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaSo.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || BoDau(x.TenPhong ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (x.MaPhong?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                    || BoDau(x.GhiChu ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));
            }
        q = LocNhanh switch
        {
            "CAN_XU_LY" => q.Where(x => x.CanXuLy),
            "TOT" => q.Where(x => x.TinhTrang is "TOT" or "DA_THAY_MOI"),
            "KHONG_DUNG" => q.Where(x => x.TinhTrang is "NGUNG_SU_DUNG" or "DA_THANH_LY"),
            _ => q
        };
        if (LocViTri == "KHO") q = q.Where(x => x.MaPhong is null);
        else if (LocViTri != "") q = q.Where(x => x.MaPhong == LocViTri);
        var ds = q.OrderByDescending(x => x.CanXuLy).ThenBy(x => x.MaPhong ?? "~").ThenBy(x => x.Ten, StringComparer.CurrentCultureIgnoreCase).ToList();
        ThietBis.Clear(); foreach (var x in ds) ThietBis.Add(x);
        var tong = _tatCa.Count(x => HienThanhLy || x.TinhTrang != "DA_THANH_LY");
        TieuDe = ds.Count == tong ? $"{ds.Count} dòng thiết bị ({ds.Sum(x => x.SoLuong)} cái)" : $"{ds.Count}/{tong} dòng khớp bộ lọc ({ds.Sum(x => x.SoLuong)} cái)";
        OnPropertyChanged(nameof(TrongDanhSach));
    }

    // ------------------------------------------------------------------ form
    private void CapNhatTrangThai()
    {
        foreach (var p in new[] { nameof(CoForm), nameof(KhongCoForm), nameof(DaChon), nameof(DaThanhLy), nameof(ChoSua), nameof(CoThayDoi), nameof(CoTheXoa), nameof(CoLyDoKhongXoa), nameof(LyDoKhongXoa),
                     nameof(CoGoiY), nameof(GoiY), nameof(TieuDeForm), nameof(ThongTinThietBi), nameof(ChoThemVaoViTri), nameof(CoNhieuHonMot) })
            OnPropertyChanged(p);
    }
    partial void OnTenChanged(string value) { LoiTen = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnSoLuongTextChanged(string value) { LoiSoLuong = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnTinhTrangChanged(string value) { LoiLyDo = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnGhiChuChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnMaPhongFormChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnLyDoChanged(string value) => LoiLyDo = "";
    partial void OnDangThemChanged(bool value) => CapNhatTrangThai();

    partial void OnSelectedChanged(CsvcThietBiDto? oldValue, CsvcThietBiDto? newValue)
    {
        if (_dangHoanTac) { CapNhatTrangThai(); return; }
        if (CoThayDoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Thiết bị đang sửa chưa được lưu. Chuyển sang thiết bị khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; Selected = oldValue; _dangHoanTac = false;
            return;
        }
        DangThem = false;
        NapForm(newValue);
    }

    private void NapForm(CsvcThietBiDto? x)
    {
        if (x is null) { Ten = ""; SoLuongText = "1"; TinhTrang = "TOT"; GhiChu = ""; MaPhongForm = ""; }
        else { Ten = x.Ten; SoLuongText = x.SoLuong.ToString(CultureInfo.InvariantCulture); TinhTrang = x.TinhTrang; GhiChu = x.GhiChu ?? ""; MaPhongForm = x.MaPhong ?? ""; }
        XoaLyDo();
        _goc = (Ten, SoLuongText, TinhTrang, GhiChu, MaPhongForm);
        CapNhatTrangThai();
    }

    private void XoaLyDo()
    {
        LyDo = ""; LoiTen = LoiSoLuong = LoiLyDo = ""; MaPhongDich = ""; SoLuongChuyenText = ""; LyDoChuyen = ""; LoiChuyen = "";
    }

    [RelayCommand]
    private void ThemMoi()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý cơ sở vật chất.", true); return; }
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm thiết bị mới?")) return;
        var phongDangLoc = LocViTri is "" or "KHO" ? "" : LocViTri;
        _dangHoanTac = true; Selected = null; _dangHoanTac = false;
        NapForm(null);
        MaPhongForm = phongDangLoc; _goc = (Ten, SoLuongText, TinhTrang, GhiChu, MaPhongForm);
       
        DangThem = true;
        DatThongBao("", false);
    }

    [RelayCommand]
    private void HoanTac() { if (DangThem) { DangThem = false; NapForm(null); return; } NapForm(Selected); }

    private bool HopLe(out int soLuong)
    {
        soLuong = 0;
        LoiTen = Ten.Trim().Length < 2 ? "Tên thiết bị tối thiểu 2 ký tự." : Ten.Trim().Length > 100 ? "Tên tối đa 100 ký tự." : "";
        LoiSoLuong = int.TryParse(SoLuongText.Trim(), out var n) && n is >= 1 and <= 9999 ? "" : "Số lượng là số nguyên từ 1 đến 9.999.";
        var canLyDo = TinhTrang is "NGUNG_SU_DUNG" or "DA_THANH_LY" && (DangThem || Selected?.TinhTrang != TinhTrang);
        LoiLyDo = canLyDo && string.IsNullOrWhiteSpace(LyDo) ? "Nhập lý do khi ngừng sử dụng / thanh lý." : "";
        if (LoiTen != "" || LoiSoLuong != "" || LoiLyDo != "") return false;
        soLuong = n; return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý cơ sở vật chất.", true); return; }
        if (!DangThem && Selected is null) { DatThongBao("Chọn một thiết bị để sửa hoặc bấm \"Thêm thiết bị\".", true); return; }
        if (!DangThem && !CoThayDoi) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }
        if (!HopLe(out var soLuong)) { DatThongBao("Vui lòng sửa các ô báo lỗi màu đỏ rồi lưu lại.", true); return; }
        if (!DangThem && Selected is { } cu && TinhTrang == "DA_THANH_LY" && cu.TinhTrang != "DA_THANH_LY"
            && !HopThoai.XacNhan("Thanh lý thiết bị", $"Thanh lý \"{cu.Ten}\" ({cu.SoLuong})? Thiết bị sẽ bị loại khỏi tài sản và KHÔNG sửa hay điều chuyển được nữa (vẫn giữ trong sổ để truy vết)."))
            return;
        if (!DangThem && Selected is { } cu2 && TinhTrang == "NGUNG_SU_DUNG" && cu2.TinhTrang != "NGUNG_SU_DUNG"
            && !HopThoai.XacNhan("Ngừng sử dụng", $"Chuyển \"{cu2.Ten}\" sang ngừng sử dụng với lý do: {LyDo.Trim()}?")) return;

        var yeuCau = new YeuCauCoSoVatChat(Ten.Trim(), soLuong, TinhTrang, string.IsNullOrWhiteSpace(LyDo) ? null : LyDo.Trim(), string.IsNullOrWhiteSpace(GhiChu) ? null : GhiChu.Trim(),
            DangThem && !string.IsNullOrEmpty(MaPhongForm) ? MaPhongForm : null);
        try
        {
            string ma;
            if (DangThem) { var moi = await _api.TaoCsvcAsync(yeuCau); ma = moi.MaSo; DatThongBao($"Đã thêm \"{moi.Ten}\" ({ma}).", false); }
            else { ma = Selected!.MaSo; await _api.UpdateFacilityAsync(ma, yeuCau); DatThongBao($"Đã lưu \"{yeuCau.Ten}\".", false); }
            DangThem = false;
            _dangHoanTac = true; Selected = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; Selected = _tatCa.FirstOrDefault(x => x.MaSo == ma); _dangHoanTac = false;
            NapForm(Selected);
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa lưu được: " + e.Message, true); }
    }

    /// <summary>Đổi tình trạng bằng một cú bấm (báo hỏng, bắt đầu sửa, sửa xong, thay mới...) - ghi lý do từ ô "Lý do".</summary>
    [RelayCommand]
    private async Task DoiNhanhAsync(string tinhTrangMoi)
    {
        var x = Selected;
        if (x is null || DangThem || !CoTheQuanLy) return;
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Đổi tình trạng sẽ bỏ các thay đổi chưa lưu ở form. Tiếp tục?")) return;
        if (x.TinhTrang == tinhTrangMoi) { DatThongBao("Thiết bị đã ở tình trạng này.", false); return; }
        if (tinhTrangMoi is "NGUNG_SU_DUNG" or "DA_THANH_LY" && string.IsNullOrWhiteSpace(LyDo)) { LoiLyDo = "Nhập lý do (ô \"Lý do\") trước khi ngừng sử dụng / thanh lý."; DatThongBao(LoiLyDo, true); return; }
        var tieuDe = TinhTrangCsvcText.HienThi(tinhTrangMoi);
        if (tinhTrangMoi is "NGUNG_SU_DUNG" or "DA_THANH_LY"
            && !HopThoai.XacNhan(tieuDe, $"Chuyển \"{x.Ten}\" ({x.SoLuong}) sang \"{tieuDe.ToLower()}\"?" + (tinhTrangMoi == "DA_THANH_LY" ? " Không sửa hay điều chuyển được nữa." : ""))) return;
        try
        {
            await _api.UpdateFacilityAsync(x.MaSo, new YeuCauCoSoVatChat(x.Ten, x.SoLuong, tinhTrangMoi, string.IsNullOrWhiteSpace(LyDo) ? null : LyDo.Trim(), x.GhiChu));
            DatThongBao($"\"{x.Ten}\" → {tieuDe}.", false);
            await LoadAsync();
            NapForm(Selected);
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa đổi được tình trạng: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DieuChuyenAsync()
    {
        var x = Selected;
        if (x is null || DangThem || !CoTheQuanLy) return;
        LoiChuyen = "";
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Điều chuyển sẽ bỏ các thay đổi chưa lưu ở form. Tiếp tục?")) return;
        if ((MaPhongDich == "" ? null : MaPhongDich) == x.MaPhong) { LoiChuyen = "Chọn nơi đến khác nơi thiết bị đang ở."; return; }
        int? soLuong = null;
        if (!string.IsNullOrWhiteSpace(SoLuongChuyenText))
        {
            if (!int.TryParse(SoLuongChuyenText.Trim(), out var n) || n < 1 || n > x.SoLuong) { LoiChuyen = $"Số lượng chuyển từ 1 đến {x.SoLuong} (bỏ trống = chuyển hết)."; return; }
            soLuong = n;
        }
        if (string.IsNullOrWhiteSpace(LyDoChuyen)) { LoiChuyen = "Nhập lý do điều chuyển."; return; }
        var den = ViTriTuyChon.FirstOrDefault(v => v.Ma == MaPhongDich)?.Ten ?? "kho";
        if (!HopThoai.XacNhan("Điều chuyển thiết bị", $"Chuyển {soLuong ?? x.SoLuong} \"{x.Ten}\" từ {x.ViTriHienThi} sang {den}?")) return;
        try
        {
            var kq = await _api.DieuChuyenCsvcAsync(x.MaSo, new YeuCauDieuChuyenCsvc(string.IsNullOrEmpty(MaPhongDich) ? null : MaPhongDich, soLuong, LyDoChuyen.Trim()));
            DatThongBao($"Đã chuyển {soLuong ?? x.SoLuong} \"{x.Ten}\" sang {den}.", false);
            _dangHoanTac = true; Selected = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; Selected = _tatCa.FirstOrDefault(v => v.MaSo == x.MaSo) ?? _tatCa.FirstOrDefault(v => v.MaSo == kq.MaSo); _dangHoanTac = false;
            NapForm(Selected);
        }
        catch (LoiYeuCauApi e) { LoiChuyen = e.Message; DatThongBao("Chưa điều chuyển được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var x = Selected;
        if (x is null || DangThem) { DatThongBao("Chọn một thiết bị để xóa.", true); return; }
        if (!x.CoTheXoa) { DatThongBao(x.LyDoKhongXoa ?? "Thiết bị này không xóa được.", true); return; }
        if (!HopThoai.XacNhan("Xóa thiết bị", $"Xóa \"{x.Ten}\" ({x.MaSo}) khỏi sổ? Thiết bị chưa có lịch sử nên xóa được, nhưng thao tác không hoàn tác (vẫn ghi vào nhật ký).")) return;
        try
        {
            await _api.XoaCsvcAsync(x.MaSo, string.IsNullOrWhiteSpace(LyDo) ? null : LyDo.Trim());
            DatThongBao($"Đã xóa \"{x.Ten}\".", false);
            _dangHoanTac = true; Selected = null; _dangHoanTac = false;
            NapForm(null);
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa xóa được: " + e.Message, true); await LoadAsync(); }
    }
}
