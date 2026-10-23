using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D18 - Đối soát chuyển khoản. Đầu màn hình: tiền ngân hàng đã báo, khớp / chưa khớp, cần xử lý, chênh lệch, mức cập nhật. Mỗi thông báo tiền về truy ngược
/// đến yêu cầu thanh toán - booking / hóa đơn - khách - số phải thu - số đã ghi nhận - chênh lệch - lý do. Ngoại lệ (thiếu/thừa/sai nội dung/hết hạn/...) xử lý
/// ngay tại panel chi tiết (đã xử lý ngoài hệ thống / bỏ qua, bắt buộc ghi chú), mọi thao tác có nhật ký và chỉ làm được một lần.
/// </summary>
public partial class DoiSoatChuyenKhoanViewModel : ObservableObject, IDisposable
{
    private readonly ApiClient _api;
    private readonly System.Windows.Threading.DispatcherTimer _dongHo;
    private int _lanTai;
    private int _lanChiTiet;

    public static Func<string, string, string?> ChonNoiLuu { get; set; } = (tenGoiY, phanMoRong) =>
    {
        var hop = new Microsoft.Win32.SaveFileDialog { Title = "Lưu báo cáo đối soát", FileName = tenGoiY, DefaultExt = phanMoRong, Filter = "Tệp Excel (*.xlsx)|*.xlsx" };
        return hop.ShowDialog() == true ? hop.FileName : null;
    };

    public ObservableCollection<DongDoiSoatDto> DanhSach { get; } = [];
    public ObservableCollection<GiaoDichChuyenKhoanDto> GiaoDich { get; } = [];
    public ObservableCollection<NhomNgoaiLeDoiSoatDto> NhomNgoaiLe { get; } = [];
    public ObservableCollection<LichSuDoiSoatDto> LichSu { get; } = [];
    public ObservableCollection<DongDoiSoatDto> CungGiaoDich { get; } = [];

    [ObservableProperty] private string tuKhoa = "";
    /// <summary>Rỗng = tất cả; KHOP hoặc mã loại ngoại lệ.</summary>
    [ObservableProperty] private string locLoai = "";
    /// <summary>Rỗng = tất cả; CAN_XU_LY / DA_XU_LY / KHONG_CAN.</summary>
    [ObservableProperty] private string locXuLy = "";
    [ObservableProperty] private DateTime? tuNgay = DateTime.Today.AddDays(-29);
    [ObservableProperty] private DateTime? denNgay = DateTime.Today;
    [ObservableProperty] private string locGiaoDich = "";
    [ObservableProperty] private int tabDangChon;

    [ObservableProperty] private TongQuanDoiSoatDto? tongQuan;
    [ObservableProperty] private DongDoiSoatDto? dongDaChon;
    [ObservableProperty] private ChiTietDoiSoatDto? chiTiet;
    [ObservableProperty] private string tieuDeDanhSach = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private bool duLieuCu;

    // Xử lý ngoại lệ
    [ObservableProperty] private string huongXuLy = "";
    [ObservableProperty] private string ghiChuXuLy = "";
    [ObservableProperty] private bool dangXuLy;

    /// <summary>Xử lý ngoại lệ tiền cần quyền XU_LY_DOI_SOAT (API kiểm tra lại); người chỉ có quyền xem chỉ theo dõi.</summary>
    public bool CoTheXuLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.XuLyDoiSoat);
    public bool CoTongQuan => TongQuan is not null;
    public bool CoChiTiet => ChiTiet is not null;
    public bool CoCungGiaoDich => CungGiaoDich.Count > 0;
    public bool CoLichSu => LichSu.Count > 0;
    public bool TrongDanhSach => !DangTai && DanhSach.Count == 0;
    public bool HienKhungXuLy => CoTheXuLy && ChiTiet is { TrangThaiXuLy: "CAN_XU_LY" };
    public bool KhongCoTheXuLyNhungCanXuLy => !CoTheXuLy && ChiTiet is { TrangThaiXuLy: "CAN_XU_LY" };
    /// <summary>Khoản chưa xác định được yêu cầu thanh toán thì không có khách/booking/hóa đơn/số phải thu để hiện.</summary>
    public bool CoYeuCau => ChiTiet?.MaGiaoDich is not null;
    public string MoTaHuong => HuongXuLy switch
    {
        "DA_XU_LY_NGOAI" => "Bạn (hoặc Kế toán) đã giải quyết bên ngoài hệ thống - ví dụ đã ghi nhận thu bổ sung vào hóa đơn / cọc, hoặc đã liên hệ khách. Chỉ đánh dấu đã xử lý, không đổi số liệu tiền.",
        "BO_QUA" => "Không phải tiền của khách sạn hoặc giao dịch thử: đóng khoản này, không hoàn, không ghi nhận.",
        _ => "Chọn cách xử lý khoản bất thường này."
    };

    public DoiSoatChuyenKhoanViewModel(ApiClient api)
    {
        _api = api;
        _dongHo = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _dongHo.Tick += (_, _) => { if (!DangXuLy) _ = NapDuLieuAsync(true); };
        _dongHo.Start();
        _ = TaiAsync();
    }

    public void Dispose() => _dongHo.Stop();

    private BoLocDoiSoat BoLoc() => new()
    {
        TuKhoa = TuKhoa, Loai = LocLoai, TrangThaiXuLy = LocXuLy,
        TuNgay = TuNgay is { } t ? DateOnly.FromDateTime(t) : null, DenNgay = DenNgay is { } d ? DateOnly.FromDateTime(d) : null
    };

    partial void OnLocLoaiChanged(string value) => _ = TaiAsync();
    partial void OnLocXuLyChanged(string value) => _ = TaiAsync();
    partial void OnTuNgayChanged(DateTime? value) => _ = TaiAsync();
    partial void OnDenNgayChanged(DateTime? value) => _ = TaiAsync();
    partial void OnLocGiaoDichChanged(string value) => _ = TaiGiaoDichAsync();
    partial void OnChiTietChanged(ChiTietDoiSoatDto? value)
    {
        OnPropertyChanged(nameof(CoChiTiet));
        OnPropertyChanged(nameof(CoYeuCau));
        OnPropertyChanged(nameof(HienKhungXuLy));
        OnPropertyChanged(nameof(KhongCoTheXuLyNhungCanXuLy));
    }
    partial void OnHuongXuLyChanged(string value)
    {
        OnPropertyChanged(nameof(MoTaHuong));
    }
    partial void OnTongQuanChanged(TongQuanDoiSoatDto? value) => OnPropertyChanged(nameof(CoTongQuan));

    partial void OnDongDaChonChanged(DongDoiSoatDto? value)
    {
        ChiTiet = null;
        LichSu.Clear();
        CungGiaoDich.Clear();
        HuongXuLy = "";
        GhiChuXuLy = "";
        OnPropertyChanged(nameof(CoLichSu));
        OnPropertyChanged(nameof(CoCungGiaoDich));
        if (value is not null) _ = TaiChiTietAsync(value.MaThongBao);
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }

    [RelayCommand]
    private Task TaiAsync() => NapDuLieuAsync(false);

    private async Task NapDuLieuAsync(bool giuChon)
    {
        var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var loc = BoLoc();
            var tongQuanTask = _api.GetTongQuanDoiSoatAsync(loc.TuNgay, loc.DenNgay);
            var danhSachTask = _api.GetDanhSachDoiSoatAsync(loc);
            var tongQuan = await tongQuanTask;
            var ds = await danhSachTask;
            if (lan != _lanTai) return;

            var dangChon = DongDaChon?.MaThongBao;
            TongQuan = tongQuan;
            NhomNgoaiLe.Clear();
            foreach (var n in tongQuan.NhomNgoaiLe) NhomNgoaiLe.Add(n);
            DanhSach.Clear();
            foreach (var d in ds) DanhSach.Add(d);
            TieuDeDanhSach = $"{ds.Count} thông báo ngân hàng" + (ds.Count >= 200 ? " (hiển thị 200 đầu - thu hẹp bộ lọc)" : "");
            DuLieuCu = false;
            if (dangChon is not null && DanhSach.FirstOrDefault(x => x.MaThongBao == dangChon) is { } giu && DongDaChon?.MaThongBao != dangChon)
                DongDaChon = giu;
            else if (dangChon is not null && giuChon && !DangXuLy)
                _ = TaiChiTietAsync(dangChon.Value);
            await TaiGiaoDichAsync();
            if (!giuChon) ThongBao = "";
        }
        catch (Exception ex) when (ex is LoiYeuCauApi or HttpRequestException)
        {
            if (lan == _lanTai)
            {
                DuLieuCu = TongQuan is not null;   // giữ dữ liệu cũ nhưng báo rõ là có thể đã cũ
                DatThongBao(ex is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {ex.Message}", true);
            }
        }
        finally
        {
            if (lan == _lanTai)
            {
                DangTai = false;
                OnPropertyChanged(nameof(TrongDanhSach));
            }
        }
    }

    private async Task TaiGiaoDichAsync()
    {
        try
        {
            var gd = await _api.GetDanhSachChuyenKhoanAsync(string.IsNullOrEmpty(LocGiaoDich) ? null : LocGiaoDich);
            GiaoDich.Clear();
            foreach (var g in gd) GiaoDich.Add(g);
        }
        catch (Exception ex) when (ex is LoiYeuCauApi or HttpRequestException) { /* danh sách yêu cầu là phần phụ; lỗi chính đã báo ở TaiAsync */ }
    }

    private async Task TaiChiTietAsync(int ma)
    {
        var lan = ++_lanChiTiet;
        try
        {
            var ct = await _api.GetChiTietDoiSoatAsync(ma);
            if (lan != _lanChiTiet || DongDaChon?.MaThongBao != ma) return;
            ChiTiet = ct;
            LichSu.Clear();
            foreach (var l in ct.LichSu) LichSu.Add(l);
            CungGiaoDich.Clear();
            foreach (var c in ct.ThongBaoCungGiaoDich) CungGiaoDich.Add(c);
            OnPropertyChanged(nameof(CoLichSu));
            OnPropertyChanged(nameof(CoCungGiaoDich));
        }
        catch (LoiYeuCauApi loi)
        {
            if (DongDaChon?.MaThongBao == ma) DatThongBao($"Không tải được chi tiết khoản #{ma}: {loi.Message}", true);
        }
    }

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = "";
        LocLoai = "";
        LocXuLy = "";
        TuNgay = DateTime.Today.AddDays(-29);
        DenNgay = DateTime.Today;
    }

    /// <summary>Bấm vào thẻ ngoại lệ / thẻ thống kê để lọc nhanh.</summary>
    [RelayCommand]
    private void LocNhanh(string kieu)
    {
        switch (kieu)
        {
            case "CAN_XU_LY": LocLoai = ""; LocXuLy = "CAN_XU_LY"; break;
            case "KHOP": LocLoai = "KHOP"; LocXuLy = ""; break;
            case "CHUA_KHOP": LocLoai = ""; LocXuLy = "DA_XU_LY"; break;
            case "TAT_CA": LocLoai = ""; LocXuLy = ""; break;
            default: LocLoai = kieu; LocXuLy = ""; break;
        }
    }

    [RelayCommand]
    private async Task XuatAsync()
    {
        try
        {
            var (noiDung, tenFile) = await _api.XuatDoiSoatAsync(BoLoc());
            var duongDan = ChonNoiLuu(tenFile, ".xlsx");
            if (duongDan is null) return;
            await System.IO.File.WriteAllBytesAsync(duongDan, noiDung);
            DatThongBao($"Đã xuất {DanhSach.Count} dòng đối soát ra {duongDan}", false);
        }
        catch (Exception ex) when (ex is LoiYeuCauApi or HttpRequestException or System.IO.IOException or UnauthorizedAccessException)
        {
            DatThongBao("Không xuất được báo cáo: " + (ex is LoiYeuCauApi l ? l.Message : ex.Message), true);
        }
    }

    [RelayCommand]
    private async Task XuLyAsync()
    {
        if (DangXuLy || ChiTiet is null || !CoTheXuLy) return;
        var ct = ChiTiet;
        if (ct.TrangThaiXuLy != "CAN_XU_LY") { DatThongBao("Khoản này không còn ở trạng thái chờ xử lý.", true); return; }
        if (string.IsNullOrEmpty(HuongXuLy)) { DatThongBao("Chọn cách xử lý khoản này.", true); return; }
        if (string.IsNullOrWhiteSpace(GhiChuXuLy)) { DatThongBao("Nhập ghi chú xử lý (bắt buộc, được lưu vào nhật ký).", true); return; }

        var tenHuong = HuongXuLy switch { "DA_XU_LY_NGOAI" => "ĐÃ XỬ LÝ NGOÀI HỆ THỐNG", _ => "BỎ QUA" };
        if (!HopThoai.XacNhan("Xác nhận xử lý khoản chuyển khoản",
                $"Khoản {ct.SoTienNganHang:N0}đ · mã NH {ct.MaGiaoDichNganHang}\nLoại: {ct.TenLoai}\n\nCách xử lý: {tenHuong}\n{MoTaHuong}\n\nGhi chú: {GhiChuXuLy.Trim()}\n\nThao tác được ghi nhật ký và chỉ làm được MỘT lần, không hoàn tác. Tiếp tục?"))
            return;

        DangXuLy = true;
        try
        {
            await _api.XuLyThongBaoNganHangAsync(ct.MaThongBao, HuongXuLy, GhiChuXuLy.Trim());
            DatThongBao($"Đã xử lý khoản {ct.MaGiaoDichNganHang} ({tenHuong.ToLowerInvariant()}).", false);
            var ma = ct.MaThongBao;
            HuongXuLy = "";
            GhiChuXuLy = "";
            await NapDuLieuAsync(true);
            await TaiChiTietAsync(ma);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao("Chưa xử lý được: " + loi.Message, true);
            if (loi.MaLoi is "THONG_BAO_DA_XU_LY" or "THONG_BAO_DA_KHOP") { await NapDuLieuAsync(true); await TaiChiTietAsync(ct.MaThongBao); }
        }
        finally
        {
            DangXuLy = false;
        }
    }
}
