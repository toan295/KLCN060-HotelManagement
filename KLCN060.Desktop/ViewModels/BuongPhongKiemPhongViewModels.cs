using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Một dòng đồ dùng trong bảng kiểm phòng: số lượng trước → sau (+ bổ sung), lý do hao hụt và tiền dự kiến tính cho khách.</summary>
public partial class DongKiemViewModel : ObservableObject
{
    public string MaVatDung { get; }
    public string TenVatDung { get; }
    public string Nhom { get; }
    public string DonViTinh { get; }
    public int SoLuongChuan { get; }
    public bool TinhPhi { get; }
    public decimal? GiaTinhPhi { get; }
    public string NhomHienThi => VatDungDto.TenNhomVatDung(Nhom);

    [ObservableProperty] private int soLuongTruoc;
    [ObservableProperty] private int soLuongSau;
    [ObservableProperty] private int soLuongBoSung;

    /// <summary>Mã lý do (TIEU_THU / MAT_THIEU / HU_HONG); rỗng = để hệ thống chọn theo nhóm đồ dùng.</summary>
    [ObservableProperty] private string lyDoMa = "";
    [ObservableProperty] private string ghiChu = "";

    public DongKiemViewModel(DongMauKiemDto mau)
    {
        MaVatDung = mau.MaVatDung; TenVatDung = mau.TenVatDung; Nhom = mau.Nhom; DonViTinh = mau.DonViTinh; SoLuongChuan = mau.SoLuongChuan;
        TinhPhi = mau.TinhPhi; GiaTinhPhi = mau.GiaTinhPhi;
        SoLuongTruoc = mau.SoLuongTruoc;
        SoLuongSau = mau.SoLuongTruoc;   // mặc định chưa hao hụt: nhân viên chỉ sửa dòng nào thay đổi
    }

    public int HaoHut => SoLuongTruoc - SoLuongSau;

    /// <summary>Lý do thực tế sẽ áp dụng (khớp mặc định của API theo nhóm đồ dùng).</summary>
    public string LyDoThucTe => HaoHut <= 0 ? "" : !string.IsNullOrEmpty(LyDoMa) ? LyDoMa : Nhom switch
    {
        "MINIBAR" or "NUOC_UONG" or "AMENITIES" => "TIEU_THU", "KHAN_GA_GOI" => "MAT_THIEU", _ => "HU_HONG"
    };

    public string TrangThaiHienThi => HaoHut < 0 ? "Còn nhiều hơn số trước - nhập phần thêm ở cột Bổ sung"
        : HaoHut == 0 ? "" : $"Thiếu {HaoHut} · {DongKiemDto.TenLyDoHaoHut(LyDoThucTe)}" + (TienDuKienHienThi is { Length: > 0 } t ? $" · tính khách {t}" : "");

    public string TienDuKienHienThi => TinhPhi && HaoHut > 0 && LyDoThucTe == "TIEU_THU" && GiaTinhPhi is { } g ? $"{HaoHut * g:N0}đ" : "";

    private void CapNhatTinhToan()
    {
        OnPropertyChanged(nameof(HaoHut));
        OnPropertyChanged(nameof(LyDoThucTe));
        OnPropertyChanged(nameof(TrangThaiHienThi));
        OnPropertyChanged(nameof(TienDuKienHienThi));
    }

    partial void OnSoLuongTruocChanged(int value) => CapNhatTinhToan();
    partial void OnSoLuongSauChanged(int value) => CapNhatTinhToan();
    partial void OnLyDoMaChanged(string value) => CapNhatTinhToan();
}

/// <summary>Tab "Đồ dùng / minibar": chọn phòng → bảng kiểm với số lượng gợi ý → lưu phiếu; xem/hủy các phiếu kiểm gần đây của phòng.</summary>
public partial class KiemPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;

    public static IReadOnlyList<LuaChon> DanhSachLyDo { get; } = [new("", "(tự động)"), new("TIEU_THU", "Tiêu thụ"), new("MAT_THIEU", "Mất / thiếu"), new("HU_HONG", "Hư hỏng")];

    public ObservableCollection<PhongDto> Phongs { get; } = [];
    public ObservableCollection<DongKiemViewModel> Dong { get; } = [];
    public ObservableCollection<PhieuKiemPhongDto> LichSu { get; } = [];

    [ObservableProperty] private PhongDto? phongDaChon;
    [ObservableProperty] private string ghiChuPhieu = "";
    [ObservableProperty] private string thongTinPhong = "";
    [ObservableProperty] private PhieuKiemPhongDto? phieuLichSuDaChon;
    [ObservableProperty] private bool dangLuu;

    /// <summary>Lý do hủy phiếu kiểm (bắt buộc, ghi vào nhật ký cùng nội dung phiếu).</summary>
    [ObservableProperty] private string lyDoHuyPhieu = "";

    public bool CoDong => Dong.Count > 0;
    public bool CoTheHuyPhieu => PhieuLichSuDaChon is not null;

    public KiemPhongViewModel(ApiClient api)
    {
        _api = api;
        Dong.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CoDong));
        _ = TaiPhongAsync();
    }

    partial void OnPhieuLichSuDaChonChanged(PhieuKiemPhongDto? value) => OnPropertyChanged(nameof(CoTheHuyPhieu));
    partial void OnPhongDaChonChanged(PhongDto? value) { _ = TaiMauAsync(); }

    private async Task TaiPhongAsync()
    {
        try
        {
            foreach (var p in (await _api.GetRoomsAsync()).OrderBy(x => x.TenPhong, StringComparer.Ordinal)) Phongs.Add(p);
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    public Task TaiMauAsync() => TaiTuanTuAsync(TaiMauKhongKhoaAsync);

    private async Task TaiMauKhongKhoaAsync()
    {
        Dong.Clear();
        LichSu.Clear();
        ThongTinPhong = "";
        if (PhongDaChon is null) return;
        try
        {
            var ma = PhongDaChon.MaPhong;
            var mau = await _api.GetMauKiemPhongAsync(ma);
            if (PhongDaChon?.MaPhong != ma) return;
            foreach (var d in mau.Dong) Dong.Add(new DongKiemViewModel(d));
            ThongTinPhong = string.IsNullOrEmpty(mau.MaPhieuNhan)
                ? "Phòng không có khách: hao hụt chỉ được ghi nhận, không tính phí cho khách."
                : $"Phòng đang có khách (lượt ở {mau.MaPhieuNhan}): minibar tiêu thụ sẽ gửi lễ tân tính phí." + (mau.KiemGanNhatLuc is { } t ? $" Lần kiểm gần nhất {t:dd/MM HH:mm} - số \"trước\" lấy theo kết quả đó." : "");
            foreach (var p in await _api.GetPhieuKiemAsync(ma)) LichSu.Add(p);
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    /// <summary>Lưu phiếu kiểm: kiểm tra số liệu ở máy trước (số còn lại không vượt số trước, không âm) rồi gửi; API kiểm tra lại và tính phí.</summary>
    [RelayCommand]
    private async Task LuuAsync()
    {
        if (PhongDaChon is null) { Loi("Chọn phòng cần kiểm."); return; }
        if (Dong.Count == 0) { Loi("Chưa có đồ dùng nào để kiểm - quản lý cần thêm danh mục đồ dùng."); return; }
        foreach (var d in Dong)
        {
            if (d.SoLuongTruoc < 0 || d.SoLuongSau < 0 || d.SoLuongBoSung < 0) { Loi($"\"{d.TenVatDung}\": số lượng không được âm."); return; }
            if (d.SoLuongSau > d.SoLuongTruoc) { Loi($"\"{d.TenVatDung}\": số còn lại ({d.SoLuongSau}) không được lớn hơn số trước khi kiểm ({d.SoLuongTruoc}). Đồ bổ sung thêm hãy nhập ở cột Bổ sung."); return; }
        }
        DangLuu = true;
        try
        {
            var dong = Dong.Select(d => new YeuCauDongKiem(d.MaVatDung, d.SoLuongTruoc, d.SoLuongSau, d.SoLuongBoSung,
                d.HaoHut > 0 && !string.IsNullOrEmpty(d.LyDoMa) ? d.LyDoMa : null, string.IsNullOrWhiteSpace(d.GhiChu) ? null : d.GhiChu.Trim())).ToList();
            var phieu = await _api.TaoPhieuKiemAsync(new YeuCauPhieuKiem(PhongDaChon.MaPhong, string.IsNullOrWhiteSpace(GhiChuPhieu) ? null : GhiChuPhieu.Trim(), dong));
            var choTinhPhi = phieu.Dong.Count(x => x.TrangThaiTinhPhi == "CHO_TINH_PHI");
            GhiChuPhieu = "";
            await TaiMauAsync();
            Ok($"Đã lưu phiếu kiểm {phieu.MaHienThi} phòng {phieu.MaPhong}: {phieu.Dong.Count(x => x.HaoHut > 0)} đồ dùng hao hụt" + (choTinhPhi > 0 ? $"; {choTinhPhi} khoản minibar đã gửi lễ tân tính phí." : "."));
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
        finally { DangLuu = false; }
    }

    [RelayCommand]
    private async Task HuyPhieuAsync()
    {
        if (PhieuLichSuDaChon is null) { Loi("Chọn phiếu kiểm cần hủy."); return; }
        if (string.IsNullOrWhiteSpace(LyDoHuyPhieu)) { Loi("Nhập lý do hủy phiếu kiểm (VD: nhập nhầm phòng)."); return; }
        var p = PhieuLichSuDaChon;
        if (!HopThoai.XacNhan("Hủy phiếu kiểm", $"Hủy phiếu {p.MaHienThi} (phòng {p.MaPhong}, {p.ThoiGian:dd/MM HH:mm})?\n\nChỉ dùng khi nhập nhầm. Chỉ người nhập (trong 60 phút) hoặc quản lý hủy được; phiếu đã tính phí hoặc đã bỏ qua khoản minibar thì không hủy được (giao dịch đã phát sinh).\n\nLý do: {LyDoHuyPhieu.Trim()}")) return;
        try
        {
            await _api.HuyPhieuKiemAsync(p.MaPhieuKiem, LyDoHuyPhieu.Trim());
            LyDoHuyPhieu = "";
            await TaiMauAsync();
            Ok($"Đã hủy phiếu {p.MaHienThi}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }
}

/// <summary>Tab "Minibar chờ tính phí" của lễ tân: các khoản tiêu thụ buồng phòng ghi nhận → tính phí vào lượt lưu trú hoặc bỏ qua kèm lý do.</summary>
public partial class MinibarChoTinhPhiViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;

    public static IReadOnlyList<LuaChon> DanhSachTrangThai { get; } = [new("CHO_TINH_PHI", "Chờ tính phí"), new("DA_TINH_PHI", "Đã tính phí"), new("BO_QUA", "Đã bỏ qua")];

    public bool CoQuyenTinhPhi { get; } = AppSession.Current.CoQuyen(QuyenHeThong.ThaoTacLeTan);
    public ObservableCollection<KhoanMinibarDto> Khoan { get; } = [];
    private readonly System.ComponentModel.ICollectionView _viewKhoan;
    [ObservableProperty] private string tuKhoaMinibar = "";
    [ObservableProperty] private string soHienThi = "";
    [ObservableProperty] private KhoanMinibarDto? khoanDaChon;
    [ObservableProperty] private LuaChon? trangThaiXem;
    [ObservableProperty] private string lyDoBoQua = "";
    [ObservableProperty] private string tomTat = "";

    public bool CoTheXuLy => KhoanDaChon is { TrangThaiTinhPhi: "CHO_TINH_PHI" };
    public bool CoTheTinhPhi => CoQuyenTinhPhi && CoTheXuLy;

    public MinibarChoTinhPhiViewModel(ApiClient api)
    {
        _api = api;
        trangThaiXem = DanhSachTrangThai[0];
        _viewKhoan = LocBang.Gan(Khoan, (KhoanMinibarDto x) => LocBang.Chua(TuKhoaMinibar, x.MaPhong, x.HoTenKhach, x.TenVatDung, x.NguoiKiemHienThi, x.MaPhieuNhan));
        _ = TaiAsync();
    }

    partial void OnTuKhoaMinibarChanged(string value) => LamMoiLoc();
    private void LamMoiLoc() { _viewKhoan.Refresh(); SoHienThi = LocBang.TomTat(LocBang.Dem(_viewKhoan), Khoan.Count, "khoản"); }

    partial void OnKhoanDaChonChanged(KhoanMinibarDto? value)
    {
        OnPropertyChanged(nameof(CoTheXuLy));
        OnPropertyChanged(nameof(CoTheTinhPhi));
    }
    partial void OnTrangThaiXemChanged(LuaChon? value) { _ = TaiAsync(); }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            var ma = KhoanDaChon?.MaChiTiet;
            var ds = await _api.GetKhoanMinibarAsync(TrangThaiXem?.Ma);
            Khoan.Clear();
            foreach (var k in ds) Khoan.Add(k);
            KhoanDaChon = ma is null ? null : Khoan.FirstOrDefault(x => x.MaChiTiet == ma);
            LamMoiLoc();
            TomTat = TrangThaiXem?.Ma == "CHO_TINH_PHI"
                ? $"{ds.Count} khoản chờ tính phí, tổng {ds.Sum(x => x.ThanhTien):N0}đ. Khách còn khoản chưa xử lý thì chưa trả phòng được."
                : $"{ds.Count} khoản ({TrangThaiXem?.Ten?.ToLowerInvariant()}), tổng {ds.Sum(x => x.ThanhTien):N0}đ";
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task TinhPhiAsync()
    {
        if (KhoanDaChon is not { TrangThaiTinhPhi: "CHO_TINH_PHI" } k) { Loi("Chọn một khoản đang chờ tính phí."); return; }
        if (!HopThoai.XacNhan("Tính phí minibar", $"Tính phí {k.SoLuongTinhPhi} {k.DonViTinh} \"{k.TenVatDung}\" = {k.ThanhTien:N0}đ vào lượt lưu trú phòng {k.MaPhong} ({k.HoTenKhach})?")) return;
        try
        {
            await _api.TinhPhiMinibarAsync(k.MaChiTiet);
            await TaiAsync();
            Ok($"Đã tính {k.ThanhTien:N0}đ ({k.TenVatDung}) vào lượt lưu trú phòng {k.MaPhong}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }

    [RelayCommand]
    private async Task BoQuaAsync()
    {
        if (KhoanDaChon is not { TrangThaiTinhPhi: "CHO_TINH_PHI" } k) { Loi("Chọn một khoản đang chờ tính phí."); return; }
        if (string.IsNullOrWhiteSpace(LyDoBoQua)) { Loi("Nhập lý do bỏ qua khoản phí (VD: tặng khách, kiểm nhầm)."); return; }
        try
        {
            await _api.BoQuaMinibarAsync(k.MaChiTiet, LyDoBoQua.Trim());
            LyDoBoQua = "";
            await TaiAsync();
            Ok($"Đã bỏ qua khoản {k.TenVatDung} phòng {k.MaPhong} (không tính tiền khách).");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); await TaiAsync(); }
    }
}

/// <summary>Tab "Danh mục đồ dùng" của quản lý: thêm/sửa/xóa đồ dùng cần kiểm và gắn dịch vụ để tính phí (minibar, nước).</summary>
/// <summary>Một loại phòng trong danh sách chọn phạm vi áp dụng của đồ dùng.</summary>
public partial class LoaiPhongChonViewModel : ObservableObject
{
    public LoaiPhongVatDungDto Loai { get; }
    public string Ma => Loai.MaLoai;
    public string Ten => $"{Loai.TenLoai}  -  {Loai.MoTa}";
    [ObservableProperty] private bool duocChon;
    public Action? KhiDoi { get; set; }
    public LoaiPhongChonViewModel(LoaiPhongVatDungDto loai) { Loai = loai; }
    partial void OnDuocChonChanged(bool value) => KhiDoi?.Invoke();
}

public partial class DanhMucVatDungViewModel : TabBuongPhongViewModel
{
    public static IReadOnlyList<LuaChon> DanhSachCachTinh { get; } =
    [
        new("MOI_PHONG", "Mỗi phòng (cố định, không đổi theo loại phòng)"), new("MOI_GIUONG", "Mỗi giường (ga, gối - phòng 2 giường gấp đôi)"), new("MOI_NGUOI", "Mỗi người tiêu chuẩn (khăn, dép, amenities)")
    ];
    public ObservableCollection<LoaiPhongChonViewModel> LoaiPhongChons { get; } = [];
    [ObservableProperty] private LuaChon? cachTinhChon;
    [ObservableProperty] private bool apDungTatCa = true;
    [ObservableProperty] private string xemTruoc = "";
    partial void OnCachTinhChonChanged(LuaChon? value) => CapNhatXemTruoc();
    partial void OnApDungTatCaChanged(bool value) => CapNhatXemTruoc();
    partial void OnDinhMucChanged(string value) => CapNhatXemTruoc();
    partial void OnDonViTinhChanged(string value) => CapNhatXemTruoc();

    /// <summary>Cho quản lý thấy NGAY định mức thực tế của từng loại phòng (phòng nhiều giường / nhiều người cần bao nhiêu) trước khi lưu.</summary>
    private void CapNhatXemTruoc()
    {
        if (!int.TryParse(DinhMuc, out var n) || n < 0) { XemTruoc = "Nhập định mức là một số nguyên không âm."; return; }
        var cac = LoaiPhongChons.Where(x => ApDungTatCa || x.DuocChon).ToList();
        if (cac.Count == 0) { XemTruoc = "Chưa chọn loại phòng nào - đồ dùng sẽ không xuất hiện ở phòng nào."; return; }
        var dv = string.IsNullOrWhiteSpace(DonViTinh) ? "" : " " + DonViTinh.Trim();
        XemTruoc = "Định mức thực tế: " + string.Join("  ·  ", cac.Select(x =>
            $"{x.Loai.TenLoai} ({x.Loai.SoGiuong} giường, {x.Loai.SoNguoiTieuChuan} người) = " +
            (CachTinhChon?.Ma switch { "MOI_GIUONG" => n * x.Loai.SoGiuong, "MOI_NGUOI" => n * Math.Max(1, x.Loai.SoNguoiTieuChuan), _ => n }) + dv));
    }

    private readonly ApiClient _api;

    public static IReadOnlyList<LuaChon> DanhSachNhom { get; } =
        [new("KHAN_GA_GOI", "Khăn / ga / gối"), new("AMENITIES", "Amenities"), new("NUOC_UONG", "Nước uống"), new("MINIBAR", "Minibar"), new("KHAC", "Đồ dùng khác")];

    private static readonly DichVuDto KhongTinhPhi = new() { MaDV = "", TenDV = "(Không tính phí)" };

    public ObservableCollection<VatDungDto> DanhSach { get; } = [];
    private readonly System.ComponentModel.ICollectionView _viewDanhMuc;
    public static IReadOnlyList<LuaChon> LocNhomTuyChon { get; } = [new("", "Mọi nhóm"), .. DanhSachNhom];
    public static IReadOnlyList<LuaChon> LocTrangThaiTuyChon { get; } = [new("", "Mọi trạng thái"), new("DANG_DUNG", "Đang dùng"), new("NGUNG", "Ngừng dùng")];
    [ObservableProperty] private string tuKhoaDanhMuc = "";
    [ObservableProperty] private string locNhom = "";
    [ObservableProperty] private string locTrangThai = "";
    [ObservableProperty] private string soHienThi = "";
    public ObservableCollection<DichVuDto> DichVus { get; } = [];
    [ObservableProperty] private VatDungDto? vatDungDaChon;
    [ObservableProperty] private string ten = "";
    [ObservableProperty] private LuaChon? nhom;
    [ObservableProperty] private string donViTinh = "";
    [ObservableProperty] private string dinhMuc = "1";
    [ObservableProperty] private DichVuDto? dichVu;
    [ObservableProperty] private bool hoatDong = true;
    [ObservableProperty] private string tomTat = "";

    public bool DangSua => VatDungDaChon is not null;
    public string NutLuu => DangSua ? "Lưu thay đổi" : "Thêm đồ dùng";

    public DanhMucVatDungViewModel(ApiClient api)
    {
        _api = api;
        Nhom = DanhSachNhom[0];
        CachTinhChon = DanhSachCachTinh[0];
        _viewDanhMuc = LocBang.Gan(DanhSach, (VatDungDto x) => LocBang.Chua(TuKhoaDanhMuc, x.MaVatDung, x.TenVatDung, x.TenDV)
            && (LocNhom == "" || x.Nhom == LocNhom) && (LocTrangThai == "" || (LocTrangThai == "DANG_DUNG") == x.HoatDong));
        _ = TaiAsync();
    }

    partial void OnTuKhoaDanhMucChanged(string value) => LamMoiLoc();
    partial void OnLocNhomChanged(string value) => LamMoiLoc();
    partial void OnLocTrangThaiChanged(string value) => LamMoiLoc();
    private void LamMoiLoc() { _viewDanhMuc.Refresh(); SoHienThi = LocBang.TomTat(LocBang.Dem(_viewDanhMuc), DanhSach.Count, "đồ dùng"); }
    [RelayCommand] private void XoaLoc() { TuKhoaDanhMuc = ""; LocNhom = ""; LocTrangThai = ""; }

    partial void OnVatDungDaChonChanged(VatDungDto? value)
    {
        OnPropertyChanged(nameof(DangSua));
        OnPropertyChanged(nameof(NutLuu));
        if (value is null) return;
        Ten = value.TenVatDung;
        Nhom = DanhSachNhom.FirstOrDefault(x => x.Ma == value.Nhom);
        DonViTinh = value.DonViTinh;
        DinhMuc = value.SoLuongChuan.ToString();
        CachTinhChon = DanhSachCachTinh.FirstOrDefault(x => x.Ma == value.CachTinh) ?? DanhSachCachTinh[0];
        ApDungTatCa = value.MaLoaiPhongs.Count == 0;
        foreach (var l in LoaiPhongChons) l.DuocChon = value.MaLoaiPhongs.Contains(l.Ma);
        DichVu = DichVus.FirstOrDefault(x => x.MaDV == (value.MaDV ?? ""));
        HoatDong = value.HoatDong;
        CapNhatXemTruoc();
    }

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        try
        {
            if (DichVus.Count == 0)
            {
                DichVus.Add(KhongTinhPhi);
                foreach (var d in await _api.GetServicesAsync(true)) DichVus.Add(d);
            }
            if (LoaiPhongChons.Count == 0)
                foreach (var l in await _api.GetLoaiPhongChoVatDungAsync())
                    LoaiPhongChons.Add(new LoaiPhongChonViewModel(l) { KhiDoi = CapNhatXemTruoc });
            var ma = VatDungDaChon?.MaVatDung;
            var ds = await _api.GetVatDungAsync(chiHoatDong: false);
            DanhSach.Clear();
            foreach (var v in ds) DanhSach.Add(v);
            VatDungDaChon = ma is null ? null : DanhSach.FirstOrDefault(x => x.MaVatDung == ma);
            LamMoiLoc();
            DichVu ??= DichVus.FirstOrDefault();
            TomTat = $"{ds.Count} đồ dùng ({ds.Count(x => x.HoatDong)} đang dùng, {ds.Count(x => x.MaDV is not null)} tính phí được)";
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    /// <summary>Bỏ chọn dòng đang sửa và xóa trắng biểu mẫu để nhập đồ dùng mới.</summary>
    [RelayCommand]
    private void HuySua()
    {
        VatDungDaChon = null;
        Ten = ""; DonViTinh = ""; DinhMuc = "1"; HoatDong = true;
        Nhom = DanhSachNhom[0];
        CachTinhChon = DanhSachCachTinh[0];
        ApDungTatCa = true;
        foreach (var l in LoaiPhongChons) l.DuocChon = false;
        DichVu = DichVus.FirstOrDefault();
        ThongBao = "";
        CapNhatXemTruoc();
    }

    [RelayCommand]
    private async Task LuuAsync()
    {
        if (string.IsNullOrWhiteSpace(Ten)) { Loi("Nhập tên đồ dùng."); return; }
        if (Nhom is null) { Loi("Chọn nhóm."); return; }
        if (string.IsNullOrWhiteSpace(DonViTinh)) { Loi("Nhập đơn vị tính (cái, chai, lon, bộ...)."); return; }
        if (!int.TryParse(DinhMuc, out var chuan) || chuan < 0 || chuan > 1000) { Loi("Định mức phải là số từ 0 đến 1000."); return; }
        var cacLoai = ApDungTatCa ? new List<string>() : LoaiPhongChons.Where(x => x.DuocChon).Select(x => x.Ma).ToList();
        if (!ApDungTatCa && cacLoai.Count == 0) { Loi("Chọn ít nhất một loại phòng, hoặc tích \"Áp dụng cho mọi loại phòng\"."); return; }
        var yc = new YeuCauVatDung(Ten.Trim(), Nhom.Ma, DonViTinh.Trim(), chuan, string.IsNullOrEmpty(DichVu?.MaDV) ? null : DichVu!.MaDV, HoatDong, CachTinhChon?.Ma ?? "MOI_PHONG", cacLoai);
        try
        {
            var kq = VatDungDaChon is null ? await _api.TaoVatDungAsync(yc) : await _api.SuaVatDungAsync(VatDungDaChon.MaVatDung, yc);
            var moi = VatDungDaChon is null;
            await TaiAsync();
            VatDungDaChon = DanhSach.FirstOrDefault(x => x.MaVatDung == kq.MaVatDung);
            Ok(moi ? $"Đã thêm đồ dùng {kq.MaVatDung} - {kq.TenVatDung}." : $"Đã lưu {kq.TenVatDung}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (VatDungDaChon is null) { Loi("Chọn đồ dùng cần xóa."); return; }
        var v = VatDungDaChon;
        if (!HopThoai.XacNhan("Xóa đồ dùng", $"Xóa \"{v.TenVatDung}\" khỏi danh mục kiểm phòng?\n\nĐồ dùng đã có lịch sử kiểm không xóa được - hãy bỏ chọn \"Đang sử dụng\" thay vì xóa.")) return;
        try
        {
            await _api.XoaVatDungAsync(v.MaVatDung);
            HuySua();
            await TaiAsync();
            Ok($"Đã xóa {v.TenVatDung}.");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }
}

/// <summary>Tab "Báo cáo" của quản lý: số phòng đã dọn theo nhân viên + thời gian dọn trung bình, và hao hụt đồ dùng (tiêu thụ / mất thiếu / hư hỏng).</summary>
public partial class BaoCaoBuongPhongViewModel : TabBuongPhongViewModel
{
    private readonly ApiClient _api;

    public ObservableCollection<DongBaoCaoDonPhongDto> DonPhong { get; } = [];
    public ObservableCollection<HaoHutDto> HaoHut { get; } = [];
    private readonly System.ComponentModel.ICollectionView _viewDonPhong;
    private readonly System.ComponentModel.ICollectionView _viewHaoHut;
    public static IReadOnlyList<LuaChon> LocHaoHutTuyChon { get; } = [new("", "Tất cả đồ dùng"), new("MAT_THIEU", "Có mất / thiếu"), new("HU_HONG", "Có hư hỏng")];
    [ObservableProperty] private string tuKhoaNhanVien = "";
    [ObservableProperty] private string tuKhoaVatDung = "";
    [ObservableProperty] private string locHaoHut = "";
    [ObservableProperty] private DateTime tuNgay = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime denNgay = DateTime.Today;
    [ObservableProperty] private string tomTatDonPhong = "";
    [ObservableProperty] private string tomTatHaoHut = "";

    public BaoCaoBuongPhongViewModel(ApiClient api)
    {
        _api = api;
        _viewDonPhong = LocBang.Gan(DonPhong, (DongBaoCaoDonPhongDto x) => LocBang.Chua(TuKhoaNhanVien, x.TenHienThi, x.NguoiDon));
        _viewHaoHut = LocBang.Gan(HaoHut, (HaoHutDto x) => LocBang.Chua(TuKhoaVatDung, x.TenVatDung, x.MaVatDung)
            && (LocHaoHut == "" || (LocHaoHut == "MAT_THIEU" ? x.MatThieu > 0 : x.HuHong > 0)));
        _ = TaiAsync();
    }

    partial void OnTuKhoaNhanVienChanged(string value) => _viewDonPhong.Refresh();
    partial void OnTuKhoaVatDungChanged(string value) => _viewHaoHut.Refresh();
    partial void OnLocHaoHutChanged(string value) => _viewHaoHut.Refresh();

    [RelayCommand]
    public Task TaiAsync() => TaiTuanTuAsync(TaiKhongKhoaAsync);

    private async Task TaiKhongKhoaAsync()
    {
        var tu = DateOnly.FromDateTime(TuNgay);
        var den = DateOnly.FromDateTime(DenNgay);
        if (den < tu) { Loi("Đến ngày phải sau hoặc bằng từ ngày."); return; }
        try
        {
            var bc = await _api.GetBaoCaoDonPhongAsync(tu, den);
            DonPhong.Clear();
            foreach (var d in bc.Dong) DonPhong.Add(d);
            TomTatDonPhong = $"{bc.TongPhongDaDon} phòng đã dọn từ {tu:dd/MM} đến {den:dd/MM/yyyy}" + (bc.ThoiGianTrungBinhPhut is { } p ? $" · trung bình {p:0.#} phút/phòng (chỉ tính phòng có bấm nhận dọn)" : "");
            var hh = await _api.GetBaoCaoHaoHutAsync(tu, den);
            HaoHut.Clear();
            foreach (var h in hh) HaoHut.Add(h);
            TomTatHaoHut = $"{hh.Sum(x => x.MatThieu)} mất/thiếu · {hh.Sum(x => x.HuHong)} hư hỏng · {hh.Sum(x => x.TieuThu)} tiêu thụ trong {hh.Sum(x => x.SoLanKiem)} lượt kiểm";
            Ok("");
        }
        catch (LoiYeuCauApi loi) { Loi(loi.Message); }
    }
}
