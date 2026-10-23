using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;
public partial class DatPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<KhachHangDto> DanhSachKhach { get; }=[];
    public ObservableCollection<LoaiPhongDto> DanhSachLoaiPhong { get; }=[];
    public ObservableCollection<DongDatPhong> DanhSachPhongDat { get; }=[];
    [ObservableProperty] private string tuKhoaKhach=""; [ObservableProperty] private KhachHangDto? khachDaChon;
    [ObservableProperty] private LoaiPhongDto? loaiPhongDaChon; [ObservableProperty] private int soLuong=1;
    [ObservableProperty] private DateTime ngayNhan=DateTime.Today; [ObservableProperty] private DateTime ngayTra=DateTime.Today.AddDays(1);
    [ObservableProperty] private string thongBao="";
    // Form nhanh tạo khách vãng lai mới ngay tại D3 khi tìm SĐT không ra kết quả (Mục 5B GD4 của Toàn:
    // "nút 'Khách mới' mở form nhập nhanh Họ tên/SĐT... gọi POST /api/v1/guests").
    [ObservableProperty] private string hoTenKhachMoi=""; [ObservableProperty] private string soDTKhachMoi="";
    // Mã phiếu đặt vừa tạo thành công, dùng để "Xác nhận đã thu cọc" - lưu trực tiếp thay vì suy ra
    // từ nội dung ThongBao (chuỗi hiển thị có thể đổi định dạng bất cứ lúc nào và làm hỏng thao tác này).
    [ObservableProperty] private string? maPhieuDatVuaTao;
    // Chặn bấm đúp trong lúc đang gọi API tạo đặt phòng - tránh giữ trùng phòng do gửi request 2 lần.
    [ObservableProperty] private bool dangTaoDatPhong;
    [ObservableProperty] private string goiYKhach = "Nhập họ tên hoặc số điện thoại ở ô phía trên rồi nhấn Enter để tìm khách.";
    public bool ChuaChonPhong => DanhSachPhongDat.Count == 0;
    public string TomTatLuuTru
    {
        get
        {
            var den = DateOnly.FromDateTime(NgayTra); var tu = DateOnly.FromDateTime(NgayNhan);
            var soDem = den.DayNumber - tu.DayNumber;
            var khach = KhachDaChon is null ? "chưa chọn khách" : $"khách {KhachDaChon.HoTen}";
            return soDem > 0 ? $"{soDem} đêm ({tu:dd/MM/yyyy} → {den:dd/MM/yyyy}) · {khach}" : $"Ngày trả phải sau ngày nhận · {khach}";
        }
    }
    partial void OnNgayNhanChanged(DateTime value) { OnPropertyChanged(nameof(TomTatLuuTru)); CapNhatBaoGia(); }
    partial void OnNgayTraChanged(DateTime value) { OnPropertyChanged(nameof(TomTatLuuTru)); CapNhatBaoGia(); }
    partial void OnLoaiPhongDaChonChanged(LoaiPhongDto? value) => CapNhatGoiYPhongTrong();

    // ---- Khuyến mãi + xem trước tiền / phòng trống (báo giá chạy ở máy chủ, đúng quy tắc khi tạo thật) ----
    /// <summary>Khuyến mãi đang áp dụng, tích chọn được nhiều cái (tối đa <see cref="SoKhuyenMaiToiDa"/>); không tích cái nào = không dùng khuyến mãi.</summary>
    public ObservableCollection<KhuyenMaiLuaChon> DanhSachKhuyenMai { get; } = [];
    public const int SoKhuyenMaiToiDa = 3;
    public bool CoKhuyenMai => DanhSachKhuyenMai.Count > 0;
    public bool KhongCoKhuyenMai => DanhSachKhuyenMai.Count == 0;
    private List<string> MaKhuyenMaiDaChon() => DanhSachKhuyenMai.Where(x => x.DuocChon && x.Ma is not null).Select(x => x.Ma!).ToList();

    private void KhuyenMaiThayDoi(KhuyenMaiLuaChon km)
    {
        if (km.DuocChon && DanhSachKhuyenMai.Count(x => x.DuocChon) > SoKhuyenMaiToiDa)
        {
            km.DuocChon = false;   // sự kiện thay đổi chạy lại và cập nhật báo giá
            ThongBao = $"Mỗi đặt phòng áp dụng tối đa {SoKhuyenMaiToiDa} khuyến mãi.";
            return;
        }
        CapNhatBaoGia();
    }
    [ObservableProperty] private string baoGiaHienThi = "Chọn ngày ở và loại phòng để xem số phòng còn trống, tiền phòng và tiền cọc.";
    [ObservableProperty] private string goiYPhongTrong = "";
    private Dictionary<string, LoaiPhongConTrongDto> _phongTrong = new();
    private int _lanBaoGia, _lanPhongTrong;

    private bool KhoangNgayHopLe(out DateOnly tu, out DateOnly den)
    {
        tu = DateOnly.FromDateTime(NgayNhan); den = DateOnly.FromDateTime(NgayTra);
        return tu >= DateOnly.FromDateTime(DateTime.Today) && den > tu && den.DayNumber - tu.DayNumber <= 90;
    }

    private YeuCauDatPhong TaoYeuCau(DateOnly tu, DateOnly den)
        => new(DanhSachPhongDat.Sum(x => x.SoLuong) > 1 ? "DOAN" : "CA_NHAN", KhachDaChon?.MaKhach, tu, den,
            DanhSachPhongDat.Select(x => new DongYeuCauDatPhong(x.MaLoai, x.SoLuong)).ToList(), null, MaKhuyenMaiDaChon());

    private async void CapNhatBaoGia()
    {
        var lan = ++_lanBaoGia;
        CapNhatGoiYPhongTrong();
        if (DanhSachPhongDat.Count == 0 || !KhoangNgayHopLe(out var tu, out var den))
        {
            BaoGiaHienThi = DanhSachPhongDat.Count == 0 ? "Chọn ngày ở và loại phòng để xem số phòng còn trống, tiền phòng và tiền cọc." : "Chọn khoảng ngày hợp lệ (ngày trả sau ngày nhận, tối đa 90 đêm) để xem tiền phòng.";
            return;
        }
        try
        {
            await Task.Delay(250);
            if (lan != _lanBaoGia) return;
            var bg = await _api.BaoGiaDatPhongAsync(TaoYeuCau(tu, den));
            if (lan != _lanBaoGia) return;
            BaoGiaHienThi = bg.TomTat;
        }
        catch (LoiYeuCauApi e) { if (lan == _lanBaoGia) BaoGiaHienThi = e.Message; }
    }

    private async void CapNhatGoiYPhongTrong()
    {
        var lan = ++_lanPhongTrong;
        if (!KhoangNgayHopLe(out var tu, out var den)) { GoiYPhongTrong = "Chọn khoảng ngày hợp lệ để xem số phòng còn trống."; return; }
        try
        {
            var ds = await _api.GetLoaiPhongConTrongAsync(tu, den);
            if (lan != _lanPhongTrong) return;
            _phongTrong = ds.ToDictionary(x => x.MaLoai);
            GoiYPhongTrong = LoaiPhongDaChon is not null && _phongTrong.TryGetValue(LoaiPhongDaChon.MaLoai, out var x)
                ? $"Còn {x.SoPhongTrong} phòng trống · {x.DonGia:N0}đ/đêm · {x.TongTienDuKien:N0}đ cho {den.DayNumber - tu.DayNumber} đêm"
                : "Chọn loại phòng để xem số phòng còn trống.";
        }
        catch (LoiYeuCauApi e) { if (lan == _lanPhongTrong) GoiYPhongTrong = e.Message; }
    }
    partial void OnKhachDaChonChanged(KhachHangDto? value)
    {
        OnPropertyChanged(nameof(TomTatLuuTru));
        MaPhieuDatVuaTao = null;   // đổi khách thì nút "Xác nhận đã thu cọc" không còn áp cho phiếu vừa tạo của khách trước
    }
    public DatPhongViewModel(ApiClient api){_api=api;DanhSachPhongDat.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(ChuaChonPhong)); CapNhatBaoGia(); };_ = TaiLoaiPhongAsync();_ = TaiKhuyenMaiAsync();CapNhatGoiYPhongTrong();}
    /// <summary>Mở sẵn khách (từ Hồ sơ khách hàng): tìm đúng mã khách và chọn, lễ tân chỉ còn chọn ngày/loại phòng.</summary>
    public async Task ChonKhachAsync(string maKhach)
    {
        try
        {
            TuKhoaKhach = maKhach;
            DanhSachKhach.Clear();
            foreach (var x in await _api.TimKiemKhachHangAsync(maKhach)) DanhSachKhach.Add(x);
            KhachDaChon = DanhSachKhach.FirstOrDefault(x => x.MaKhach == maKhach);
            if (KhachDaChon is null) ThongBao = $"Không tìm thấy khách {maKhach}.";
        }
        catch (LoiYeuCauApi e) { ThongBao = e.Message; }
    }
    [RelayCommand]
    private async Task TimKhachAsync()
    {
        try
        {
            var ds = await _api.TimKiemKhachHangAsync(TuKhoaKhach.Trim());
            DanhSachKhach.Clear();
            foreach (var x in ds) DanhSachKhach.Add(x);
            GoiYKhach = ds.Count == 0 ? (string.IsNullOrWhiteSpace(TuKhoaKhach) ? "Chưa có khách nào trong hệ thống." : $"Không tìm thấy khách khớp \"{TuKhoaKhach.Trim()}\". Hãy tạo hồ sơ khách mới ở khung bên dưới.") : "";
            if (ds.Count == 1) KhachDaChon = ds[0];
            else if (ds.Count > 1) ThongBao = $"Tìm thấy {ds.Count} khách - bấm chọn đúng khách trong danh sách.";
        }
        catch (LoiYeuCauApi e) { ThongBao = e.Message; }
    }
    [RelayCommand] private void BoPhong(DongDatPhong? dong) { if (dong is not null) DanhSachPhongDat.Remove(dong); }
    [RelayCommand]
    private void ThemPhong()
    {
        if (LoaiPhongDaChon is null) { ThongBao = "Chọn loại phòng trước khi bấm Thêm phòng."; return; }
        if (SoLuong < 1) { ThongBao = "Số phòng phải từ 1 trở lên."; return; }
        var soLuongThem = SoLuong;
        // Chặn sớm khi vượt số phòng còn trống (máy chủ vẫn kiểm lại khi tạo thật).
        if (_phongTrong.TryGetValue(LoaiPhongDaChon.MaLoai, out var conTrong))
        {
            var daChon = DanhSachPhongDat.Where(x => x.MaLoai == LoaiPhongDaChon.MaLoai).Sum(x => x.SoLuong);
            if (daChon + soLuongThem > conTrong.SoPhongTrong)
            {
                ThongBao = $"Loại {LoaiPhongDaChon.TenLoai} chỉ còn {conTrong.SoPhongTrong} phòng trống trong khoảng ngày này (đã chọn {daChon}, thêm {soLuongThem}).";
                return;
            }
        }
        // Gộp vào dòng đã có cùng loại phòng thay vì tạo dòng trùng - tránh gửi 2 dòng cùng maLoai lên
        // API (POST /bookings gộp theo maLoai nhưng UI hiển thị tách rời sẽ gây hiểu nhầm số lượng thật).
        var daCo = DanhSachPhongDat.FirstOrDefault(x => x.MaLoai == LoaiPhongDaChon.MaLoai);
        if (daCo is not null)
        {
            var idx = DanhSachPhongDat.IndexOf(daCo);
            DanhSachPhongDat[idx] = daCo with { SoLuong = daCo.SoLuong + soLuongThem };
        }
        else
        {
            DanhSachPhongDat.Add(new DongDatPhong(LoaiPhongDaChon.MaLoai, LoaiPhongDaChon.TenLoai, soLuongThem));
        }
    }
    [RelayCommand]
    private async Task TaoKhachMoiAsync()
    {
        if (string.IsNullOrWhiteSpace(HoTenKhachMoi) || string.IsNullOrWhiteSpace(SoDTKhachMoi))
        {
            ThongBao = "Vui lòng nhập họ tên và số điện thoại khách mới.";
            return;
        }
        try
        {
            var khach = await _api.TaoKhachHangAsync(new YeuCauKhachHang(HoTenKhachMoi.Trim(), SoDTKhachMoi.Trim(), null, null, null));
            DanhSachKhach.Clear();
            DanhSachKhach.Add(khach);
            GoiYKhach = "";
            KhachDaChon = khach;
            HoTenKhachMoi = "";
            SoDTKhachMoi = "";
            ThongBao = $"Đã tạo khách mới {khach.MaKhach} và chọn cho đặt phòng.";
        }
        catch (LoiYeuCauApi e) { ThongBao = e.Message; }
    }
    [RelayCommand]
    private async Task TaoDatPhongAsync()
    {
        if (DangTaoDatPhong) return;
        if (KhachDaChon is null || DanhSachPhongDat.Count == 0)
        {
            ThongBao = "Vui lòng chọn khách hàng và ít nhất một loại phòng.";
            return;
        }
        var homNay = DateOnly.FromDateTime(DateTime.Today);
        var tu = DateOnly.FromDateTime(NgayNhan); var den = DateOnly.FromDateTime(NgayTra);
        if (tu < homNay) { ThongBao = "Ngày nhận không được ở quá khứ."; return; }
        if (den <= tu) { ThongBao = "Ngày trả phải sau ngày nhận (tối thiểu 1 đêm)."; return; }
        BaoGiaDatPhongDto bg;
        try { bg = await _api.BaoGiaDatPhongAsync(TaoYeuCau(tu, den)); }
        catch (LoiYeuCauApi e) { ThongBao = e.Message; return; }
        if (!bg.DuPhong) { ThongBao = "Không đủ phòng trống: " + string.Join("; ", bg.Dong.Where(x => !x.DuPhong).Select(x => x.HienThi)); return; }
        if (bg.KhuyenMais.FirstOrDefault(x => !x.DuDieuKien) is { } km) { ThongBao = $"Khuyến mãi {km.TenKM} không áp dụng được: {km.LyDoKhongDat} Hãy bỏ chọn khuyến mãi hoặc đổi thông tin đặt phòng."; return; }
        var tomTat = $"Khách: {KhachDaChon.HoTen} · {KhachDaChon.SoDT}\nỞ: {tu:dd/MM/yyyy} → {den:dd/MM/yyyy} ({den.DayNumber - tu.DayNumber} đêm)\nPhòng: "
                     + string.Join(", ", DanhSachPhongDat.Select(x => $"{x.SoLuong} × {x.TenLoai}")) + "\n\n" + bg.TomTat + "\n\nTạo phiếu đặt phòng? Thao tác được ghi nhật ký (ai, lúc nào).";
        if (!HopThoai.XacNhan("Xác nhận đặt phòng", tomTat)) return;
        DangTaoDatPhong = true;
        try
        {
            // Đặt từ 2 phòng trở lên (dù cùng hay khác loại) là đặt đoàn - chỉ đúng 1 phòng mới là cá nhân.
            var tongSoPhong = DanhSachPhongDat.Sum(x => x.SoLuong);
            var loaiDatPhong = tongSoPhong > 1 ? "DOAN" : "CA_NHAN";
            var r = new YeuCauDatPhong(loaiDatPhong, KhachDaChon.MaKhach, DateOnly.FromDateTime(NgayNhan), DateOnly.FromDateTime(NgayTra),
                DanhSachPhongDat.Select(x => new DongYeuCauDatPhong(x.MaLoai, x.SoLuong)).ToList(), null, MaKhuyenMaiDaChon());
            var kq = await _api.TaoPhieuDatAsync(r);
            MaPhieuDatVuaTao = kq.MaPhieuDat;
            ThongBao = $"Đã tạo {kq.MaPhieuDat}. Tiền cọc: {kq.TienCoc:N0}đ. Thu cọc xong hãy bấm \"Xác nhận đã thu cọc\".";
            DanhSachPhongDat.Clear();   // tránh bấm Tạo lần nữa làm sinh phiếu trùng; muốn đặt thêm thì chọn phòng lại
        }
        catch (LoiYeuCauApi e) { ThongBao = e.Message; }
        finally { DangTaoDatPhong = false; }
    }
    [RelayCommand] private async Task XacNhanCocAsync(){if(string.IsNullOrEmpty(MaPhieuDatVuaTao)){ThongBao="Hãy tạo phiếu đặt trước.";return;}try{await _api.XacNhanDatCocAsync(MaPhieuDatVuaTao);ThongBao="Đã xác nhận thu cọc.";}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
    private async Task TaiKhuyenMaiAsync()
    {
        try
        {
            foreach (var k in await _api.GetKhuyenMaiDangApDungAsync())
            {
                var km = new KhuyenMaiLuaChon(k.MaKM, $"{k.TenKM} ({(k.PhanTramKM is { } pt ? $"-{pt:0.#}%" : k.GiaTri is { } gt ? $"-{gt:N0}đ" : "")})");
                km.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(KhuyenMaiLuaChon.DuocChon)) KhuyenMaiThayDoi(km); };
                DanhSachKhuyenMai.Add(km);
            }
            OnPropertyChanged(nameof(CoKhuyenMai)); OnPropertyChanged(nameof(KhongCoKhuyenMai));
        }
        catch (LoiYeuCauApi) { /* không tải được khuyến mãi thì vẫn đặt phòng không khuyến mãi */ }
    }
    private async Task TaiLoaiPhongAsync(){try{foreach(var x in await _api.GetRoomTypesAsync())DanhSachLoaiPhong.Add(x);}catch(LoiYeuCauApi e){ThongBao=e.Message;}}
}
public sealed partial class KhuyenMaiLuaChon : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public KhuyenMaiLuaChon(string? ma, string ten) { Ma = ma; Ten = ten; }
    public string? Ma { get; }
    public string Ten { get; }
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool duocChon;
}
public sealed record DongDatPhong(string MaLoai,string TenLoai,int SoLuong);
