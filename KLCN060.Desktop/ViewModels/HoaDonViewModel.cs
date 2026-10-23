using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D20 - Hóa đơn. Danh sách + thẻ thống kê (tính trên TOÀN BỘ hóa đơn khớp bộ lọc) + panel tổng quan một hóa đơn
/// (của ai, booking/phòng nào, cơ cấu tiền, ai thu/khi nào, lịch sử, ai được làm gì).
/// Quản lý chỉ xem và kiểm soát; Kế toán/Lễ tân (quyền THANH_TOAN) mở màn hình thanh toán.
/// </summary>
public partial class HoaDonViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int _lanTai;
    private int _lanTaiChiTiet;

    /// <summary>Phát ra mã hóa đơn khi người dùng bấm "Mở thanh toán" - ManHinhChinhViewModel chuyển sang D8.</summary>
    public event Action<string>? MoThanhToan;

    public ObservableCollection<HoaDonTomTatDto> DanhSach { get; } = [];

    [ObservableProperty] private string tuKhoa = "";
    /// <summary>Rỗng = tất cả; hoặc CHUA_THANH_TOAN / THANH_TOAN_MOT_PHAN / DA_THANH_TOAN.</summary>
    [ObservableProperty] private string locTrangThai = "";
    /// <summary>Rỗng = tất cả; hoặc QUA_HAN.</summary>
    [ObservableProperty] private string locDacBiet = "";
    [ObservableProperty] private DateTime? tuNgay;
    [ObservableProperty] private DateTime? denNgay;
    /// <summary>Rỗng = mới nhất; con_no / tong_tien / qua_han.</summary>
    [ObservableProperty] private string sapXep = "";
    [ObservableProperty] private HoaDonTomTatDto? hoaDonDaChon;
    [ObservableProperty] private ThongKeHoaDonDto? thongKe;
    [ObservableProperty] private TongQuanHoaDonDto? tongQuan;
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool dangTai;

    /// <summary>Có quyền thu tiền (THANH_TOAN): mở màn hình thanh toán. Người chỉ có quyền báo cáo (Quản lý) chỉ xem.</summary>
    public bool CoTheThanhToan { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThanhToan);
    public bool CheDoChiXem => !CoTheThanhToan;
    /// <summary>Lịch sử thao tác chỉ hiện cho Kế toán; Lễ tân chỉ thu tiền và tra cứu; Quản lý chỉ theo dõi hóa đơn và công nợ.</summary>
    public bool LaLeTan { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan) && !AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);
    public bool HienDieuChinh => CoTheThanhToan && !LaLeTan;
    public string PhuDe => LaLeTan
        ? "Tra cứu hóa đơn theo khách, phòng, mã; chọn một hóa đơn để xem của ai, booking/phòng nào, cơ cấu tiền, ai thu và còn nợ bao nhiêu, rồi bấm Mở thanh toán để thu tiền."
        : CoTheThanhToan
        ? "Chọn một hóa đơn để thấy của ai, booking/phòng nào, ai thu và lịch sử thao tác. Thẻ thống kê tính trên toàn bộ hóa đơn khớp bộ lọc."
        : "Chọn một hóa đơn để xem của ai, booking/phòng nào, cơ cấu tiền, ai thu và còn nợ bao nhiêu. Thẻ thống kê tính trên toàn bộ hóa đơn khớp bộ lọc.";

    public bool CoThongKe => ThongKe is not null;
    public bool CoTongQuan => TongQuan is not null;
    public bool TrongDanhSach => !DangTai && DanhSach.Count == 0;
    public string TieuDeDanhSach => $"{DanhSach.Count} hóa đơn" + (ThongKe is { } t && t.TongHoaDon > DanhSach.Count ? $" (hiển thị {DanhSach.Count}/{t.TongHoaDon}, thu hẹp bộ lọc để xem phần còn lại)" : "");

    /// <param name="tuKhoaBanDau">Từ khóa lọc sẵn (VD: số điện thoại khách) khi mở từ màn khác.</param>
    public HoaDonViewModel(ApiClient api, string? tuKhoaBanDau = null)
    {
        _api = api;
        if (!string.IsNullOrWhiteSpace(tuKhoaBanDau)) TuKhoa = tuKhoaBanDau.Trim();
        _ = TaiAsync();
    }

    private BoLocHoaDon BoLoc() => new()
    {
        TuKhoa = TuKhoa, TrangThai = LocTrangThai, DacBiet = LocDacBiet, TuNgay = TuNgay, DenNgay = DenNgay, SapXep = SapXep
    };

    partial void OnHoaDonDaChonChanged(HoaDonTomTatDto? value)
    {
        TongQuan = null;
        if (value is not null) _ = TaiChiTietAsync(value.MaHD);
    }

    partial void OnTongQuanChanged(TongQuanHoaDonDto? value)
    {
        OnPropertyChanged(nameof(CoTongQuan));
    }

    partial void OnThongKeChanged(ThongKeHoaDonDto? value)
    {
        OnPropertyChanged(nameof(CoThongKe));
        OnPropertyChanged(nameof(TieuDeDanhSach));
    }

    private async Task TaiChiTietAsync(string maHD)
    {
        var lan = ++_lanTaiChiTiet;
        try
        {
            var tongQuan = await _api.GetTongQuanHoaDonAsync(maHD);
            if (HoaDonDaChon?.MaHD != maHD || lan != _lanTaiChiTiet) return;
            TongQuan = tongQuan;
        }
        catch (LoiYeuCauApi loi)
        {
            if (HoaDonDaChon?.MaHD == maHD) DatThongBao($"Không tải được chi tiết hóa đơn {maHD}: {loi.Message}", true);
        }
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }

    partial void OnLocTrangThaiChanged(string value) => _ = TaiAsync();
    partial void OnLocDacBietChanged(string value) => _ = TaiAsync();
    partial void OnSapXepChanged(string value) => _ = TaiAsync();
    partial void OnTuNgayChanged(DateTime? value) => _ = TaiAsync();
    partial void OnDenNgayChanged(DateTime? value) => _ = TaiAsync();

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = "";
        LocTrangThai = "";
        LocDacBiet = "";
        SapXep = "";
        TuNgay = null;
        DenNgay = null;
        _ = TaiAsync();
    }

    /// <summary>Bấm vào thẻ thống kê để lọc nhanh danh sách.</summary>
    [RelayCommand]
    private void LocNhanh(string kieu)
    {
        switch (kieu)
        {
            case "NO": LocTrangThai = ""; LocDacBiet = ""; SapXep = "con_no"; break;
            case "QUA_HAN": LocTrangThai = ""; LocDacBiet = "QUA_HAN"; SapXep = "qua_han"; break;
            default: LocTrangThai = ""; LocDacBiet = ""; SapXep = ""; break;
        }
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        // Chỉ hiển thị kết quả của lần tải MỚI NHẤT (các lần tải có thể chồng nhau khi đổi bộ lọc liên tiếp).
        var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var loc = BoLoc();
            var danhSachTask = _api.GetDanhSachHoaDonAsync(loc);
            var thongKeTask = _api.GetThongKeHoaDonAsync(loc);
            var danhSach = await danhSachTask;
            var thongKe = await thongKeTask;
            if (lan != _lanTai) return;

            var dangChon = HoaDonDaChon?.MaHD;
            DanhSach.Clear();
            foreach (var hoaDon in danhSach) DanhSach.Add(hoaDon);
            ThongKe = thongKe;
            CapNhatLuc = $"Cập nhật {thongKe.CapNhatLuc:HH:mm:ss}";
            // Giữ hóa đơn đang xem sau khi làm mới (nếu vẫn còn trong kết quả) - không làm mất ngữ cảnh người dùng.
            if (dangChon is not null) HoaDonDaChon = DanhSach.FirstOrDefault(x => x.MaHD == dangChon);
            OnPropertyChanged(nameof(TieuDeDanhSach));
            ThongBao = "";
        }
        catch (LoiYeuCauApi loi)
        {
            if (lan == _lanTai) DatThongBao(loi.Message, true);
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

    [RelayCommand]
    private void MoThanhToanHoaDon()
    {
        if (!CoTheThanhToan) return;
        if (HoaDonDaChon is null)
        {
            ThongBao = "Vui lòng chọn một hóa đơn trong bảng.";
            return;
        }

        MoThanhToan?.Invoke(HoaDonDaChon.MaHD);
    }
}
