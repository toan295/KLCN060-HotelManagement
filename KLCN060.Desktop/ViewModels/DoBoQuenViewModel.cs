using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Ảnh của món đồ đang chọn (tải từ API có xác thực, không có đường công khai).</summary>
public sealed record AnhXemViewModel(int MaAnh, ImageSource? Nguon, string ChuThich);

/// <summary>
/// Tab "Đồ bỏ quên" (Lost &amp; Found): ghi nhận (phòng/khu vực, thời gian, loại, mô tả, người nhặt, vị trí lưu giữ, ảnh), lưu kho, trả khách (lễ tân, có người nhận + giấy tờ), thanh lý (quản lý).
/// Buồng phòng không thấy thông tin khách của phòng.
/// </summary>
public partial class DoBoQuenViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;

    public static IReadOnlyList<LuaChon> DanhSachLoai { get; } =
        [new("GIAY_TO", "Giấy tờ"), new("DIEN_TU", "Thiết bị điện tử"), new("VI_TIEN", "Ví / tiền"), new("TRANG_SUC", "Trang sức / phụ kiện"), new("QUAN_AO", "Quần áo"), new("Y_TE", "Thuốc / đồ y tế"), new("KHAC", "Khác")];
    public static IReadOnlyList<LuaChon> DanhSachLoc { get; } =
        [new("DANG_CON", "Chưa trả / chưa thanh lý"), new("DA_TIM_THAY", "Đã tìm thấy"), new("DANG_LUU_GIU", "Đang lưu giữ"), new("DA_TRA_KHACH", "Đã trả khách"), new("DA_THANH_LY", "Đã thanh lý"), new("", "Tất cả")];

    private static readonly PhongDto KhuVucChung = new() { MaPhong = "", TenPhong = "(Khu vực chung - nhập bên dưới)" };

    /// <summary>Ghi nhận đồ bỏ quên là việc của buồng phòng / lễ tân (người nhặt được); Quản lý giám sát, duyệt trả và thanh lý.</summary>
    public bool CoQuyenGhi { get; } = AppSession.Current.CoQuyen(QuyenHeThong.GhiNhanBuongPhong) && !AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);
    // Đồ bỏ quên do Buồng phòng ghi nhận; lễ tân chỉ xem thông tin (để trả lời khách), việc trả đồ/thanh lý do Quản lý xử lý.
    public bool CoQuyenTra { get; } = AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);
    public bool CoChiXem { get; } = !AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu) && !AppSession.Current.CoQuyen(QuyenHeThong.GhiNhanBuongPhong);
    /// <summary>Hàng lưu kho / thêm ảnh / sửa thông tin: ẩn với lễ tân (chỉ xem đồ bỏ quên, kể cả khi được cấp thêm quyền ghi nhận), hiện với Buồng phòng và Quản lý.</summary>
    public bool CoQuyenXemSua => !CoChiXem && AppSession.Current.VaiTro != "LE_TAN";
    public bool CoQuyenThanhLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.DuyetNghiepVu);

    public ObservableCollection<DoBoQuenDto> DanhSach { get; } = [];
    public ObservableCollection<PhongDto> Phongs { get; } = [];
    public ObservableCollection<AnhXemViewModel> Anhs { get; } = [];

    [ObservableProperty] private DoBoQuenDto? doDaChon;
    [ObservableProperty] private LuaChon? loc;
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string tomTat = "";

    // Biểu mẫu ghi nhận / sửa
    [ObservableProperty] private PhongDto? phongPhatHien;
    [ObservableProperty] private string khuVuc = "";
    [ObservableProperty] private LuaChon? loai;
    [ObservableProperty] private string moTa = "";
    [ObservableProperty] private string nguoiNhat = "";
    [ObservableProperty] private string viTriGhi = "";
    [ObservableProperty] private DateTime thoiGianPhatHien = DateTime.Now;
    [ObservableProperty] private int? maDoDangSua;

    // Thao tác trên món đã chọn
    [ObservableProperty] private string viTriLuuKho = "";
    [ObservableProperty] private string nguoiNhanLai = "";
    [ObservableProperty] private string giayToNguoiNhan = "";
    [ObservableProperty] private string ghiChuTra = "";
    [ObservableProperty] private string lyDoThanhLy = "";

    public bool DangSua => MaDoDangSua is not null;
    public string NutGhi => DangSua ? "Lưu thay đổi" : "Ghi nhận đồ bỏ quên";
    public bool CoDo => DoDaChon is not null;
    public bool CoTheThayDoi => CoDo && DoDaChon!.DangCon;
    public bool CoTheLuuKho => CoQuyenGhi && CoTheThayDoi;
    public bool CoTheTra => CoQuyenTra && CoTheThayDoi;
    public bool CoTheThanhLy => CoQuyenThanhLy && DoDaChon is { TrangThai: "DANG_LUU_GIU" };
    public bool CoTheThemAnh => CoQuyenGhi && CoTheThayDoi && Anhs.Count < 3;
    public bool CoKhachGoiY => DoDaChon is { HoTenKhach: { Length: > 0 } };

    public DoBoQuenViewModel(ApiClient api)
    {
        _api = api;
        loc = DanhSachLoc[0];
        loai = DanhSachLoai[6];
        Anhs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CoTheThemAnh));
        _ = TaiAsync();
    }

    partial void OnLocChanged(LuaChon? value) { _ = TaiAsync(); }
    partial void OnMaDoDangSuaChanged(int? value) { OnPropertyChanged(nameof(DangSua)); OnPropertyChanged(nameof(NutGhi)); }

    partial void OnDoDaChonChanged(DoBoQuenDto? value)
    {
        foreach (var n in new[] { nameof(CoDo), nameof(CoTheThayDoi), nameof(CoTheLuuKho), nameof(CoTheTra), nameof(CoTheThanhLy), nameof(CoTheThemAnh), nameof(CoKhachGoiY) })
            OnPropertyChanged(n);
        ViTriLuuKho = value?.ViTriLuuGiu ?? "";
        _ = TaiAnhAsync(value);
    }

    private int _lanTaiAnh;

    private async Task TaiAnhAsync(DoBoQuenDto? doVat)
    {
        var lan = ++_lanTaiAnh;   // chọn món khác giữa chừng thì bỏ lượt tải cũ
        Anhs.Clear();
        if (doVat is null) return;
        var moi = new List<AnhXemViewModel>();
        foreach (var a in doVat.Anhs)
        {
            ImageSource? nguon = null;
            try { nguon = TaoAnh(await _api.TaiAnhDoBoQuenAsync(doVat.MaDo, a.MaAnh)); }
            catch (LoiYeuCauApi) { /* ảnh lỗi thì chỉ hiện khung trống */ }
            if (lan != _lanTaiAnh) return;
            moi.Add(new AnhXemViewModel(a.MaAnh, nguon, $"{a.ThoiGian:dd/MM HH:mm} · {a.KichThuocByte / 1024} KB"));
        }
        if (lan != _lanTaiAnh) return;
        Anhs.Clear();
        foreach (var a in moi) Anhs.Add(a);
    }

    private static ImageSource? TaoAnh(byte[] duLieu)
    {
        try
        {
            var anh = new BitmapImage();
            anh.BeginInit();
            anh.CacheOption = BitmapCacheOption.OnLoad;
            anh.StreamSource = new MemoryStream(duLieu);
            anh.DecodePixelWidth = 320;
            anh.EndInit();
            anh.Freeze();
            return anh;
        }
        catch { return null; }
    }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            if (Phongs.Count == 0)
            {
                Phongs.Add(KhuVucChung);
                foreach (var p in (await _api.GetRoomsAsync()).OrderBy(x => x.TenPhong, StringComparer.Ordinal)) Phongs.Add(p);
                PhongPhatHien ??= KhuVucChung;
            }
            var ma = DoDaChon?.MaDo;
            var ds = await _api.GetDoBoQuenAsync(string.IsNullOrEmpty(Loc?.Ma) ? null : Loc.Ma, TuKhoa);
            DanhSach.Clear();
            foreach (var x in ds) DanhSach.Add(x);
            DoDaChon = ma is null ? null : DanhSach.FirstOrDefault(x => x.MaDo == ma);
            TomTat = $"{ds.Count} món · {ds.Count(x => x.DangCon)} chưa trả/thanh lý · {ds.Count(x => x.DuDieuKienThanhLy)} đủ hạn thanh lý (lưu giữ từ 30 ngày)";
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    // ------------------------------------------------------------ ghi nhận / sửa

    [RelayCommand]
    private async Task GhiNhanAsync()
    {
        if (Loai is null) { Loi("Chọn loại đồ."); return; }
        if (string.IsNullOrWhiteSpace(MoTa)) { Loi("Nhập mô tả đồ vật (màu sắc, nhãn hiệu, đặc điểm nhận dạng)."); return; }
        var maPhong = string.IsNullOrEmpty(PhongPhatHien?.MaPhong) ? null : PhongPhatHien!.MaPhong;
        var khuVuc = string.IsNullOrWhiteSpace(KhuVuc) ? null : KhuVuc.Trim();
        if (maPhong is null && khuVuc is null) { Loi("Chọn phòng phát hiện, hoặc nhập khu vực (sảnh, nhà hàng...)."); return; }
        var nguoiNhat = string.IsNullOrWhiteSpace(NguoiNhat) ? null : NguoiNhat.Trim();
        try
        {
            DoBoQuenDto kq;
            if (MaDoDangSua is { } maSua)
                kq = await _api.SuaDoBoQuenAsync(maSua, new YeuCauSuaDoBoQuen(maPhong, khuVuc, Loai.Ma, MoTa.Trim(), nguoiNhat));
            else
                kq = await _api.GhiNhanDoBoQuenAsync(new YeuCauDoBoQuen(maPhong, khuVuc, ThoiGianPhatHien > DateTime.Now ? DateTime.Now : ThoiGianPhatHien, Loai.Ma, MoTa.Trim(), nguoiNhat, string.IsNullOrWhiteSpace(ViTriGhi) ? null : ViTriGhi.Trim()));
            var sua = MaDoDangSua is not null;
            XoaBieuMau();
            await TaiAsync();
            DoDaChon = DanhSach.FirstOrDefault(x => x.MaDo == kq.MaDo);
            Ok(sua ? $"Đã cập nhật {kq.MaHienThi}." : $"Đã ghi nhận {kq.MaHienThi} ({kq.LoaiHienThi}, {kq.NoiPhatHienHienThi}) - {kq.TrangThaiHienThi.ToLowerInvariant()}. Bấm \"Thêm ảnh\" để đính kèm hình.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private void NapDeSua()
    {
        if (DoDaChon is not { DangCon: true } d) { Loi("Chọn một món chưa trả/thanh lý để sửa."); return; }
        MaDoDangSua = d.MaDo;
        PhongPhatHien = Phongs.FirstOrDefault(x => x.MaPhong == (d.MaPhong ?? "")) ?? KhuVucChung;
        KhuVuc = d.KhuVuc ?? "";
        Loai = DanhSachLoai.FirstOrDefault(x => x.Ma == d.Loai);
        MoTa = d.MoTa;
        NguoiNhat = d.NguoiNhat;
        ThongBao = "";
    }

    [RelayCommand]
    private void HuySua() => XoaBieuMau();

    private void XoaBieuMau()
    {
        MaDoDangSua = null;
        KhuVuc = ""; MoTa = ""; NguoiNhat = ""; ViTriGhi = "";
        ThoiGianPhatHien = DateTime.Now;
        Loai = DanhSachLoai[6];
        PhongPhatHien = Phongs.FirstOrDefault();
    }

    // ------------------------------------------------------------ vòng đời

    [RelayCommand]
    private async Task LuuKhoAsync()
    {
        if (DoDaChon is not { DangCon: true } d) { Loi("Chọn một món chưa trả/thanh lý."); return; }
        if (string.IsNullOrWhiteSpace(ViTriLuuKho)) { Loi("Nhập vị trí lưu giữ (VD: Tủ A - ngăn 2)."); return; }
        try
        {
            var kq = await _api.LuuKhoDoBoQuenAsync(d.MaDo, ViTriLuuKho.Trim());
            await TaiAsync();
            Ok($"{kq.MaHienThi} đang lưu giữ tại \"{kq.ViTriLuuGiu}\".");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task TraKhachAsync()
    {
        if (DoDaChon is not { DangCon: true } d) { Loi("Chọn một món chưa trả/thanh lý."); return; }
        if (string.IsNullOrWhiteSpace(NguoiNhanLai)) { Loi("Nhập tên người nhận lại."); return; }
        if (string.IsNullOrWhiteSpace(GiayToNguoiNhan)) { Loi("Nhập giấy tờ đối chiếu của người nhận (CCCD, hộ chiếu...)."); return; }
        if (!HopThoai.XacNhan("Trả đồ cho khách", $"Trả {d.MaHienThi} ({d.MoTa}) cho \"{NguoiNhanLai.Trim()}\" - giấy tờ {GiayToNguoiNhan.Trim()}?\n\nSau khi trả, món đồ đóng lại và không thay đổi được nữa.")) return;
        try
        {
            var kq = await _api.TraDoBoQuenAsync(d.MaDo, NguoiNhanLai.Trim(), GiayToNguoiNhan.Trim(), string.IsNullOrWhiteSpace(GhiChuTra) ? null : GhiChuTra.Trim());
            NguoiNhanLai = ""; GiayToNguoiNhan = ""; GhiChuTra = "";
            await TaiAsync();
            Ok($"Đã trả {kq.MaHienThi} cho {kq.NguoiNhanLai}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task ThanhLyAsync()
    {
        if (DoDaChon is not { TrangThai: "DANG_LUU_GIU" } d) { Loi("Chỉ thanh lý được món đang lưu giữ."); return; }
        if (string.IsNullOrWhiteSpace(LyDoThanhLy)) { Loi("Nhập lý do thanh lý."); return; }
        if (!HopThoai.XacNhan("Thanh lý đồ bỏ quên", $"Thanh lý {d.MaHienThi} ({d.MoTa})?\n\nĐã lưu giữ {d.SoNgayLuuGiu} ngày. Chỉ thanh lý được sau tối thiểu 30 ngày; sau khi thanh lý không hoàn tác được.")) return;
        try
        {
            var kq = await _api.ThanhLyDoBoQuenAsync(d.MaDo, LyDoThanhLy.Trim());
            LyDoThanhLy = "";
            await TaiAsync();
            Ok($"Đã thanh lý {kq.MaHienThi}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    // ------------------------------------------------------------ ảnh

    /// <summary>Đường dẫn ảnh do người dùng chọn (hộp thoại mở file); kiểm thử đặt trực tiếp rồi gọi <see cref="ThemAnhAsync"/>.</summary>
    public Func<string?> ChonFileAnh { get; set; } = () =>
    {
        var hopThoai = new Microsoft.Win32.OpenFileDialog { Title = "Chọn ảnh đồ bỏ quên", Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp" };
        return hopThoai.ShowDialog() == true ? hopThoai.FileName : null;
    };

    [RelayCommand]
    public async Task ThemAnhAsync()
    {
        if (DoDaChon is not { DangCon: true } d) { Loi("Chọn một món chưa trả/thanh lý để thêm ảnh."); return; }
        var duongDan = ChonFileAnh();
        if (string.IsNullOrEmpty(duongDan)) return;
        try
        {
            await _api.ThemAnhDoBoQuenAsync(d.MaDo, duongDan);
            await TaiAsync();
            Ok($"Đã thêm ảnh cho {d.MaHienThi}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
        catch (IOException loi) { Loi("Không đọc được file ảnh: " + loi.Message); }
    }

    [RelayCommand]
    private async Task XoaAnhAsync(AnhXemViewModel? anh)
    {
        if (DoDaChon is not { DangCon: true } d || anh is null) return;
        if (!HopThoai.XacNhan("Xóa ảnh", "Xóa ảnh này khỏi món đồ?")) return;
        try
        {
            await _api.XoaAnhDoBoQuenAsync(d.MaDo, anh.MaAnh);
            await TaiAsync();
            Ok("Đã xóa ảnh.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }
}
