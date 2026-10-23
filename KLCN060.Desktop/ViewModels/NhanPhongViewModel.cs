using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
namespace KLCN060.Desktop.ViewModels;

// 1 dòng = 1 phòng thuộc phiếu đặt, cho phép số người RIÊNG từng phòng và tick chọn nhận một phần
// (đoàn khách đến rải rác). Phòng chưa nhận được (chưa dọn, có khách, bảo trì...) bị khóa và nêu rõ lý do.
public partial class DongNhanPhong : ObservableObject
{
    public string MaPhong { get; }
    public string MaLoai { get; }
    public string TenLoai { get; }
    public string TinhTrang { get; }
    public bool NhanDuoc { get; }
    public bool DaNhan { get; }
    public string? LyDo { get; }
    public int SucChuaTieuChuan { get; }
    public int SucChuaToiDa { get; }
    public decimal DonGia { get; }
    [ObservableProperty] private bool daChon;
    [ObservableProperty] private int soNguoi;

    public DongNhanPhong(PhongNhanInfoDto p)
    {
        MaPhong = p.MaPhong; MaLoai = p.MaLoai; TenLoai = p.TenLoai; TinhTrang = p.TinhTrang; NhanDuoc = p.NhanDuoc; DaNhan = p.DaNhan;
        LyDo = p.LyDoKhongNhanDuoc; SucChuaTieuChuan = p.SucChuaTieuChuan; SucChuaToiDa = p.SucChuaToiDa; DonGia = p.DonGiaApDung;
        daChon = p.NhanDuoc; soNguoi = p.SucChuaTieuChuan;
    }

    public string TinhTrangHienThi => TinhTrang switch
    {
        "VC" => "Đã dọn", "VI" => "Đã kiểm tra", "VD" => "Chưa dọn", "OC" => "Có khách", "OD" => "Có khách - cần dọn", "OOO" => "Bảo trì", _ => TinhTrang
    };
    public string SucChuaHienThi => $"{SucChuaTieuChuan} (tối đa {SucChuaToiDa})";
    public string DonGiaHienThi => $"{DonGia:N0}đ";
    public string TrangThaiNhan => DaNhan ? "Đã nhận" : NhanDuoc ? "Sẵn sàng nhận" : $"Chưa nhận được: {LyDo}";
}

public partial class NhanPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<PhieuDatTomTatDto> DanhSachPhieuDat { get; }=[];
    public ObservableCollection<PhongDto> PhongTrong { get; }=[];
    public ObservableCollection<KhachHangDto> DanhSachKhach { get; }=[];
    public ObservableCollection<DongNhanPhong> PhongCuaPhieuDat { get; }=[];
    [ObservableProperty] private string tuKhoa=""; [ObservableProperty] private PhieuDatTomTatDto? phieuDatDaChon;
    /// <summary>Mặc định chỉ liệt kê phiếu đã xác nhận cọc (nhận được); bỏ tick để tra cả phiếu chờ cọc/đã hủy và biết vì sao chưa nhận được.</summary>
    [ObservableProperty] private bool chiPhieuDaXacNhan = true;
    [ObservableProperty] private string tuKhoaKhach="";
    [ObservableProperty] private KhachHangDto? khachDaChon; [ObservableProperty] private PhongDto? phongVangLaiDaChon;
    // Dùng riêng cho khách vãng lai (luôn đúng 1 phòng/1 khách nên không cần tách theo dòng như bên trái).
    [ObservableProperty] private string cccdVangLai=""; [ObservableProperty] private int soNguoiVangLai;
    [ObservableProperty] private string thongBao="";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    // Chặn bấm đúp trong lúc đang gọi API nhận phòng.
    [ObservableProperty] private bool dangNhanPhong;
    [ObservableProperty] private bool dangTai;
    /// <summary>Thông tin tổng hợp của phiếu đang chọn (khách, ngày, tiền, từng phòng nhận được hay không).</summary>
    [ObservableProperty] private ThongTinNhanPhongDto? thongTin;
    /// <summary>Số giấy tờ nhập khi hồ sơ khách chưa có (áp cho khách của phiếu, một lần cho cả đoàn).</summary>
    [ObservableProperty] private string cccdKhachPhieu = "";
    [ObservableProperty] private string lyDoNhanSom = "";
    /// <summary>Số đêm ở của khách vãng lai (bắt buộc từ 1): xác định ngày trả dự kiến để biết phòng nào sắp trả và kiểm tra khi gia hạn.</summary>
    [ObservableProperty] private int soDemVangLai = 1;
    /// <summary>PHIEU (khách có phiếu đặt) hoặc VANG_LAI (khách không đặt trước): mỗi chế độ hiện đúng một khung, toàn chiều rộng để không bị cắt chữ.</summary>
    [ObservableProperty] private string cheDo = "PHIEU";
    public bool LaCheDoPhieu => CheDo == "PHIEU";
    public bool LaCheDoVangLai => CheDo == "VANG_LAI";
    public bool ChuaChonPhieu => PhieuDatDaChon is null;
    /// <summary>Khách lưu trú bắt buộc có ngày trả cụ thể: hiển thị ngày + giờ trả phòng chuẩn sẽ ghi vào lượt ở.</summary>
    public string TraPhongDuKienHienThi => SoDemVangLai >= 1
        ? $"Trả phòng dự kiến: {DateTime.Today.AddDays(SoDemVangLai):dd/MM/yyyy} trước {GioTraPhongChuan} ({SoDemVangLai} đêm)"
        : "Nhập số đêm ở (từ 1) để có ngày trả phòng dự kiến.";
    public const string GioTraPhongChuan = "12:00";
    partial void OnCheDoChanged(string value) { OnPropertyChanged(nameof(LaCheDoPhieu)); OnPropertyChanged(nameof(LaCheDoVangLai)); }
    partial void OnSoDemVangLaiChanged(int value) => OnPropertyChanged(nameof(TraPhongDuKienHienThi));
    [RelayCommand] private void ChonCheDo(string? cheDo) { if (cheDo is "PHIEU" or "VANG_LAI") CheDo = cheDo; }
    // Khách vãng lai mới (tạo ngay tại màn hình, có kiểm tra trùng).
    [ObservableProperty] private string hoTenMoi = ""; [ObservableProperty] private string sdtMoi = ""; [ObservableProperty] private string cccdMoi = "";
    // Kết quả lần nhận phòng gần nhất (mã phiếu nhận, giờ, người thực hiện, phòng, trạng thái).
    [ObservableProperty] private string ketQuaNhan = "";

    /// <summary>Phiếu có ngày đón sau hôm nay: chỉ nhận được khi chọn "nhận sớm" (ghi lý do, phòng phải trống từ hôm nay, ngày đón dời về hôm nay).</summary>
    public bool LaNhanSom => ThongTin?.ThoiDiem == "SOM";
    public bool CoThongTin => ThongTin is not null;
    public bool CanNhapCccd => ThongTin?.ThieuCccd == true;
    public bool CoKetQua => !string.IsNullOrEmpty(KetQuaNhan);
    public bool KhachVangLaiThieuCccd => KhachDaChon is not null && string.IsNullOrWhiteSpace(KhachDaChon.CCCD);
    public string KhachVangLaiGhiChu => KhachDaChon is null ? "" :
        $"{KhachDaChon.HoTen} · {KhachDaChon.SoDT} · " + (KhachVangLaiThieuCccd ? "chưa có giấy tờ - phải nhập bên dưới" : $"giấy tờ đã có: {KhachDaChon.CCCD}") +
        (KhachDaChon.DangO ? " · ĐANG có lượt ở khác (nhận thêm phòng cho cùng khách)" : "");
    public string TrangThaiTimKhach => DanhSachKhach.Count == 0 && DaTimKhach ? "Không tìm thấy khách - tạo khách mới bên dưới." : "";
    private bool DaTimKhach { get; set; }

    public NhanPhongViewModel(ApiClient api){_api=api;_ = KhoiTaoAsync();}

    private async Task KhoiTaoAsync()
    {
        await TaiPhongTrongAsync();
        await TimPhieuDatAsync();
    }

    /// <summary>Mở sẵn phiếu đặt (từ Tổng quan điều hành): tìm đúng mã phiếu, chọn phiếu và tải danh sách phòng để nhận.</summary>
    public async Task MoPhieuAsync(string maPhieuDat)
    {
        CheDo = "PHIEU";
        ChiPhieuDaXacNhan = false;
        TuKhoa = maPhieuDat;
        await TimPhieuDatAsync();
        var phieu = DanhSachPhieuDat.FirstOrDefault(x => x.MaPhieuDat == maPhieuDat);
        if (phieu is null) { DatThongBao($"Không tìm thấy phiếu {maPhieuDat}.", true); return; }
        PhieuDatDaChon = phieu;
    }

    /// <summary>Mở sẵn khách vãng lai vào một phòng trống (từ Tổng quan điều hành): chọn đúng phòng, lễ tân chỉ còn tìm/chọn khách.</summary>
    public async Task MoVangLaiAsync(string maPhong)
    {
        CheDo = "VANG_LAI";
        await TaiPhongTrongAsync();
        PhongVangLaiDaChon = PhongTrong.FirstOrDefault(x => x.MaPhong == maPhong);
        if (PhongVangLaiDaChon is null) DatThongBao($"Phòng {maPhong} hiện không nhận ngay được (chưa dọn/kiểm tra, đã giữ cho khách đến hoặc đã có khách).", true);
    }

    private void DatThongBao(string noiDung, bool laLoi) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    [RelayCommand]
    private async Task TimPhieuDatAsync()
    {
        DangTai = true;
        try
        {
            var ds = await _api.TimKiemPhieuDatAsync(TuKhoa.Trim(), ChiPhieuDaXacNhan ? "DA_XAC_NHAN" : null);
            DanhSachPhieuDat.Clear();
            foreach (var x in ds.OrderBy(x => x.NgayDonDuKien).ThenBy(x => x.MaPhieuDat)) DanhSachPhieuDat.Add(x);
            if (DanhSachPhieuDat.Count == 0)
                DatThongBao(string.IsNullOrWhiteSpace(TuKhoa) ? "Hiện không có phiếu đã xác nhận cọc nào chờ nhận phòng." : $"Không tìm thấy phiếu nào khớp \"{TuKhoa.Trim()}\"" + (ChiPhieuDaXacNhan ? " (đang chỉ lọc phiếu đã xác nhận cọc - bỏ tick để tra cả phiếu khác)." : "."), true);
            else if (ThongBaoLaLoi && ThongBao.StartsWith("Không tìm thấy phiếu", StringComparison.Ordinal)) ThongBao = "";
        }
        catch (LoiYeuCauApi e) { DatThongBao(e.Message, true); }
        finally { DangTai = false; }
    }

    [RelayCommand]
    private async Task TimKhachAsync()
    {
        try
        {
            var ds = await _api.TimKiemKhachHangAsync(TuKhoaKhach.Trim());
            DanhSachKhach.Clear();
            foreach (var x in ds) DanhSachKhach.Add(x);
            DaTimKhach = true;
            OnPropertyChanged(nameof(TrangThaiTimKhach));
            if (DanhSachKhach.Count == 1) KhachDaChon = DanhSachKhach[0];
        }
        catch (LoiYeuCauApi e) { DatThongBao(e.Message, true); }
    }

    partial void OnChiPhieuDaXacNhanChanged(bool value) { if (DanhSachPhieuDat.Count > 0 || !string.IsNullOrEmpty(TuKhoa)) _ = TimPhieuDatAsync(); }
    partial void OnKhachDaChonChanged(KhachHangDto? value) { OnPropertyChanged(nameof(KhachVangLaiThieuCccd)); OnPropertyChanged(nameof(KhachVangLaiGhiChu)); }
    partial void OnKetQuaNhanChanged(string value) => OnPropertyChanged(nameof(CoKetQua));
    partial void OnThongTinChanged(ThongTinNhanPhongDto? value)
    {
        OnPropertyChanged(nameof(LaNhanSom)); OnPropertyChanged(nameof(CoThongTin)); OnPropertyChanged(nameof(CanNhapCccd));
    }

    partial void OnPhieuDatDaChonChanged(PhieuDatTomTatDto? value)
    {
        OnPropertyChanged(nameof(ChuaChonPhieu));
        LyDoNhanSom = ""; CccdKhachPhieu = "";
        PhongCuaPhieuDat.Clear();
        ThongTin = null;
        if (value is not null) _ = TaiThongTinPhieuAsync(value.MaPhieuDat);
    }

    private async Task TaiThongTinPhieuAsync(string maPhieuDat)
    {
        try
        {
            var tt = await _api.GetThongTinNhanPhongAsync(maPhieuDat);
            if (PhieuDatDaChon?.MaPhieuDat != maPhieuDat) return;   // người dùng đã chọn phiếu khác trong lúc chờ tải
            ThongTin = tt;
            PhongCuaPhieuDat.Clear();
            foreach (var p in tt.Phong) PhongCuaPhieuDat.Add(new DongNhanPhong(p));
        }
        catch (LoiYeuCauApi e) { DatThongBao(e.Message, true); }
    }

    [RelayCommand]
    private async Task NhanTheoPhieuAsync()
    {
        if (DangNhanPhong) return;
        if (PhieuDatDaChon is null || ThongTin is null) { DatThongBao("Vui lòng chọn phiếu đặt.", true); return; }
        if (!ThongTin.NhanDuoc) { DatThongBao($"Chưa nhận phòng được: {ThongTin.LyDoKhongNhanDuoc}", true); return; }
        var daChon = PhongCuaPhieuDat.Where(x => x.DaChon && x.NhanDuoc).ToList();
        if (daChon.Count == 0) { DatThongBao("Vui lòng chọn ít nhất 1 phòng sẵn sàng để nhận (bỏ tick các phòng khách chưa tới nếu đoàn đến rải rác).", true); return; }
        foreach (var d in daChon)
            if (d.SoNguoi < 1 || d.SoNguoi > d.SucChuaToiDa) { DatThongBao($"Phòng {d.MaPhong}: số người phải từ 1 đến {d.SucChuaToiDa} (tiêu chuẩn {d.SucChuaTieuChuan}, người ở thêm bị tính phụ thu).", true); return; }
        if (ThongTin.ThieuCccd && string.IsNullOrWhiteSpace(CccdKhachPhieu)) { DatThongBao("Hồ sơ khách chưa có giấy tờ: nhập số CCCD/hộ chiếu của khách trước khi nhận phòng.", true); return; }

        var nhanSom = LaNhanSom;
        if (nhanSom && string.IsNullOrWhiteSpace(LyDoNhanSom)) { DatThongBao($"Khách đến sớm (ngày đón dự kiến {PhieuDatDaChon.NgayDonDuKien:dd/MM/yyyy}): cần nhập lý do nhận phòng sớm.", true); return; }

        var tomTat = $"Khách: {ThongTin.KhachHienThi}\nPhiếu: {ThongTin.MaPhieuDat} · {ThongTin.NgayHienThi}\n" +
                     "Phòng nhận: " + string.Join(", ", daChon.Select(x => $"{x.MaPhong} ({x.SoNguoi} người)")) + "\n" +
                     ThongTin.TienHienThi + (ThongTin.DaThuCoc ? "" : "\n⚠ Cọc CHƯA thu.") +
                     (nhanSom ? "\n\nNhận SỚM: ngày đón dời về hôm nay, tiền phòng tính từ hôm nay." : "") +
                     (daChon.Count < ThongTin.Phong.Count(x => !x.DaNhan) ? "\n\nChỉ nhận MỘT PHẦN số phòng của phiếu." : "") +
                     "\n\nXác nhận nhận phòng? Thao tác được ghi nhật ký (ai, lúc nào).";
        if (!HopThoai.XacNhan("Xác nhận nhận phòng", tomTat)) return;

        DangNhanPhong = true;
        try
        {
            var cccd = ThongTin.ThieuCccd ? CccdKhachPhieu.Trim() : null;
            var ds = daChon.Select((x, i) => new ThongTinNhanPhong(x.MaPhong, i == 0 ? cccd : null, x.SoNguoi)).ToList();
            var kq = await _api.NhanPhongTheoPhieuAsync(PhieuDatDaChon.MaPhieuDat, new YeuCauNhanPhong(ds.Select(x => x.MaPhong).ToList(), ds, nhanSom, nhanSom ? LyDoNhanSom.Trim() : null));
            HienKetQua(kq, ThongTin.HoTenKhach);
            DatThongBao($"Đã nhận phòng thành công: phiếu nhận {kq.MaPhieuNhan}." + (nhanSom ? " Ngày đón của phiếu đã dời về hôm nay, tiền phòng tính từ hôm nay." : ""), false);
            LyDoNhanSom = ""; CccdKhachPhieu = "";
            await TaiPhongTrongAsync();
            await TaiThongTinPhieuAsync(PhieuDatDaChon.MaPhieuDat);
        }
        catch (LoiYeuCauApi e)
        {
            DatThongBao(e.Message, true);
            // Lỗi tranh chấp (phòng vừa bị nhận/đổi ở nơi khác...) → tải lại để lễ tân thấy tình trạng thật.
            await TaiThongTinPhieuAsync(PhieuDatDaChon.MaPhieuDat);
        }
        finally { DangNhanPhong = false; }
    }

    [RelayCommand]
    private async Task TaoKhachMoiAsync()
    {
        if (string.IsNullOrWhiteSpace(HoTenMoi) || string.IsNullOrWhiteSpace(SdtMoi)) { DatThongBao("Nhập họ tên và số điện thoại của khách mới.", true); return; }
        try
        {
            var yc = new YeuCauKhachHang(HoTenMoi.Trim(), SdtMoi.Trim(), string.IsNullOrWhiteSpace(CccdMoi) ? null : CccdMoi.Trim(), null, null);
            var trung = await _api.KiemTraKhachTrungAsync(yc, null);
            if (trung.Count > 0)
            {
                // Trùng chắc chắn (SĐT/giấy tờ) thì chặn; nghi ngờ (cùng tên...) thì hiện để chọn khách đã có thay vì tạo thêm.
                DanhSachKhach.Clear();
                foreach (var t in trung) DanhSachKhach.Add(new KhachHangDto { MaKhach = t.MaKhach, HoTen = t.HoTen, SoDT = t.SoDT, CCCD = t.CCCD });
                OnPropertyChanged(nameof(TrangThaiTimKhach));
                var chacChan = trung.Any(x => x.Muc == "CHAC_CHAN");
                DatThongBao((chacChan ? "Không tạo khách mới - đã có hồ sơ trùng: " : "Có thể đã có hồ sơ của khách này - chọn trong danh sách nếu đúng, hoặc bấm tạo lại để xác nhận khác người: ") +
                            string.Join(" | ", trung.Select(x => x.HienThi)), true);
                if (chacChan || !_xacNhanKhacNguoi) { _xacNhanKhacNguoi = !chacChan; return; }
            }
            var moi = await _api.TaoKhachHangAsync(yc);
            _xacNhanKhacNguoi = false;
            DanhSachKhach.Clear(); DanhSachKhach.Add(moi);
            KhachDaChon = moi;
            HoTenMoi = SdtMoi = CccdMoi = "";
            DatThongBao($"Đã tạo hồ sơ khách {moi.HoTen} ({moi.MaKhach}) và chọn để nhận phòng.", false);
        }
        catch (LoiYeuCauApi e) { DatThongBao(e.Message, true); }
    }
    private bool _xacNhanKhacNguoi;

    [RelayCommand]
    private async Task NhanVangLaiAsync()
    {
        if (DangNhanPhong) return;
        if (KhachDaChon is null || PhongVangLaiDaChon is null) { DatThongBao("Vui lòng chọn khách hàng và phòng trống.", true); return; }
        if (SoNguoiVangLai < 0) { DatThongBao("Số người không được âm.", true); return; }
        if (SoDemVangLai < 1) { DatThongBao("Khách lưu trú phải có ngày trả phòng cụ thể: nhập số đêm ở từ 1 trở lên.", true); return; }
        if (KhachVangLaiThieuCccd && string.IsNullOrWhiteSpace(CccdVangLai)) { DatThongBao("Khách chưa có giấy tờ: nhập số CCCD/hộ chiếu trước khi nhận phòng.", true); return; }

        var ngayTra = DateOnly.FromDateTime(DateTime.Today).AddDays(SoDemVangLai);
        var tomTat = $"Khách: {KhachDaChon.HoTen} · {KhachDaChon.SoDT}\nPhòng: {PhongVangLaiDaChon.TenPhong} ({PhongVangLaiDaChon.TenLoaiPhong}, tầng {PhongVangLaiDaChon.Tang})\n" +
                     $"Số người: {(SoNguoiVangLai > 0 ? SoNguoiVangLai.ToString() : "theo sức chứa tiêu chuẩn")} · Trả dự kiến: {ngayTra:dd/MM/yyyy} trước {GioTraPhongChuan} ({SoDemVangLai} đêm)\n" +
                     "Khách vãng lai không có phiếu đặt/cọc trước.\n\nXác nhận nhận phòng? Thao tác được ghi nhật ký (ai, lúc nào).";
        if (!HopThoai.XacNhan("Xác nhận nhận khách vãng lai", tomTat)) return;

        DangNhanPhong = true;
        try
        {
            var kq = await _api.NhanKhachVangLaiAsync(new YeuCauNhanPhongVangLai(KhachDaChon.MaKhach,
                [new(PhongVangLaiDaChon.MaPhong, KhachVangLaiThieuCccd ? CccdVangLai.Trim() : null, Math.Max(SoNguoiVangLai, 0))], ngayTra));
            HienKetQua(kq, KhachDaChon.HoTen);
            DatThongBao($"Đã nhận phòng thành công: phiếu nhận {kq.MaPhieuNhan}.", false);
            CccdVangLai = ""; SoNguoiVangLai = 0; SoDemVangLai = 1;
            PhongVangLaiDaChon = null;
            await TaiPhongTrongAsync();
        }
        catch (LoiYeuCauApi e)
        {
            DatThongBao(e.Message, true);
            await TaiPhongTrongAsync();   // phòng có thể vừa bị người khác nhận → bỏ khỏi danh sách
        }
        finally { DangNhanPhong = false; }
    }

    private void HienKetQua(KetQuaNhanPhongDto kq, string tenKhach)
    {
        var phong = string.Join(", ", kq.PhongDaNhan.Select(x => $"{x.MaPhong} ({x.SoNguoi} người, {x.DonGia:N0}đ/đêm) → {(x.TinhTrangPhong == "OC" ? "có khách" : x.TinhTrangPhong)}"));
        var phieu = kq.MaPhieuDat is null ? "Khách vãng lai (không có phiếu đặt)." : $"Phiếu {kq.MaPhieuDat} → {kq.TrangThaiPhieuDat switch { "DA_NHAN_PHONG" => "Đã nhận phòng (đủ phòng)", "DA_XAC_NHAN" => "Đã xác nhận (còn phòng chưa nhận)", var t => t }}.";
        KetQuaNhan = $"✔ Đã nhận phòng cho {tenKhach} — phiếu nhận {kq.MaPhieuNhan}\nLúc {kq.NgayNhan:HH:mm dd/MM/yyyy} · người thực hiện: {kq.NguoiThucHien ?? "?"}\nPhòng: {phong}\n{phieu}";
    }

    private async Task TaiPhongTrongAsync()
    {
        try
        {
            var ds = (await _api.GetRoomsAsync()).Where(x => x.BanDuocNgay).ToList();
            var dangChon = PhongVangLaiDaChon?.MaPhong;
            PhongTrong.Clear();
            foreach (var x in ds) PhongTrong.Add(x);
            if (dangChon is not null) PhongVangLaiDaChon = PhongTrong.FirstOrDefault(x => x.MaPhong == dangChon);
        }
        catch (LoiYeuCauApi e) { DatThongBao(e.Message, true); }
    }
}
