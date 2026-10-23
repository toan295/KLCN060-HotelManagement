using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D11 - Quản lý danh mục phòng: tab Phòng (thống kê theo tầng/loại/tình trạng, tìm-lọc-sắp xếp, thêm/sửa/xóa an toàn, thêm hàng loạt, đổi tình trạng đúng sơ đồ),
/// tab Loại phòng (giá, sức chứa, diện tích, giường, tiện nghi, mô tả) và tab Ảnh &amp; Website.
/// Phòng đã phát sinh dữ liệu không xóa được (chuyển sang bảo trì); đổi loại phòng bị chặn khi phòng đang có khách / đặt phòng sắp tới.
/// </summary>
public partial class QuanLyDanhMucPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private int _lanTai;
    private bool _dangHoanTac;
    private (string Ten, string Tang, string Loai) _gocPhong = ("", "", "");
    private (string Ten, string SucChua, string Gia, string PhuThu, string DienTich, string Giuong, string TienNghi, string MoTa) _gocLoai = ("", "", "", "", "", "", "", "");

    public ObservableCollection<PhongDanhMucDto> Phongs { get; } = [];
    public ObservableCollection<LoaiPhongDanhMucDto> RoomTypes { get; } = [];
    public ObservableCollection<DemDanhMucDto> TheoTang { get; } = [];
    public ObservableCollection<DemDanhMucDto> TheoLoai { get; } = [];
    /// <summary>Danh sách thả xuống lọc theo loại phòng: có mục "Tất cả loại phòng" để bỏ lọc.</summary>
    public ObservableCollection<LuaChon> LoaiLocTuyChon { get; } = [new("", "Tất cả loại phòng")];
    public ObservableCollection<DemDanhMucDto> TheoTinhTrang { get; } = [];
    public ObservableCollection<string> TinhTrangChoPhep { get; } = [];

    [ObservableProperty] private int tabDangChon;
    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool messageLaLoi = true;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private DanhMucPhongDto? tongQuan;
    [ObservableProperty] private string tieuDePhong = "";

    // ---- Bộ lọc phòng
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locTang = "";
    [ObservableProperty] private string locLoai = "";
    [ObservableProperty] private string locTinhTrang = "";
    [ObservableProperty] private string sapXep = "";

    // ---- Form phòng
    [ObservableProperty] private PhongDanhMucDto? selectedRoom;
    [ObservableProperty] private bool dangThemPhong;
    [ObservableProperty] private string roomName = "";
    [ObservableProperty] private string floorText = "1";
    [ObservableProperty] private string roomTypeCode = "";
    [ObservableProperty] private string tinhTrangMoi = "";
    [ObservableProperty] private string lyDoTinhTrang = "";
    [ObservableProperty] private string trangThaiPhongThongBao = "";

    // ---- Thêm hàng loạt
    [ObservableProperty] private string hangLoatTang = "";
    [ObservableProperty] private string hangLoatTu = "";
    [ObservableProperty] private string hangLoatDen = "";
    [ObservableProperty] private string hangLoatTienTo = "";
    [ObservableProperty] private string hangLoatLoai = "";
    [ObservableProperty] private string hangLoatXemTruoc = "";

    // ---- Form loại phòng
    [ObservableProperty] private LoaiPhongDanhMucDto? selectedRoomType;
    [ObservableProperty] private bool dangThemLoai;
    [ObservableProperty] private string typeName = "";
    [ObservableProperty] private string capacityText = "2";
    [ObservableProperty] private string priceText = "";
    [ObservableProperty] private string surchargeText = "200000";
    [ObservableProperty] private string dienTichText = "";
    [ObservableProperty] private string loaiGiuong = "";
    [ObservableProperty] private string tienNghi = "";
    [ObservableProperty] private string moTa = "";

    // ---- Ảnh phòng (tab 3): theo loại phòng đang chọn + banner chung
    public ObservableCollection<AnhPhongSlotViewModel> AnhCuaLoaiPhong { get; } = [];
    public AnhPhongSlotViewModel AnhBanner { get; }
    [ObservableProperty] private string huongDanAnh = "Chọn một loại phòng ở tab \"Loại phòng\" để xem và thay ảnh của loại phòng đó.";
    [ObservableProperty] private string thongBaoAnh = "";
    [ObservableProperty] private bool thongBaoAnhLaLoi;
    [ObservableProperty] private LoaiPhongDanhMucDto? loaiChoAnh;

    public bool CoTheQuanLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyPhong);
    public bool CoTheDatGia { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyGia);

    public bool CoTongQuan => TongQuan is not null;
    public bool CoForm => DangThemPhong || SelectedRoom is not null;
    public bool KhongCoForm => !CoForm;
    public bool CoThayDoiPhong => CoForm && (RoomName, FloorText, RoomTypeCode) != _gocPhong;
    public bool CoTheDoiTinhTrang => !DangThemPhong && SelectedRoom is not null && TinhTrangChoPhep.Count > 0 && CoTheQuanLy;
    public bool CanLyDoTinhTrang => TinhTrangMoi == "OOO";
    public bool CoTheXoaPhong => !DangThemPhong && SelectedRoom?.CoTheXoa == true && CoTheQuanLy;
    public string LyDoKhongXoaPhong => SelectedRoom is null || DangThemPhong ? "" : SelectedRoom.CoTheXoa ? "" : SelectedRoom.LyDoKhongXoa ?? "";
    public string LyDoKhongDoiLoaiPhong => SelectedRoom is null || DangThemPhong || SelectedRoom.CoTheDoiLoai ? "" : SelectedRoom.LyDoKhongDoiLoai ?? "";
    public bool KhongDoiLoaiDuoc => LyDoKhongDoiLoaiPhong != "";
    public string TieuDeFormPhong => DangThemPhong ? "Thêm phòng mới" : SelectedRoom is null ? "Phòng" : $"Phòng {SelectedRoom.TenPhong} ({SelectedRoom.MaPhong})";
    public string NguonTinhTrang => "Nguồn của từng tình trạng: Đặt phòng giữ chỗ (không đổi tình trạng) · Nhận phòng → Đang có khách (OC) · Trả phòng → Có khách cần dọn / Trống chưa dọn (do luồng Trả phòng) · Buồng phòng → Trống đã dọn (VC) / đã kiểm tra (VI) · Bảo trì → Ngừng phục vụ (OOO, bắt buộc lý do). Quản lý chỉ được chuyển tay theo các lựa chọn dưới đây.";

    public bool CoFormLoai => DangThemLoai || SelectedRoomType is not null;
    public bool KhongCoFormLoai => !CoFormLoai;
    public bool CoThayDoiLoai => CoFormLoai && (TypeName, CapacityText, PriceText, SurchargeText, DienTichText, LoaiGiuong, TienNghi, MoTa) != _gocLoai;
    public bool CoTheXoaLoai => !DangThemLoai && SelectedRoomType?.CoTheXoa == true && CoTheQuanLy;
    public string LyDoKhongXoaLoai => SelectedRoomType is null || DangThemLoai ? "" : SelectedRoomType.CoTheXoa ? "" : SelectedRoomType.LyDoKhongXoa ?? "";
    public string TieuDeFormLoai => DangThemLoai ? "Thêm loại phòng mới" : SelectedRoomType is null ? "Loại phòng" : $"Loại phòng {SelectedRoomType.TenLoai} ({SelectedRoomType.MaLoai})";
    public bool KhongSuaGia => !CoTheDatGia;

    public QuanLyDanhMucPhongViewModel(ApiClient api)
    {
        _api = api;
        AnhBanner = new AnhPhongSlotViewModel(api, "hero", "Banner trang chủ", (nd, loi) => { ThongBaoAnh = nd; ThongBaoAnhLaLoi = loi; });
        _ = LoadAsync();
        _ = NapAnhAsync();
    }

    private void DatThongBao(string noiDung, bool laLoi) { Message = noiDung; MessageLaLoi = laLoi; }

    // ------------------------------------------------------------------ nạp dữ liệu
    [RelayCommand]
    private async Task LoadAsync()
    {
        var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var loaiTask = _api.GetDanhMucLoaiPhongAsync();
            var phongTask = _api.GetDanhMucPhongAsync(TuKhoa.Trim(), int.TryParse(LocTang, out var t) ? t : null, LocLoai, LocTinhTrang, SapXep);
            var loai = await loaiTask;
            var phong = await phongTask;
            if (lan != _lanTai) return;

            var maPhongChon = SelectedRoom?.MaPhong;
            var maLoaiChon = SelectedRoomType?.MaLoai;
            TongQuan = phong;
            OnPropertyChanged(nameof(CoTongQuan));
            Dem(TheoTang, phong.TheoTang); Dem(TheoLoai, phong.TheoLoai); Dem(TheoTinhTrang, phong.TheoTinhTrang);
            RoomTypes.Clear(); foreach (var x in loai) RoomTypes.Add(x);
            if (LoaiLocTuyChon.Count != loai.Count + 1)
            {
                var dangLoc = LocLoai ?? "";
                while (LoaiLocTuyChon.Count > 1) LoaiLocTuyChon.RemoveAt(1);
                foreach (var x in loai) LoaiLocTuyChon.Add(new LuaChon(x.MaLoai, x.TenLoai));
                if (LocLoai != dangLoc) LocLoai = dangLoc;
            }
            Phongs.Clear(); foreach (var x in phong.Phong) Phongs.Add(x);
            TieuDePhong = phong.TongKhopLoc == phong.TongPhong ? $"{phong.TongPhong} phòng" : $"{phong.TongKhopLoc}/{phong.TongPhong} phòng khớp bộ lọc";
            if (string.IsNullOrEmpty(RoomTypeCode)) RoomTypeCode = RoomTypes.FirstOrDefault()?.MaLoai ?? "";
            if (string.IsNullOrEmpty(HangLoatLoai)) HangLoatLoai = RoomTypes.FirstOrDefault()?.MaLoai ?? "";

            // Giữ lựa chọn sau khi tải lại; không đụng tới dữ liệu đang nhập.
            _dangHoanTac = true;
            if (maPhongChon is not null && !DangThemPhong) SelectedRoom = Phongs.FirstOrDefault(x => x.MaPhong == maPhongChon) ?? SelectedRoom;
            if (maLoaiChon is not null && !DangThemLoai) SelectedRoomType = RoomTypes.FirstOrDefault(x => x.MaLoai == maLoaiChon) ?? SelectedRoomType;
            _dangHoanTac = false;
            CapNhatTrangThaiPhong();
            OnPropertyChanged(nameof(CoTheXoaLoai)); OnPropertyChanged(nameof(LyDoKhongXoaLoai));
        }
        catch (Exception e) when (e is LoiYeuCauApi or HttpRequestException)
        {
            if (lan == _lanTai) DatThongBao(e is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {e.Message}", true);
        }
        finally
        {
            if (lan == _lanTai) DangTai = false;
        }
    }

    private static void Dem(ObservableCollection<DemDanhMucDto> dich, List<DemDanhMucDto> nguon) { dich.Clear(); foreach (var x in nguon) dich.Add(x); }

    partial void OnTuKhoaChanged(string value) => _ = LoadAsync();
    partial void OnLocTangChanged(string value) => _ = LoadAsync();
    partial void OnLocLoaiChanged(string value) => _ = LoadAsync();
    partial void OnLocTinhTrangChanged(string value) => _ = LoadAsync();
    partial void OnSapXepChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private void XoaBoLoc()
    {
        TuKhoa = ""; LocTang = ""; LocLoai = ""; LocTinhTrang = ""; SapXep = "";
    }

    [RelayCommand]
    private void LocNhanh(string kieu)
    {
        // kieu = "tang:2" | "loai:LP01" | "tt:OOO"
        var p = kieu.Split(':', 2);
        if (p.Length != 2) return;
        if (p[0] == "tang") LocTang = LocTang == p[1] ? "" : p[1];
        else if (p[0] == "loai") LocLoai = LocLoai == p[1] ? "" : p[1];
        else if (p[0] == "tt") LocTinhTrang = LocTinhTrang == p[1] ? "" : p[1];
    }

    // ------------------------------------------------------------------ phòng
    private void CapNhatTrangThaiPhong()
    {
        OnPropertyChanged(nameof(CoForm)); OnPropertyChanged(nameof(KhongCoForm)); OnPropertyChanged(nameof(CoThayDoiPhong));
        OnPropertyChanged(nameof(CoTheDoiTinhTrang)); OnPropertyChanged(nameof(CoTheXoaPhong)); OnPropertyChanged(nameof(LyDoKhongXoaPhong));
        OnPropertyChanged(nameof(LyDoKhongDoiLoaiPhong)); OnPropertyChanged(nameof(KhongDoiLoaiDuoc)); OnPropertyChanged(nameof(TieuDeFormPhong));
    }

    partial void OnRoomNameChanged(string value) => OnPropertyChanged(nameof(CoThayDoiPhong));
    partial void OnFloorTextChanged(string value) => OnPropertyChanged(nameof(CoThayDoiPhong));
    partial void OnRoomTypeCodeChanged(string value) => OnPropertyChanged(nameof(CoThayDoiPhong));
    partial void OnTinhTrangMoiChanged(string value) => OnPropertyChanged(nameof(CanLyDoTinhTrang));
    partial void OnDangThemPhongChanged(bool value) => CapNhatTrangThaiPhong();

    partial void OnSelectedRoomChanged(PhongDanhMucDto? oldValue, PhongDanhMucDto? newValue)
    {
        if (_dangHoanTac) { CapNhatTrangThaiPhong(); return; }
        if (CoThayDoiPhong && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Thông tin phòng đang sửa chưa được lưu. Chuyển sang phòng khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; SelectedRoom = oldValue; _dangHoanTac = false;
            return;
        }
        DangThemPhong = false;
        NapFormPhong(newValue);
    }

    private void NapFormPhong(PhongDanhMucDto? p)
    {
        if (p is null) { RoomName = ""; FloorText = "1"; TinhTrangChoPhep.Clear(); }
        else
        {
            RoomName = p.TenPhong; FloorText = p.Tang.ToString(); RoomTypeCode = p.MaLoai;
            TinhTrangChoPhep.Clear(); foreach (var t in p.TinhTrangChoPhep) TinhTrangChoPhep.Add(t);
        }
        TinhTrangMoi = ""; LyDoTinhTrang = "";
        _gocPhong = (RoomName, FloorText, RoomTypeCode);
        CapNhatTrangThaiPhong();
    }

    [RelayCommand]
    private void ThemPhong()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý danh mục phòng.", true); return; }
        if (CoThayDoiPhong && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm phòng mới?")) return;
        _dangHoanTac = true; SelectedRoom = null; _dangHoanTac = false;
        RoomName = ""; FloorText = LocTang != "" ? LocTang : "1"; RoomTypeCode = RoomTypes.FirstOrDefault()?.MaLoai ?? "";
        TinhTrangMoi = "VC"; LyDoTinhTrang = ""; TinhTrangChoPhep.Clear();
        _gocPhong = (RoomName, FloorText, RoomTypeCode);
        DangThemPhong = true;
        TabDangChon = 0;
        DatThongBao("", false);
    }

    [RelayCommand]
    private void HoanTacPhong()
    {
        if (DangThemPhong) { DangThemPhong = false; NapFormPhong(null); return; }
        (RoomName, FloorText, RoomTypeCode) = _gocPhong;
    }

    [RelayCommand]
    private async Task SaveRoomAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý danh mục phòng.", true); return; }
        if (!DangThemPhong && SelectedRoom is null) { DatThongBao("Chọn một phòng để sửa hoặc bấm \"Thêm phòng\".", true); return; }
        if (!int.TryParse(FloorText.Trim(), out var tang)) { DatThongBao("Tầng phải là số nguyên (0-100).", true); return; }
        if (string.IsNullOrWhiteSpace(RoomName)) { DatThongBao("Nhập tên phòng.", true); return; }
        if (string.IsNullOrWhiteSpace(RoomTypeCode)) { DatThongBao("Chọn loại phòng.", true); return; }
        if (!DangThemPhong && !CoThayDoiPhong) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }
        try
        {
            if (DangThemPhong)
            {
                var moi = await _api.CreateRoomAsync(new YeuCauPhong(RoomName.Trim(), tang, RoomTypeCode, string.IsNullOrEmpty(TinhTrangMoi) ? "VC" : TinhTrangMoi, LyDoTinhTrang.Trim()));
                DatThongBao($"Đã thêm phòng {moi.TenPhong}.", false);
                DangThemPhong = false;
                _dangHoanTac = true; SelectedRoom = null; _dangHoanTac = false;
                NapFormPhong(null);
                await LoadAsync();
                _dangHoanTac = true; SelectedRoom = Phongs.FirstOrDefault(x => x.MaPhong == moi.MaPhong); _dangHoanTac = false;
                NapFormPhong(SelectedRoom);
            }
            else
            {
                var ma = SelectedRoom!.MaPhong;
                // Không gửi tình trạng khi sửa thông tin: dữ liệu trên màn hình có thể đã cũ, đổi tình trạng đi qua nút riêng.
                await _api.UpdateRoomAsync(ma, new YeuCauPhong(RoomName.Trim(), tang, RoomTypeCode, null));
                DatThongBao($"Đã lưu phòng {RoomName.Trim()}.", false);
                await LoadAsync();
                _dangHoanTac = true; SelectedRoom = Phongs.FirstOrDefault(x => x.MaPhong == ma); _dangHoanTac = false;
                NapFormPhong(SelectedRoom);
            }
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa lưu được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DoiTinhTrangAsync()
    {
        var p = SelectedRoom;
        if (p is null || DangThemPhong) return;
        if (string.IsNullOrEmpty(TinhTrangMoi)) { DatThongBao("Chọn tình trạng muốn chuyển sang.", true); return; }
        if (TinhTrangMoi == "OOO" && string.IsNullOrWhiteSpace(LyDoTinhTrang)) { DatThongBao("Nhập lý do khóa/bảo trì phòng (bắt buộc).", true); return; }
        var ten = new PhongDanhMucDto { TinhTrang = TinhTrangMoi }.TinhTrangHienThi;
        var canhBao = TinhTrangMoi == "OOO" && p.SoDatPhongTuongLai > 0 ? $"\n\nCẢNH BÁO: phòng đang được giữ cho {p.SoDatPhongTuongLai} đặt phòng sắp tới - cần xử lý các đặt phòng đó." : "";
        if (!HopThoai.XacNhan("Đổi tình trạng phòng", $"Phòng {p.TenPhong}: {p.TinhTrangHienThi} → {ten}." + (string.IsNullOrWhiteSpace(LyDoTinhTrang) ? "" : $"\nLý do: {LyDoTinhTrang.Trim()}") + canhBao + "\n\nThao tác được ghi vào lịch sử phòng. Tiếp tục?")) return;
        try
        {
            await _api.CapNhatTinhTrangPhongAsync(p.MaPhong, TinhTrangMoi, string.IsNullOrWhiteSpace(LyDoTinhTrang) ? null : LyDoTinhTrang.Trim(), p.TinhTrang);
            DatThongBao($"Đã chuyển phòng {p.TenPhong} sang \"{ten}\".", false);
            var ma = p.MaPhong;
            await LoadAsync();
            _dangHoanTac = true; SelectedRoom = Phongs.FirstOrDefault(x => x.MaPhong == ma); _dangHoanTac = false;
            NapFormPhong(SelectedRoom);
        }
        catch (LoiYeuCauApi e)
        {
            DatThongBao("Chưa đổi được: " + e.Message, true);
            if (e.MaLoi == "XUNG_DOT_TINH_TRANG") await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteRoomAsync()
    {
        var p = SelectedRoom;
        if (p is null || DangThemPhong) { DatThongBao("Chọn một phòng để xóa.", true); return; }
        if (!p.CoTheXoa) { DatThongBao(p.LyDoKhongXoa ?? "Phòng này không xóa được.", true); return; }
        if (!HopThoai.XacNhan("Xóa phòng", $"Xóa phòng {p.TenPhong} ({p.MaPhong})? Phòng chưa phát sinh dữ liệu nên xóa được, nhưng thao tác không hoàn tác.")) return;
        try
        {
            await _api.DeleteRoomAsync(p.MaPhong);
            DatThongBao($"Đã xóa phòng {p.TenPhong}.", false);
            _dangHoanTac = true; SelectedRoom = null; _dangHoanTac = false;
            NapFormPhong(null);
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa xóa được: " + e.Message, true); await LoadAsync(); }
    }

    // ------------------------------------------------------------------ thêm hàng loạt
    partial void OnHangLoatTangChanged(string value) => CapNhatXemTruocHangLoat();
    partial void OnHangLoatTuChanged(string value) => CapNhatXemTruocHangLoat();
    partial void OnHangLoatDenChanged(string value) => CapNhatXemTruocHangLoat();
    partial void OnHangLoatTienToChanged(string value) => CapNhatXemTruocHangLoat();

    private List<string>? TinhTenHangLoat(out string loi)
    {
        loi = "";
        if (!int.TryParse(HangLoatTu.Trim(), out var tu) || !int.TryParse(HangLoatDen.Trim(), out var den)) { loi = "Nhập số bắt đầu và số kết thúc (VD 501 đến 510)."; return null; }
        if (den < tu) { loi = "Số kết thúc phải lớn hơn hoặc bằng số bắt đầu."; return null; }
        if (den - tu + 1 > 100) { loi = "Mỗi lần tạo tối đa 100 phòng."; return null; }
        var ten = Enumerable.Range(tu, den - tu + 1).Select(n => HangLoatTienTo.Trim() + n).ToList();
        if (ten.Any(x => x.Length > 9)) { loi = "Tên phòng tối đa 9 ký tự (kể cả tiền tố)."; return null; }
        return ten;
    }

    private void CapNhatXemTruocHangLoat()
    {
        var ten = TinhTenHangLoat(out var loi);
        HangLoatXemTruoc = ten is null ? loi : $"Sẽ tạo {ten.Count} phòng: {(ten.Count <= 6 ? string.Join(", ", ten) : $"{string.Join(", ", ten.Take(3))} ... {string.Join(", ", ten.TakeLast(2))}")}";
    }

    [RelayCommand]
    private async Task ThemHangLoatAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý danh mục phòng.", true); return; }
        var ten = TinhTenHangLoat(out var loi);
        if (ten is null) { DatThongBao(loi, true); return; }
        if (!int.TryParse(HangLoatTang.Trim(), out var tang)) { DatThongBao("Nhập tầng cho các phòng mới.", true); return; }
        if (string.IsNullOrEmpty(HangLoatLoai)) { DatThongBao("Chọn loại phòng cho các phòng mới.", true); return; }
        var tenLoai = RoomTypes.FirstOrDefault(x => x.MaLoai == HangLoatLoai)?.TenLoai ?? HangLoatLoai;
        if (!HopThoai.XacNhan("Thêm phòng hàng loạt", $"Tạo {ten.Count} phòng ({ten.First()} - {ten.Last()}) tại tầng {tang}, loại {tenLoai}, tình trạng \"Trống, đã dọn\".\nNếu một phòng đã tồn tại thì KHÔNG phòng nào được tạo. Tiếp tục?")) return;
        try
        {
            var kq = await _api.TaoPhongHangLoatAsync(new YeuCauPhongHangLoat(ten, tang, HangLoatLoai, "VC"));
            DatThongBao($"Đã tạo {kq.SoPhongDaTao} phòng.", false);
            HangLoatTu = ""; HangLoatDen = "";
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa tạo được: " + e.Message, true); }
    }

    // ------------------------------------------------------------------ loại phòng
    private void CapNhatTrangThaiLoai()
    {
        OnPropertyChanged(nameof(CoFormLoai)); OnPropertyChanged(nameof(KhongCoFormLoai)); OnPropertyChanged(nameof(CoThayDoiLoai));
        OnPropertyChanged(nameof(CoTheXoaLoai)); OnPropertyChanged(nameof(LyDoKhongXoaLoai)); OnPropertyChanged(nameof(TieuDeFormLoai));
    }

    partial void OnTypeNameChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnCapacityTextChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnPriceTextChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnSurchargeTextChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnDienTichTextChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnLoaiGiuongChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnTienNghiChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnMoTaChanged(string value) => OnPropertyChanged(nameof(CoThayDoiLoai));
    partial void OnDangThemLoaiChanged(bool value) => CapNhatTrangThaiLoai();

    partial void OnSelectedRoomTypeChanged(LoaiPhongDanhMucDto? oldValue, LoaiPhongDanhMucDto? newValue)
    {
        if (_dangHoanTac) { CapNhatTrangThaiLoai(); return; }
        if (CoThayDoiLoai && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Thông tin loại phòng đang sửa chưa được lưu. Chuyển sang loại khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; SelectedRoomType = oldValue; _dangHoanTac = false;
            return;
        }
        DangThemLoai = false;
        NapFormLoai(newValue);
        LoaiChoAnh = newValue;
        _ = NapAnhAsync();
    }

    private void NapFormLoai(LoaiPhongDanhMucDto? l)
    {
        if (l is null) { TypeName = ""; CapacityText = "2"; PriceText = ""; SurchargeText = KLCN060.Desktop.Controls.MoneyInput.Dinh(200000); DienTichText = ""; LoaiGiuong = ""; TienNghi = ""; MoTa = ""; }
        else
        {
            TypeName = l.TenLoai; CapacityText = l.SoNguoiTieuChuan.ToString(); PriceText = KLCN060.Desktop.Controls.MoneyInput.Dinh(l.DonGia);
            SurchargeText = KLCN060.Desktop.Controls.MoneyInput.Dinh(l.PhuThu); DienTichText = l.DienTich?.ToString("0.#", CultureInfo.InvariantCulture) ?? "";
            LoaiGiuong = l.LoaiGiuong ?? ""; TienNghi = l.TienNghi ?? ""; MoTa = l.MoTa ?? "";
        }
        _gocLoai = (TypeName, CapacityText, PriceText, SurchargeText, DienTichText, LoaiGiuong, TienNghi, MoTa);
        CapNhatTrangThaiLoai();
    }

    [RelayCommand]
    private void ThemLoai()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý danh mục phòng.", true); return; }
        if (CoThayDoiLoai && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm loại phòng mới?")) return;
        _dangHoanTac = true; SelectedRoomType = null; _dangHoanTac = false;
        NapFormLoai(null);
        DangThemLoai = true;
        TabDangChon = 1;
        DatThongBao("", false);
    }

    [RelayCommand]
    private void HoanTacLoai()
    {
        if (DangThemLoai) { DangThemLoai = false; NapFormLoai(null); return; }
        NapFormLoai(SelectedRoomType);
    }

    private static decimal? DocSo(string s) => decimal.TryParse(s.Replace(".", "").Replace(",", ".").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    [RelayCommand]
    private async Task SaveRoomTypeAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý danh mục phòng.", true); return; }
        if (!DangThemLoai && SelectedRoomType is null) { DatThongBao("Chọn một loại phòng để sửa hoặc bấm \"Thêm loại phòng\".", true); return; }
        if (string.IsNullOrWhiteSpace(TypeName) || TypeName.Trim().Length < 2) { DatThongBao("Tên loại phòng bắt buộc (tối thiểu 2 ký tự).", true); return; }
        if (!int.TryParse(CapacityText.Trim(), out var sucChua) || sucChua is < 1 or > 20) { DatThongBao("Sức chứa phải từ 1 đến 20 người.", true); return; }
        if (DocSo(PriceText) is not { } gia || gia <= 0) { DatThongBao("Nhập đơn giá hợp lệ (lớn hơn 0).", true); return; }
        if (DocSo(SurchargeText) is not { } phuThu || phuThu < 0) { DatThongBao("Phụ thu phải là số không âm.", true); return; }
        decimal? dienTich = null;
        if (!string.IsNullOrWhiteSpace(DienTichText))
        {
            dienTich = DocSo(DienTichText);
            if (dienTich is null or < 5 or > 1000) { DatThongBao("Diện tích phải từ 5 đến 1000 m² (hoặc để trống).", true); return; }
        }
        if (!DangThemLoai && !CoThayDoiLoai) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }

        var cu = SelectedRoomType;
        if (!DangThemLoai && cu is not null && gia != cu.DonGia)
        {
            if (!CoTheDatGia) { DatThongBao("Chỉ người có quyền quản lý giá được đổi đơn giá loại phòng.", true); return; }
            if (!HopThoai.XacNhan("Đổi đơn giá loại phòng",
                    $"Đơn giá {cu.TenLoai}: {cu.DonGia:N0}đ → {gia:N0}đ.\nÁp dụng cho các đặt phòng MỚI; {cu.SoDatPhongTuongLai} đặt phòng sắp tới và hóa đơn cũ giữ nguyên giá đã chốt. Thay đổi được ghi nhật ký. Tiếp tục?")) return;
        }
        var req = new YeuCauLoaiPhong(TypeName.Trim(), sucChua, gia, phuThu, dienTich,
            string.IsNullOrWhiteSpace(LoaiGiuong) ? null : LoaiGiuong.Trim(), string.IsNullOrWhiteSpace(TienNghi) ? null : TienNghi.Trim(), string.IsNullOrWhiteSpace(MoTa) ? null : MoTa.Trim());
        try
        {
            string ma;
            if (DangThemLoai) { var moi = await _api.CreateRoomTypeAsync(req); ma = moi.MaLoai; DatThongBao($"Đã thêm loại phòng {moi.TenLoai} (mã {ma}).", false); }
            else { ma = SelectedRoomType!.MaLoai; await _api.UpdateRoomTypeAsync(ma, req); DatThongBao($"Đã lưu loại phòng {TypeName.Trim()}.", false); }
            DangThemLoai = false;
            _dangHoanTac = true; SelectedRoomType = null; _dangHoanTac = false;
            await LoadAsync();
            _dangHoanTac = true; SelectedRoomType = RoomTypes.FirstOrDefault(x => x.MaLoai == ma); _dangHoanTac = false;
            NapFormLoai(SelectedRoomType);
            LoaiChoAnh = SelectedRoomType;
            _ = NapAnhAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa lưu được: " + e.Message, true); }
    }

    [RelayCommand]
    private async Task DeleteRoomTypeAsync()
    {
        var l = SelectedRoomType;
        if (l is null || DangThemLoai) { DatThongBao("Chọn một loại phòng để xóa.", true); return; }
        if (!l.CoTheXoa) { DatThongBao(l.LyDoKhongXoa ?? "Loại phòng này không xóa được.", true); return; }
        if (!HopThoai.XacNhan("Xóa loại phòng", $"Xóa loại phòng {l.TenLoai} ({l.MaLoai})? Loại chưa có phòng nào nên xóa được, nhưng thao tác không hoàn tác.")) return;
        try
        {
            await _api.DeleteRoomTypeAsync(l.MaLoai);
            DatThongBao($"Đã xóa loại phòng {l.TenLoai}.", false);
            _dangHoanTac = true; SelectedRoomType = null; _dangHoanTac = false;
            NapFormLoai(null);
            await LoadAsync();
        }
        catch (LoiYeuCauApi e) { DatThongBao("Chưa xóa được: " + e.Message, true); await LoadAsync(); }
    }

    // ------------------------------------------------------------------ ảnh
    private async Task NapAnhAsync()
    {
        try
        {
            var loai = LoaiChoAnh;
            AnhCuaLoaiPhong.Clear();
            if (loai is not null)
            {
                AnhCuaLoaiPhong.Add(new AnhPhongSlotViewModel(_api, loai.MaLoai, "Ảnh đại diện", (nd, l) => { ThongBaoAnh = nd; ThongBaoAnhLaLoi = l; }));
                for (var i = 1; i <= 3; i++)
                    AnhCuaLoaiPhong.Add(new AnhPhongSlotViewModel(_api, $"{loai.MaLoai}-{i}", $"Ảnh chi tiết {i}", (nd, l) => { ThongBaoAnh = nd; ThongBaoAnhLaLoi = l; }));
            }
            HuongDanAnh = loai is null
                ? "Chọn một loại phòng ở tab \"Loại phòng\" để xem và thay ảnh của loại phòng đó."
                : $"Loại phòng đang chọn: {loai.TenLoai} ({loai.MaLoai}). Ảnh đại diện hiện ở trang chủ/kết quả tìm kiếm; 3 ảnh chi tiết hiện ở trang chi tiết phòng.";

            var tatCa = (await _api.GetAnhPhongAsync()).ToDictionary(x => x.Slot, StringComparer.OrdinalIgnoreCase);
            var cacO = AnhCuaLoaiPhong.Append(AnhBanner).ToList();
            await Task.WhenAll(cacO.Select(o => o.ApDungAsync(tatCa.GetValueOrDefault(o.Slot))));
        }
        catch (LoiYeuCauApi e) { ThongBaoAnh = e.Message; ThongBaoAnhLaLoi = true; }
        catch (HttpRequestException e) { ThongBaoAnh = $"Không tải được danh sách ảnh: {e.Message}"; ThongBaoAnhLaLoi = true; }
    }
}
