using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D24 - Nhật ký hoạt động toàn hệ thống (Quản lý): ai làm gì, lúc nào, trên đối tượng nào, kết quả ra sao (thành công / thất bại / bị từ chối), thay đổi Trước → Sau,
/// chuỗi sự kiện liên quan. Lọc theo khoảng ngày, người, nhóm nghiệp vụ, kết quả, nguồn (người dùng / hệ thống), mã đối tượng, từ khóa; phân trang;
/// xuất file (có quyền riêng) và kiểm tra toàn vẹn nhật ký.
/// </summary>
public partial class NhatKyViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int _lanTai;
    private int _lanLienQuan;

    public static Func<string, string, string?> ChonNoiLuu { get; set; } = (tenGoiY, phanMoRong) =>
    {
        var hop = new Microsoft.Win32.SaveFileDialog { Title = "Lưu nhật ký hoạt động", FileName = tenGoiY, DefaultExt = phanMoRong, Filter = "Tệp Excel (*.xlsx)|*.xlsx" };
        return hop.ShowDialog() == true ? hop.FileName : null;
    };

    public ObservableCollection<NhatKyDto> DanhSach { get; } = [];
    public ObservableCollection<NhomNhatKyDto> Nhoms { get; } = [];
    public ObservableCollection<LuaChon> KetQuas { get; } = [new("", "Mọi kết quả"), new("THANH_CONG", "Thành công"), new("THAT_BAI", "Thất bại"), new("TU_CHOI", "Bị từ chối (không đủ quyền)")];
    public ObservableCollection<LuaChon> Nguons { get; } = [new("", "Người dùng + hệ thống"), new("NGUOI_DUNG", "Người dùng thực hiện"), new("HE_THONG", "Hệ thống tự động")];

    public ObservableCollection<NhatKyDto> ChuoiSuKien { get; } = [];
    public ObservableCollection<NhatKyTinhTrangPhongDto> TinhTrangPhong { get; } = [];
    public ObservableCollection<string> DoiTuongLienQuan { get; } = [];

    /// <summary>Nút "Mở màn hình nghiệp vụ": PHONG, DAT_PHONG, HOA_DON, NHAN_VIEN, ... (ManHinhChinhViewModel chuyển màn hình).</summary>
    public event Action<string>? MoManHinh;

    [ObservableProperty] private DateTime? tuNgay = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime? denNgay = DateTime.Today;
    [ObservableProperty] private string taiKhoan = "";
    [ObservableProperty] private string nhomChon = "";
    [ObservableProperty] private string ketQuaChon = "";
    [ObservableProperty] private string nguonChon = "";
    [ObservableProperty] private string doiTuong = "";
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private NhatKyDto? dongDaChon;
    [ObservableProperty] private int trang = 1;
    [ObservableProperty] private int tongSoTrang = 1;
    [ObservableProperty] private string tongKet = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private string toanVenText = "";
    [ObservableProperty] private bool toanVenLaLoi;
    [ObservableProperty] private bool dangTaiLienQuan;
    [ObservableProperty] private DateTime? capNhatLuc;

    public bool CoTrangTruoc => Trang > 1;
    public bool CoTrangSau => Trang < TongSoTrang;
    public string ViTriTrang => $"Trang {Trang}/{TongSoTrang}";
    public bool CoQuyenXuat => AppSession.Current.CoQuyen(QuyenHeThong.XuatNhatKy);
    public bool CoQuyenToanVen => AppSession.Current.CoQuyen(QuyenHeThong.XemNhatKy);
    public string CapNhatLucText => CapNhatLuc is { } t ? $"Dữ liệu tính lúc {t:HH:mm:ss}" : "";
    public bool CoChuoiSuKien => ChuoiSuKien.Count > 1;
    public bool CoTinhTrangPhong => TinhTrangPhong.Count > 0;
    public bool CoDongChon => DongDaChon is not null;
    public string LocTomTat
    {
        get
        {
            var d = new List<string>();
            if (!string.IsNullOrWhiteSpace(DoiTuong)) d.Add($"đối tượng {DoiTuong.Trim()}");
            if (!string.IsNullOrEmpty(KetQuaChon)) d.Add(KetQuas.First(x => x.Ma == KetQuaChon).Ten.ToLowerInvariant());
            if (!string.IsNullOrEmpty(NguonChon)) d.Add(Nguons.First(x => x.Ma == NguonChon).Ten.ToLowerInvariant());
            return d.Count == 0 ? "" : "Đang lọc: " + string.Join(" · ", d);
        }
    }

    public NhatKyViewModel(ApiClient api)
    {
        _api = api;
        Nhoms.Add(new NhomNhatKyDto { Ma = "", Ten = "Tất cả nghiệp vụ" });
        _ = KhoiTaoAsync();
    }

    private async Task KhoiTaoAsync()
    {
        try
        {
            foreach (var n in await _api.GetNhomNhatKyAsync()) Nhoms.Add(n);
        }
        catch (LoiYeuCauApi) { /* vẫn dùng được bộ lọc còn lại */ }
        await TaiAsync();
    }

    partial void OnNhomChonChanged(string value) { if (!_dangDatLai) _ = TimLaiAsync(); }
    partial void OnKetQuaChonChanged(string value) { OnPropertyChanged(nameof(LocTomTat)); if (!_dangDatLai) _ = TimLaiAsync(); }
    partial void OnNguonChonChanged(string value) { OnPropertyChanged(nameof(LocTomTat)); if (!_dangDatLai) _ = TimLaiAsync(); }
    partial void OnDoiTuongChanged(string value) => OnPropertyChanged(nameof(LocTomTat));
    partial void OnCapNhatLucChanged(DateTime? value) => OnPropertyChanged(nameof(CapNhatLucText));

    partial void OnDongDaChonChanged(NhatKyDto? value)
    {
        OnPropertyChanged(nameof(CoDongChon));
        ChuoiSuKien.Clear(); TinhTrangPhong.Clear(); DoiTuongLienQuan.Clear();
        NotifyLienQuan();
        if (value is not null) _ = TaiLienQuanAsync(value);
    }

    private void NotifyLienQuan()
    {
        OnPropertyChanged(nameof(CoChuoiSuKien)); OnPropertyChanged(nameof(CoTinhTrangPhong));
    }

    private async Task TaiLienQuanAsync(NhatKyDto dong)
    {
        var lan = ++_lanLienQuan;
        DangTaiLienQuan = true;
        try
        {
            var lq = await _api.GetNhatKyLienQuanAsync(dong.MaNhatKy);
            if (lan != _lanLienQuan) return;
            foreach (var x in lq.ChuoiSuKien) ChuoiSuKien.Add(x);
            foreach (var x in lq.LichSuTinhTrangPhong) TinhTrangPhong.Add(x);
            foreach (var x in lq.DoiTuongLienQuan) DoiTuongLienQuan.Add(x);
            NotifyLienQuan();
        }
        catch (LoiYeuCauApi) { /* phần tra cứu thêm: không có cũng xem được dòng chính */ }
        finally { if (lan == _lanLienQuan) DangTaiLienQuan = false; }
    }

    partial void OnTrangChanged(int value)
    {
        OnPropertyChanged(nameof(CoTrangTruoc));
        OnPropertyChanged(nameof(CoTrangSau));
        OnPropertyChanged(nameof(ViTriTrang));
    }

    partial void OnTongSoTrangChanged(int value)
    {
        OnPropertyChanged(nameof(CoTrangSau));
        OnPropertyChanged(nameof(ViTriTrang));
    }

    [RelayCommand]
    private Task TimLaiAsync()
    {
        Trang = 1;
        return TaiAsync();
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            var lan = ++_lanTai;
            var ket = await _api.GetNhatKyAsync(TuNgay is null ? null : DateOnly.FromDateTime(TuNgay.Value), DenNgay is null ? null : DateOnly.FromDateTime(DenNgay.Value),
                TaiKhoan, string.IsNullOrEmpty(NhomChon) ? null : NhomChon, TuKhoa.Trim(), Trang,
                string.IsNullOrEmpty(KetQuaChon) ? null : KetQuaChon, string.IsNullOrEmpty(NguonChon) ? null : NguonChon, string.IsNullOrWhiteSpace(DoiTuong) ? null : DoiTuong.Trim());
            if (lan != _lanTai) return;
            DanhSach.Clear();
            foreach (var d in ket.DanhSach) DanhSach.Add(d);
            TongSoTrang = Math.Max(1, ket.TongSoTrang);
            DongDaChon = null;
            TongKet = $"{ket.TongSo:N0} dòng nhật ký";
            CapNhatLuc = DateTime.Now;
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    private void DatThongBao(string noiDung, bool laLoi) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    [RelayCommand]
    private async Task TrangTruocAsync()
    {
        if (!CoTrangTruoc) return;
        Trang--;
        await TaiAsync();
    }

    [RelayCommand]
    private async Task TrangSauAsync()
    {
        if (!CoTrangSau) return;
        Trang++;
        await TaiAsync();
    }

    [RelayCommand]
    private async Task XoaLocAsync()
    {
        TaiKhoan = ""; TuKhoa = ""; DoiTuong = "";
        _dangDatLai = true;
        NhomChon = ""; KetQuaChon = ""; NguonChon = "";
        _dangDatLai = false;
        TuNgay = DateTime.Today.AddDays(-6); DenNgay = DateTime.Today;
        await TimLaiAsync();
    }
    private bool _dangDatLai;

    /// <summary>Bấm một mã đối tượng liên quan: lọc toàn bộ nhật ký theo đối tượng đó (mọi ngày) để thấy cả vòng đời.</summary>
    [RelayCommand]
    private async Task XemDoiTuongAsync(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma)) return;
        DoiTuong = ma;
        TuNgay = null; DenNgay = null;
        await TimLaiAsync();
    }

    /// <summary>Mở màn hình nghiệp vụ của đối tượng đang xem (theo tiền tố mã).</summary>
    [RelayCommand]
    private void MoDoiTuong(string? ma)
    {
        if (string.IsNullOrWhiteSpace(ma)) return;
        var m = ma.Trim().ToUpperInvariant();
        string? man = m.StartsWith("BK") || m.StartsWith("CI") || m.StartsWith("PN") ? "DAT_PHONG"
            : m.StartsWith("HD") ? "HOA_DON"
            : m.StartsWith("NV") ? "NHAN_VIEN"
            : m.Length >= 4 && m[0] == 'P' && char.IsDigit(m[1]) ? "PHONG"
            : m.StartsWith("DQ") || m.StartsWith("SC") ? "BUONG_PHONG"
            : null;
        if (man is null) { DatThongBao($"Chưa có màn hình nghiệp vụ riêng cho mã {ma}; dùng \"Xem nhật ký\" để xem vòng đời của đối tượng này.", false); return; }
        MoManHinh?.Invoke(man);
    }

    [RelayCommand]
    private async Task XuatAsync()
    {
        if (!CoQuyenXuat) { DatThongBao("Bạn không có quyền xuất nhật ký.", true); return; }
        try
        {
            var (noi, tenFile) = await _api.XuatNhatKyAsync(TuNgay is null ? null : DateOnly.FromDateTime(TuNgay.Value), DenNgay is null ? null : DateOnly.FromDateTime(DenNgay.Value),
                TaiKhoan, string.IsNullOrEmpty(NhomChon) ? null : NhomChon, TuKhoa.Trim(),
                string.IsNullOrEmpty(KetQuaChon) ? null : KetQuaChon, string.IsNullOrEmpty(NguonChon) ? null : NguonChon, string.IsNullOrWhiteSpace(DoiTuong) ? null : DoiTuong.Trim());
            var duongDan = ChonNoiLuu(tenFile, ".xlsx");
            if (duongDan is null) { DatThongBao("Đã hủy xuất file (việc xuất chưa được thực hiện ở máy bạn, nhưng lần gọi xuất đã được ghi vào nhật ký).", false); return; }
            await File.WriteAllBytesAsync(duongDan, noi);
            DatThongBao($"Đã xuất nhật ký theo bộ lọc hiện tại ra: {duongDan} (việc xuất được ghi vào nhật ký).", false);
        }
        catch (LoiYeuCauApi loi) { DatThongBao(loi.Message, true); }
        catch (IOException loi) { DatThongBao($"Không ghi được file: {loi.Message}", true); }
    }

    [RelayCommand]
    private async Task KiemTraToanVenAsync()
    {
        try
        {
            var kq = await _api.KiemTraToanVenNhatKyAsync();
            ToanVenText = kq.KetLuan;
            ToanVenLaLoi = !kq.ToanVen;
        }
        catch (LoiYeuCauApi loi)
        {
            ToanVenText = loi.Message; ToanVenLaLoi = true;
        }
    }
}
