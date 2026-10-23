using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;

/// <summary>Một dòng dịch vụ (kèm số lượng) trong form soạn gói dịch vụ.</summary>
public partial class DongGoiChonViewModel : ObservableObject
{
    public string MaDV { get; }
    public string TenDV { get; }
    public string DonViTinh { get; }
    public decimal GiaDV { get; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(GiaLeDong))] private int soLuong;
    /// <summary>Phần giá gói phân bổ cho dòng này (tính lại mỗi khi đổi số lượng / giá gói).</summary>
    [ObservableProperty] private decimal phanBo;
    public decimal GiaLeDong => GiaDV * SoLuong;
    public DongGoiChonViewModel(string maDV, string tenDV, string donViTinh, decimal giaDV, int soLuong) { MaDV = maDV; TenDV = tenDV; DonViTinh = donViTinh; GiaDV = giaDV; this.soLuong = soLuong; }
}

/// <summary>
/// D13 - Quản lý dịch vụ: danh mục dịch vụ (giá, đơn vị, nhóm doanh thu, trạng thái phục vụ), chi tiết hiển thị cho khách và GÓI dịch vụ.
/// Kèm tìm/lọc/sắp xếp, thống kê sử dụng, các ràng buộc (dịch vụ đã dùng không xóa được - chỉ ngừng phục vụ), xác nhận khi đổi giá / ngừng / xóa
/// và lịch sử thay đổi (ai, lúc nào, trước → sau). Hóa đơn cũ giữ nguyên tên/giá đã chốt lúc bán.
/// </summary>
public partial class QuanLyDichVuViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private bool _dangHoanTac;
    private List<DichVuQuanLyDto> _tatCa = [];
    private (string Ten, string Gia, string DonVi, bool PhucVu, string Nhom) _goc = ("", "", "", true, "DICH_VU");

    public ObservableCollection<DichVuQuanLyDto> Services { get; } = [];
    /// <summary>Dịch vụ đang phục vụ, không phụ thuộc bộ lọc - để chọn đưa vào gói.</summary>
    public ObservableCollection<DichVuQuanLyDto> DichVuChoGoi { get; } = [];
    public static IReadOnlyList<LuaChon> NhomDoanhThuTuyChon { get; } = [new("DICH_VU", "Dịch vụ"), new("MINIBAR", "Minibar"), new("GIAT_LA", "Giặt là"), new("KHAC", "Khác")];
    public static IReadOnlyList<LuaChon> SapXepTuyChon { get; } = [new("TEN", "Tên A → Z"), new("GIA", "Giá cao → thấp"), new("DOANH_THU", "Doanh thu cao → thấp"), new("LUOT", "Dùng nhiều → ít"), new("MA", "Mã dịch vụ")];
    public static IReadOnlyList<LuaChon> LocTrangThaiTuyChon { get; } = [new("", "Tất cả"), new("HOAT_DONG", "Đang phục vụ"), new("NGUNG", "Ngừng phục vụ"), new("CHUA_DUNG", "Chưa từng dùng"), new("THIEU_CHI_TIET", "Chưa có chi tiết")];

    [ObservableProperty] private string tab = "DICH_VU";
    [RelayCommand] private void ChonTab(string ma) => Tab = ma;
    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool messageLaLoi = true;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private string thongKe = "";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private string tieuDe = "";

    public bool CoTheQuanLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyDichVu);
    private void DatThongBao(string noiDung, bool laLoi) { Message = noiDung; MessageLaLoi = laLoi; }

    // ---- bộ lọc danh mục
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locTrangThai = "";
    [ObservableProperty] private string locNhom = "";
    [ObservableProperty] private string sapXep = "TEN";
    partial void OnTuKhoaChanged(string value) => ApDungLoc();
    partial void OnLocTrangThaiChanged(string value) => ApDungLoc();
    partial void OnLocNhomChanged(string value) => ApDungLoc();
    partial void OnSapXepChanged(string value) => ApDungLoc();
    [RelayCommand] private void XoaBoLoc() { TuKhoa = ""; LocTrangThai = ""; LocNhom = ""; }
    [RelayCommand] private void LocNhanh(string ma) => LocTrangThai = LocTrangThai == ma ? "" : ma;
    public bool TrongDanhSach => !DangTai && Services.Count == 0;

    // ---- form dịch vụ
    [ObservableProperty] private DichVuQuanLyDto? selectedService;
    [ObservableProperty] private bool dangThem;
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string priceText = "";
    /// <summary>Đơn giá dạng số (tương thích mã cũ); ô nhập trên form dùng <see cref="PriceText"/>.</summary>
    public decimal Price { get => DocSo(PriceText) ?? 0; set => PriceText = KLCN060.Desktop.Controls.MoneyInput.Dinh(value); }
    [ObservableProperty] private string unit = "";
    [ObservableProperty] private bool dangPhucVu = true;
    [ObservableProperty] private string loaiDoanhThu = "DICH_VU";
    [ObservableProperty] private string loiTen = "";
    [ObservableProperty] private string loiGia = "";
    [ObservableProperty] private string loiDonVi = "";

    public bool CoForm => DangThem || SelectedService is not null;
    public bool KhongCoForm => !CoForm;
    public bool HasSelectedService => SelectedService is not null && !DangThem;
    public bool CoThayDoi => CoForm && (Name, PriceText, Unit, DangPhucVu, LoaiDoanhThu) != _goc;
    public bool CoTheXoa => HasSelectedService && SelectedService!.CoTheXoa && CoTheQuanLy;
    public bool CoLyDoKhongXoa => HasSelectedService && !SelectedService!.CoTheXoa;
    public string LyDoKhongXoa => SelectedService?.LyDoKhongXoa ?? "";
    public bool CoTheNgung => HasSelectedService && SelectedService!.HoatDong && CoTheQuanLy;
    public bool CoTheMoLai => HasSelectedService && !SelectedService!.HoatDong && CoTheQuanLy;
    public string TieuDeForm => DangThem ? "Thêm dịch vụ mới" : SelectedService is null ? "Dịch vụ" : $"{SelectedService.TenDV} ({SelectedService.MaDV})";
    public string ThongTinSuDung => SelectedService is null || DangThem ? "" : SelectedService.DungHienThi + "\n" + SelectedService.GoiHienThi + "\n" + SelectedService.ChiTietHienThi
        + (SelectedService.DangGanMinibar ? "\nĐang gắn với vật dụng minibar (tính phí khi kiểm phòng)." : "")
        + (string.IsNullOrEmpty(SelectedService.SuaCuoiHienThi) ? "" : "\n" + SelectedService.SuaCuoiHienThi);
    public string NhacLichSu => "Quy tắc: tên và đơn giá được chốt vào hóa đơn lúc ghi cho khách - đổi giá/tên sau này chỉ áp dụng cho lần ghi MỚI. Dịch vụ đã dùng hoặc nằm trong gói không xóa được, chỉ ngừng phục vụ.";

    // ---- chi tiết dịch vụ
    public ObservableCollection<ChiTietDichVuDto> Details { get; } = [];
    [ObservableProperty] private ChiTietDichVuDto? selectedDetail;
    [ObservableProperty] private string detailTitle = ""; [ObservableProperty] private string detailContent = "";
    [ObservableProperty] private int detailOrder = 1; [ObservableProperty] private bool detailVisible = true;
    [ObservableProperty] private string detailMessage = "";
    public string DetailHeader => SelectedService is null ? "Chi tiết dịch vụ" : $"Chi tiết dịch vụ: {SelectedService.TenDV}";

    public QuanLyDichVuViewModel(ApiClient api) { _api = api; _ = LoadAsync(); }

    // ------------------------------------------------------------------ nạp + lọc
    [RelayCommand]
    private async Task LoadAsync()
    {
        DangTai = true;
        try
        {
            var cat = await _api.GetDichVuCatalogAsync();
            _tatCa = cat.DichVus;
            DichVuChoGoi.Clear();
            foreach (var x in _tatCa.Where(x => x.HoatDong).OrderBy(x => x.TenDV)) DichVuChoGoi.Add(x);
            var t = cat.TongHop;
            ThongKe = $"{t.TongDichVu} dịch vụ: {t.DangPhucVu} đang phục vụ · {t.NgungPhucVu} ngừng · {t.DichVuChuaCoChiTiet} chưa có chi tiết · {t.DichVuChuaTungDung} chưa từng dùng. "
                + $"{t.TongGoi} gói ({t.GoiDangBan} đang bán" + (t.GoiKhongBanDuoc > 0 ? $", {t.GoiKhongBanDuoc} tạm không bán được vì có dịch vụ ngừng" : "") + $"). Doanh thu dịch vụ đã ghi: {t.DoanhThuDichVu:N0}đ.";
            CapNhatLuc = $"Cập nhật {cat.CapNhatLuc:HH:mm:ss}";
            var maChon = SelectedService?.MaDV;
            ApDungLoc();
            if (maChon is not null && !DangThem)
            {
                _dangHoanTac = true; SelectedService = _tatCa.FirstOrDefault(x => x.MaDV == maChon); _dangHoanTac = false;
                CapNhatTrangThai();
            }
            await LoadPackagesAsync();
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
        foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormC);
    }

    private void ApDungLoc()
    {
        IEnumerable<DichVuQuanLyDto> q = _tatCa;
        if (!string.IsNullOrWhiteSpace(TuKhoa))
            foreach (var t in TuKhoa.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = BoDau(t);
                q = q.Where(x => BoDau(x.TenDV).Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaDV.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
        q = LocTrangThai switch
        {
            "HOAT_DONG" => q.Where(x => x.HoatDong),
            "NGUNG" => q.Where(x => !x.HoatDong),
            "CHUA_DUNG" => q.Where(x => x.SoLuotDung == 0),
            "THIEU_CHI_TIET" => q.Where(x => x.SoMucChiTietHienThi == 0),
            _ => q
        };
        if (LocNhom != "") q = q.Where(x => x.LoaiDoanhThu == LocNhom);
        var ds = (SapXep switch
        {
            "GIA" => q.OrderByDescending(x => x.GiaDV),
            "DOANH_THU" => q.OrderByDescending(x => x.DoanhThu),
            "LUOT" => q.OrderByDescending(x => x.SoLuotDung),
            "MA" => q.OrderBy(x => x.MaDV),
            _ => q.OrderBy(x => x.TenDV, StringComparer.CurrentCultureIgnoreCase)
        }).ToList();
        Services.Clear();
        foreach (var x in ds) Services.Add(x);
        TieuDe = ds.Count == _tatCa.Count ? $"{ds.Count} dịch vụ" : $"{ds.Count}/{_tatCa.Count} dịch vụ khớp bộ lọc";
        OnPropertyChanged(nameof(TrongDanhSach));
    }

    // ------------------------------------------------------------------ form dịch vụ
    private void CapNhatTrangThai()
    {
        foreach (var p in new[] { nameof(CoForm), nameof(KhongCoForm), nameof(HasSelectedService), nameof(CoThayDoi), nameof(CoTheXoa), nameof(CoLyDoKhongXoa), nameof(LyDoKhongXoa),
                     nameof(CoTheNgung), nameof(CoTheMoLai), nameof(TieuDeForm), nameof(ThongTinSuDung), nameof(DetailHeader) })
            OnPropertyChanged(p);
    }
    partial void OnNameChanged(string value) { LoiTen = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnPriceTextChanged(string value) { LoiGia = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnUnitChanged(string value) { LoiDonVi = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnDangPhucVuChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnLoaiDoanhThuChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnDangThemChanged(bool value) => CapNhatTrangThai();

    partial void OnSelectedServiceChanged(DichVuQuanLyDto? oldValue, DichVuQuanLyDto? newValue)
    {
        if (_dangHoanTac) { CapNhatTrangThai(); return; }
        if (CoThayDoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Dịch vụ đang sửa chưa được lưu. Chuyển sang dịch vụ khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; SelectedService = oldValue; _dangHoanTac = false;
            return;
        }
        DangThem = false;
        NapForm(newValue);
        DetailMessage = "";
        _ = LoadDetailsAsync();
    }

    private void NapForm(DichVuQuanLyDto? d)
    {
        if (d is null) { Name = ""; PriceText = ""; Unit = ""; DangPhucVu = true; LoaiDoanhThu = "DICH_VU"; }
        else { Name = d.TenDV; PriceText = KLCN060.Desktop.Controls.MoneyInput.Dinh(d.GiaDV); Unit = d.DonViTinh; DangPhucVu = d.HoatDong; LoaiDoanhThu = d.LoaiDoanhThu; }
        LoiTen = LoiGia = LoiDonVi = "";
        _goc = (Name, PriceText, Unit, DangPhucVu, LoaiDoanhThu);
        CapNhatTrangThai();
    }

    [RelayCommand]
    private void ThemMoi()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý dịch vụ.", true); return; }
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm dịch vụ mới?")) return;
        _dangHoanTac = true; SelectedService = null; _dangHoanTac = false;
        NapForm(null);
        Details.Clear(); ClearDetail();
        DangThem = true;
        DatThongBao("", false);
    }

    [RelayCommand]
    private void HoanTac() { if (DangThem) { DangThem = false; NapForm(null); return; } NapForm(SelectedService); }

    private static decimal? DocSo(string s) => decimal.TryParse(s.Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private bool HopLe(out decimal gia)
    {
        gia = 0;
        LoiTen = Name.Trim().Length < 2 ? "Tên dịch vụ tối thiểu 2 ký tự." : Name.Trim().Length > 100 ? "Tên tối đa 100 ký tự." : "";
        LoiDonVi = Unit.Trim().Length == 0 ? "Nhập đơn vị tính (VD: lượt, chai, suất, kg)." : Unit.Trim().Length > 20 ? "Đơn vị tính tối đa 20 ký tự." : "";
        var g = DocSo(PriceText);
        LoiGia = g is null or <= 0 ? "Nhập đơn giá lớn hơn 0 (VND)." : g > 100_000_000m ? "Đơn giá tối đa 100.000.000đ." : "";
        if (LoiTen != "" || LoiDonVi != "" || LoiGia != "") return false;
        gia = g!.Value;
        return true;
    }

    /// <summary>Các gói bị ảnh hưởng nếu dịch vụ đổi sang giá mới (tổng giá lẻ mới thấp hơn giá gói) - máy chủ cũng chặn, đây là cảnh báo sớm.</summary>
    private string CanhBaoGiaMoi(string maDV, decimal giaMoi)
    {
        var loi = new List<string>();
        foreach (var g in Goi.Where(x => x.DichVus.Any(d => d.MaDV == maDV)))
        {
            var giaLe = g.DichVus.Sum(d => (d.MaDV == maDV ? giaMoi : d.GiaDV) * d.SoLuong);
            if (giaLe < g.GiaGoi) loi.Add($"{g.TenGoi} (giá gói {g.GiaGoi:N0}đ > giá lẻ mới {giaLe:N0}đ)");
        }
        return loi.Count == 0 ? "" : "Giá mới làm các gói sau vô lý, hãy sửa gói trước: " + string.Join("; ", loi) + ".";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý dịch vụ.", true); return; }
        if (!DangThem && SelectedService is null) { DatThongBao("Chọn một dịch vụ để sửa hoặc bấm \"Thêm dịch vụ\".", true); return; }
        if (!DangThem && !CoThayDoi) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }
        if (!HopLe(out var gia)) { DatThongBao("Vui lòng sửa các ô báo lỗi màu đỏ rồi lưu lại.", true); return; }

        if (!DangThem && SelectedService is { } cu)
        {
            var doiGia = Math.Round(cu.GiaDV, 0) != Math.Round(gia, 0);
            var ngung = cu.HoatDong && !DangPhucVu;
            if (doiGia)
            {
                var canhBao = CanhBaoGiaMoi(cu.MaDV, gia);
                if (canhBao != "") { DatThongBao(canhBao, true); return; }
                var ghi = $"Đổi giá \"{cu.TenDV}\" từ {cu.GiaDV:N0}đ thành {gia:N0}đ?\n\nGiá mới chỉ áp dụng cho lần ghi MỚI; hóa đơn và lịch sử đã ghi giữ nguyên giá cũ."
                    + (cu.CacGoiChua.Count > 0 ? $"\nTổng giá lẻ (phần \"tiết kiệm\") của các gói {string.Join(", ", cu.CacGoiChua)} sẽ thay đổi theo." : "");
                if (!HopThoai.XacNhan("Đổi giá dịch vụ", ghi)) return;
            }
            if (ngung)
            {
                var ghi = $"Ngừng phục vụ \"{cu.TenDV}\"? Lễ tân không ghi thêm cho khách được nữa và dịch vụ ẩn khỏi danh sách công khai; lượt đã ghi giữ nguyên."
                    + (cu.CacGoiChua.Count > 0 ? $"\nCác gói chứa dịch vụ này sẽ tạm không bán được: {string.Join(", ", cu.CacGoiChua)}." : "");
                if (!HopThoai.XacNhan("Ngừng phục vụ", ghi)) return;
            }
        }

        var yeuCau = new YeuCauDichVu(Name.Trim(), gia, Unit.Trim(), DangPhucVu, LoaiDoanhThu);
        try
        {
            string ma;
            if (DangThem) { var moi = await _api.CreateServiceAsync(yeuCau); ma = moi.MaDV; DatThongBao($"Đã thêm dịch vụ {moi.TenDV} ({ma}).", false); }
            else { ma = SelectedService!.MaDV; await _api.UpdateServiceAsync(ma, yeuCau); DatThongBao($"Đã lưu dịch vụ {yeuCau.TenDV}.", false); }
            DangThem = false;
            _dangHoanTac = true; SelectedService = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; SelectedService = _tatCa.FirstOrDefault(x => x.MaDV == ma); _dangHoanTac = false;
            NapForm(SelectedService);
            await LoadDetailsAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa lưu được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DoiPhucVuAsync()
    {
        var d = SelectedService;
        if (d is null || DangThem) return;
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Đổi trạng thái sẽ bỏ các thay đổi chưa lưu ở form. Tiếp tục?")) return;
        var moLai = !d.HoatDong;
        if (!moLai)
        {
            var ghi = $"Ngừng phục vụ \"{d.TenDV}\"? Lễ tân không ghi thêm cho khách được nữa; lượt đã ghi giữ nguyên."
                + (d.CacGoiChua.Count > 0 ? $"\nCác gói chứa dịch vụ này sẽ tạm không bán được: {string.Join(", ", d.CacGoiChua)}." : "");
            if (!HopThoai.XacNhan("Ngừng phục vụ", ghi)) return;
        }
        try
        {
            await _api.UpdateServiceAsync(d.MaDV, new YeuCauDichVu(d.TenDV, d.GiaDV, d.DonViTinh, moLai, d.LoaiDoanhThu));
            DatThongBao(moLai ? $"Đã cho {d.TenDV} phục vụ trở lại." : $"Đã ngừng phục vụ {d.TenDV}.", false);
            await LoadAsync();
            NapForm(SelectedService);
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa đổi được trạng thái: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var d = SelectedService;
        if (d is null || DangThem) { DatThongBao("Chọn một dịch vụ để xóa.", true); return; }
        if (!d.CoTheXoa) { DatThongBao(d.LyDoKhongXoa ?? "Dịch vụ này không xóa được.", true); return; }
        if (!HopThoai.XacNhan("Xóa dịch vụ", $"Xóa dịch vụ \"{d.TenDV}\" ({d.MaDV}) cùng các mục chi tiết của nó? Dịch vụ chưa được dùng nên xóa được, nhưng thao tác không hoàn tác.")) return;
        try
        {
            await _api.DeleteServiceAsync(d.MaDV);
            DatThongBao($"Đã xóa dịch vụ {d.TenDV}.", false);
            _dangHoanTac = true; SelectedService = null; _dangHoanTac = false;
            NapForm(null); Details.Clear(); ClearDetail();
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa xóa được: " + e.Message, true); await LoadAsync(); }
    }

    // ------------------------------------------------------------------ chi tiết
    partial void OnSelectedDetailChanged(ChiTietDichVuDto? value)
    {
        if (value is null) return;
        DetailTitle = value.TieuDe; DetailContent = value.NoiDung; DetailOrder = value.ThuTu; DetailVisible = value.ConHienThi;
    }

    private async Task LoadDetailsAsync()
    {
        var dichVu = SelectedService;
        if (dichVu is null) { Details.Clear(); ClearDetail(); return; }
        try
        {
            var ds = await _api.GetServiceDetailsAsync(dichVu.MaDV);
            if (SelectedService?.MaDV != dichVu.MaDV) return;
            Details.Clear();
            foreach (var x in ds) Details.Add(x);
            ClearDetail();
        }
        catch (LoiYeuCauApi e) { DetailMessage = e.Message; }
    }

    [RelayCommand]
    private async Task SaveDetailAsync()
    {
        if (SelectedService is null) { DetailMessage = "Vui lòng chọn dịch vụ ở danh sách bên trái."; return; }
        if (string.IsNullOrWhiteSpace(DetailTitle) || string.IsNullOrWhiteSpace(DetailContent)) { DetailMessage = "Vui lòng nhập đủ tiêu đề và nội dung chi tiết."; return; }
        var maDV = SelectedService.MaDV;
        var yeuCau = new YeuCauChiTietDichVu(DetailTitle.Trim(), DetailContent.Trim(), DetailOrder, DetailVisible);
        try
        {
            if (SelectedDetail is null) await _api.CreateServiceDetailAsync(maDV, yeuCau);
            else await _api.UpdateServiceDetailAsync(maDV, SelectedDetail.MaCTDV, yeuCau);
            DetailMessage = "Đã lưu mục chi tiết.";
            await LoadDetailsAsync();
            await RefreshCatalogGiuChonAsync();
        }
        catch (LoiYeuCauApi e) { DetailMessage = e.Message; }
    }

    [RelayCommand]
    private async Task DeleteDetailAsync()
    {
        if (SelectedService is null || SelectedDetail is null) { DetailMessage = "Vui lòng chọn một mục chi tiết để xóa."; return; }
        if (!HopThoai.XacNhan("Xóa mục chi tiết", $"Xóa mục \"{SelectedDetail.TieuDe}\" khỏi mô tả dịch vụ? Khách sẽ không còn thấy mục này.")) return;
        var maDV = SelectedService.MaDV;
        try
        {
            await _api.DeleteServiceDetailAsync(maDV, SelectedDetail.MaCTDV);
            DetailMessage = "Đã xóa mục chi tiết.";
            await LoadDetailsAsync();
            await RefreshCatalogGiuChonAsync();
        }
        catch (LoiYeuCauApi e) { DetailMessage = e.Message; }
    }

    /// <summary>Làm mới số liệu danh mục (số mục chi tiết...) mà không đụng tới form đang soạn.</summary>
    private async Task RefreshCatalogGiuChonAsync()
    {
        var ma = SelectedService?.MaDV;
        try
        {
            var cat = await _api.GetDichVuCatalogAsync();
            _tatCa = cat.DichVus;
            ApDungLoc();
            _dangHoanTac = true; SelectedService = _tatCa.FirstOrDefault(x => x.MaDV == ma); _dangHoanTac = false;
            CapNhatTrangThai();
        }
        catch (LoiYeuCauApi) { }
    }

    [RelayCommand]
    private void ClearDetail()
    {
        SelectedDetail = null; DetailTitle = ""; DetailContent = "";
        DetailOrder = Details.Count == 0 ? 1 : Details.Max(x => x.ThuTu) + 1; DetailVisible = true;
    }

    // ======================= Gói dịch vụ (package) =======================
    private List<GoiDichVuDto> _tatCaGoi = [];
    private (string Ten, string MoTa, decimal Gia, bool Ban, string Dong) _gocGoi = ("", "", 0, true, "");
    public ObservableCollection<GoiDichVuDto> Goi { get; } = [];
    public ObservableCollection<DongGoiChonViewModel> DongGoi { get; } = [];
    [ObservableProperty] private string tuKhoaGoi = "";
    partial void OnTuKhoaGoiChanged(string value) => ApDungLocGoi();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(TieuDeGoi), nameof(CoGoiDaChon), nameof(CoTheXoaGoi), nameof(CoLyDoKhongXoaGoi), nameof(LyDoKhongXoaGoi), nameof(ThongTinGoi))] private GoiDichVuDto? goiDaChon;
    [ObservableProperty] private string tenGoi = ""; [ObservableProperty] private string moTaGoi = ""; [ObservableProperty] private decimal giaGoi;
    [ObservableProperty] private bool goiDangBan = true;
    [ObservableProperty] private DichVuQuanLyDto? dichVuThemVaoGoi; [ObservableProperty] private int soLuongThemVaoGoi = 1;
    [ObservableProperty] private string goiMessage = ""; [ObservableProperty] private bool goiMessageLaLoi = true;
    public bool CoGoiDaChon => GoiDaChon is not null;
    public string TieuDeGoi => GoiDaChon is null ? "Thêm gói dịch vụ" : $"Sửa gói {GoiDaChon.MaGoi}";
    public bool CoTheXoaGoi => GoiDaChon is { CoTheXoa: true } && CoTheQuanLy;
    public bool CoLyDoKhongXoaGoi => GoiDaChon is { CoTheXoa: false };
    public string LyDoKhongXoaGoi => GoiDaChon is { CoTheXoa: false } g ? $"Không xóa được vì gói đã được bán {g.SoLanDaBan} lần (hóa đơn cũ còn dùng). Hãy bỏ chọn \"Đang bán\" để ngừng bán." : "";
    public string ThongTinGoi => GoiDaChon is null ? "" : GoiDaChon.BanHienThi + (string.IsNullOrEmpty(GoiDaChon.SuaCuoiHienThi) ? "" : " · " + GoiDaChon.SuaCuoiHienThi);
    public bool CoThayDoiGoi => (TenGoi, MoTaGoi, GiaGoi, GoiDangBan, ChuoiDong()) != _gocGoi && (GoiDaChon is not null || TenGoi != "" || DongGoi.Count > 0 || GiaGoi != 0);
    private string ChuoiDong() => string.Join("|", DongGoi.OrderBy(x => x.MaDV, StringComparer.Ordinal).Select(x => $"{x.MaDV}x{x.SoLuong}"));

    public decimal GiaLeDangSoan => DongGoi.Sum(x => x.GiaLeDong);
    public string GhiChuGia => DongGoi.Count == 0 ? "Chọn dịch vụ để đưa vào gói." : $"Giá lẻ các dịch vụ: {GiaLeDangSoan:N0}đ" + (GiaGoi > 0 && GiaGoi <= GiaLeDangSoan ? $" · khách tiết kiệm {GiaLeDangSoan - GiaGoi:N0}đ ({(GiaLeDangSoan == 0 ? 0 : (GiaLeDangSoan - GiaGoi) * 100 / GiaLeDangSoan):0.#}%)" : GiaGoi > GiaLeDangSoan ? " · GIÁ GÓI PHẢI THẤP HƠN HOẶC BẰNG giá lẻ" : "");

    partial void OnGiaGoiChanged(decimal value) { OnPropertyChanged(nameof(GhiChuGia)); OnPropertyChanged(nameof(CoThayDoiGoi)); CapNhatGiaLe(); }
    partial void OnTenGoiChanged(string value) => OnPropertyChanged(nameof(CoThayDoiGoi));
    partial void OnMoTaGoiChanged(string value) => OnPropertyChanged(nameof(CoThayDoiGoi));
    partial void OnGoiDangBanChanged(bool value) => OnPropertyChanged(nameof(CoThayDoiGoi));

    partial void OnGoiDaChonChanged(GoiDichVuDto? oldValue, GoiDichVuDto? newValue)
    {
        if (_dangHoanTac) return;
        if (CoThayDoiGoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Gói đang sửa chưa được lưu. Chuyển sang gói khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; GoiDaChon = oldValue; _dangHoanTac = false;
            return;
        }
        if (newValue is null) return;
        NapFormGoi(newValue);
    }

    private void NapFormGoi(GoiDichVuDto g)
    {
        TenGoi = g.TenGoi; MoTaGoi = g.MoTa ?? ""; GiaGoi = g.GiaGoi; GoiDangBan = g.HoatDong;
        DongGoi.Clear();
        foreach (var d in g.DichVus) ThemDongVaoForm(new DongGoiChonViewModel(d.MaDV, d.TenDV, d.DonViTinh, d.GiaDV, d.SoLuong));
        CapNhatGiaLe();
        _gocGoi = (TenGoi, MoTaGoi, GiaGoi, GoiDangBan, ChuoiDong());
        OnPropertyChanged(nameof(CoThayDoiGoi));
    }

    private void ThemDongVaoForm(DongGoiChonViewModel dong)
    {
        dong.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DongGoiChonViewModel.SoLuong)) CapNhatGiaLe(); };
        DongGoi.Add(dong);
    }

    /// <summary>Tính lại giá lẻ và phân bổ giá gói cho từng dịch vụ (cùng thuật toán với máy chủ: theo tỷ lệ giá lẻ, dòng cuối nhận phần dư).</summary>
    private void CapNhatGiaLe()
    {
        var giaLe = GiaLeDangSoan;
        decimal daChia = 0;
        var dong = DongGoi.OrderBy(x => x.MaDV, StringComparer.Ordinal).ToList();
        for (var i = 0; i < dong.Count; i++)
        {
            var phan = i == dong.Count - 1 ? GiaGoi - daChia : Math.Round(giaLe > 0 ? GiaGoi * dong[i].GiaLeDong / giaLe : GiaGoi / dong.Count, 0, MidpointRounding.AwayFromZero);
            dong[i].PhanBo = Math.Max(0, phan); daChia += phan;
        }
        OnPropertyChanged(nameof(GiaLeDangSoan)); OnPropertyChanged(nameof(GhiChuGia)); OnPropertyChanged(nameof(CoThayDoiGoi));
    }
    private void DatThongBaoGoi(string noiDung, bool laLoi) { GoiMessage = noiDung; GoiMessageLaLoi = laLoi; }

    private async Task LoadPackagesAsync()
    {
        var giu = GoiDaChon?.MaGoi;
        _tatCaGoi = await _api.GetGoiDichVuAsync(tatCa: true);
        ApDungLocGoi();
        if (giu is not null) { _dangHoanTac = true; GoiDaChon = _tatCaGoi.FirstOrDefault(x => x.MaGoi == giu); _dangHoanTac = false; OnPropertyChanged(nameof(CoTheXoaGoi)); OnPropertyChanged(nameof(CoLyDoKhongXoaGoi)); OnPropertyChanged(nameof(LyDoKhongXoaGoi)); OnPropertyChanged(nameof(ThongTinGoi)); }
    }

    private void ApDungLocGoi()
    {
        IEnumerable<GoiDichVuDto> q = _tatCaGoi;
        if (!string.IsNullOrWhiteSpace(TuKhoaGoi))
            foreach (var t in TuKhoaGoi.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = BoDau(t);
                q = q.Where(x => BoDau(x.TenGoi).Contains(k, StringComparison.OrdinalIgnoreCase) || x.MaGoi.Contains(k, StringComparison.OrdinalIgnoreCase) || BoDau(x.NoiDungTomTat).Contains(k, StringComparison.OrdinalIgnoreCase));
            }
        var ds = q.ToList();
        Goi.Clear();
        foreach (var g in ds) Goi.Add(g);
    }

    [RelayCommand]
    private void GoiMoi()
    {
        if (CoThayDoiGoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và soạn gói mới?")) return;
        _dangHoanTac = true; GoiDaChon = null; _dangHoanTac = false;
        TenGoi = ""; MoTaGoi = ""; GiaGoi = 0; GoiDangBan = true; DongGoi.Clear(); DichVuThemVaoGoi = null; SoLuongThemVaoGoi = 1;
        CapNhatGiaLe(); _gocGoi = ("", "", 0, true, ""); DatThongBaoGoi("", false);
        foreach (var p in new[] { nameof(TieuDeGoi), nameof(CoGoiDaChon), nameof(CoTheXoaGoi), nameof(CoLyDoKhongXoaGoi), nameof(ThongTinGoi), nameof(CoThayDoiGoi) }) OnPropertyChanged(p);
    }

    [RelayCommand]
    private void ThemDichVuVaoGoi()
    {
        if (DichVuThemVaoGoi is null) { DatThongBaoGoi("Chọn một dịch vụ để đưa vào gói.", true); return; }
        if (SoLuongThemVaoGoi < 1) { DatThongBaoGoi("Số lượng phải lớn hơn 0.", true); return; }
        if (!DichVuThemVaoGoi.HoatDong) { DatThongBaoGoi($"\"{DichVuThemVaoGoi.TenDV}\" đang ngừng phục vụ nên không đưa vào gói được.", true); return; }
        var co = DongGoi.FirstOrDefault(x => x.MaDV == DichVuThemVaoGoi.MaDV);
        if (co is not null) co.SoLuong += SoLuongThemVaoGoi;
        else ThemDongVaoForm(new DongGoiChonViewModel(DichVuThemVaoGoi.MaDV, DichVuThemVaoGoi.TenDV, DichVuThemVaoGoi.DonViTinh, DichVuThemVaoGoi.GiaDV, SoLuongThemVaoGoi));
        SoLuongThemVaoGoi = 1; DatThongBaoGoi("", false); CapNhatGiaLe();
    }

    [RelayCommand]
    private void BoDichVuKhoiGoi(DongGoiChonViewModel? dong)
    {
        if (dong is null) return;
        DongGoi.Remove(dong); CapNhatGiaLe();
    }

    [RelayCommand]
    private void HoanTacGoi()
    {
        if (GoiDaChon is not null) NapFormGoi(GoiDaChon);
        else { TenGoi = ""; MoTaGoi = ""; GiaGoi = 0; GoiDangBan = true; DongGoi.Clear(); CapNhatGiaLe(); _gocGoi = ("", "", 0, true, ""); }
        DatThongBaoGoi("", false);
    }

    [RelayCommand]
    private async Task LuuGoiAsync()
    {
        if (!CoTheQuanLy) { DatThongBaoGoi("Tài khoản của bạn không có quyền quản lý dịch vụ.", true); return; }
        if (string.IsNullOrWhiteSpace(TenGoi)) { DatThongBaoGoi("Nhập tên gói.", true); return; }
        if (DongGoi.Count == 0) { DatThongBaoGoi("Gói cần có ít nhất một dịch vụ.", true); return; }
        if (GiaGoi <= 0) { DatThongBaoGoi("Nhập giá gói lớn hơn 0.", true); return; }
        if (GiaGoi > GiaLeDangSoan) { DatThongBaoGoi($"Giá gói ({GiaGoi:N0}đ) không được cao hơn tổng giá lẻ ({GiaLeDangSoan:N0}đ).", true); return; }
        if (GoiDaChon is { } cu)
        {
            if (!CoThayDoiGoi) { DatThongBaoGoi("Chưa có thay đổi nào để lưu.", false); return; }
            if (cu.GiaGoi != GiaGoi && !HopThoai.XacNhan("Đổi giá gói", $"Đổi giá gói \"{cu.TenGoi}\" từ {cu.GiaGoi:N0}đ thành {GiaGoi:N0}đ? Chỉ áp dụng cho lần bán MỚI; gói đã bán giữ nguyên giá cũ.")) return;
            if (cu.HoatDong && !GoiDangBan && !HopThoai.XacNhan("Ngừng bán gói", $"Ngừng bán gói \"{cu.TenGoi}\"? Lễ tân sẽ không ghi gói này cho khách được nữa; lịch sử đã bán giữ nguyên.")) return;
        }
        var yeuCau = new YeuCauGoiDichVu(TenGoi.Trim(), string.IsNullOrWhiteSpace(MoTaGoi) ? null : MoTaGoi.Trim(), GiaGoi, GoiDangBan, DongGoi.Select(x => new YeuCauGoiDong(x.MaDV, x.SoLuong)).ToList());
        try
        {
            var luu = GoiDaChon is null ? await _api.TaoGoiDichVuAsync(yeuCau) : await _api.SuaGoiDichVuAsync(GoiDaChon.MaGoi, yeuCau);
            _dangHoanTac = true; GoiDaChon = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; GoiDaChon = _tatCaGoi.FirstOrDefault(x => x.MaGoi == luu.MaGoi); _dangHoanTac = false;
            if (GoiDaChon is not null) NapFormGoi(GoiDaChon);
            DatThongBaoGoi($"Đã lưu gói {luu.MaGoi} - {luu.TenGoi} ({luu.TrangThaiHienThi.ToLower()}).", false);
        }
        catch (LoiYeuCauApi e) { DatThongBaoGoi("Chưa lưu được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task XoaGoiAsync()
    {
        if (GoiDaChon is null) { DatThongBaoGoi("Chọn một gói trong bảng để xóa.", true); return; }
        if (!GoiDaChon.CoTheXoa) { DatThongBaoGoi(LyDoKhongXoaGoi, true); return; }
        var g = GoiDaChon;
        if (!HopThoai.XacNhan("Xóa gói dịch vụ", $"Xóa gói \"{g.TenGoi}\" ({g.MaGoi})? Gói chưa từng được bán nên xóa được, nhưng thao tác không hoàn tác.")) return;
        try
        {
            await _api.XoaGoiDichVuAsync(g.MaGoi);
            _dangHoanTac = true; GoiDaChon = null; _dangHoanTac = false;
            TenGoi = ""; MoTaGoi = ""; GiaGoi = 0; GoiDangBan = true; DongGoi.Clear(); CapNhatGiaLe(); _gocGoi = ("", "", 0, true, "");
            await LoadAsync();
            DatThongBaoGoi($"Đã xóa gói {g.MaGoi}.", false);
        }
        catch (LoiYeuCauApi e) { DatThongBaoGoi("Chưa xóa được: " + e.Message, true); }
    }
}
