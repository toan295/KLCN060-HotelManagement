using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DichVuSuDungViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private List<LuotLuuTruDto> _tatCaLuot = [];
    private List<DichVuSuDungDto> _tatCaDong = [];
    private int _lanTai;

    public ObservableCollection<LuotLuuTruDto> Luot { get; } = [];
    public ObservableCollection<DichVuDto> DichVu { get; } = [];
    /// <summary>Dòng dịch vụ của PHÒNG đang chọn (phiếu nhiều phòng: dòng của phòng khác không lẫn vào đây).</summary>
    public ObservableCollection<DichVuSuDungDto> DaDung { get; } = [];
    /// <summary>Gói dịch vụ đang bán (mọi dịch vụ trong gói còn phục vụ) để ghi một lần cho khách.</summary>
    public ObservableCollection<GoiDichVuDto> Goi { get; } = [];
    public ObservableCollection<NhatKyDto> NhatKyDichVu { get; } = [];
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private GoiDichVuDto? goiDaChon;
    [ObservableProperty] private int soLuongGoi = 1;
    [ObservableProperty] private bool dangThemGoi;

    // Hủy dòng ghi nhầm (chỉ khi chưa lập hóa đơn; bắt buộc lý do, có ghi nhật ký)
    [ObservableProperty] private DichVuSuDungDto? dongDaChon;
    [ObservableProperty] private string lyDoHuyDong = "";

    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private DichVuDto? dichVuDaChon;
    [ObservableProperty] private int soLuong = 1;
    [ObservableProperty] private decimal tongCong;
    [ObservableProperty] private decimal tongCaPhieu;
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    // Chặn bấm đúp "Thêm dịch vụ" - API tạo ChiTietSuDungDV là insert thuần, không chặn trùng, bấm đúp
    // sẽ cộng dịch vụ 2 lần (double-charge khách).
    [ObservableProperty] private bool dangThem;

    public bool CoLuotDaChon => LuotDaChon is not null;
    public bool GoiYTimHien => string.IsNullOrEmpty(TuKhoa);
    public bool KhongCoLuot => Luot.Count == 0;
    public string DangGhiChoHienThi => LuotDaChon is null ? "Chưa chọn khách. Chọn một dòng ở bảng \"Khách đang ở\" để ghi dịch vụ đúng người, đúng phòng." :
        $"ĐANG GHI CHO: {LuotDaChon.KhachHienThi} · phòng {LuotDaChon.MaPhong} ({LuotDaChon.TenLoai}) · phiếu nhận {LuotDaChon.MaPhieuNhan}" + (LuotDaChon.MaPhieuDat is null ? " (khách vãng lai)" : $" · phiếu đặt {LuotDaChon.MaPhieuDat}") +
        $" · ở {LuotDaChon.NgayNhanHienThi} → {LuotDaChon.NgayTraHienThi}";
    /// <summary>Thành tiền sẽ ghi = số lượng × đơn giá hiện hành của dịch vụ (đơn giá được chốt tại lúc ghi, đổi giá sau này không ảnh hưởng).</summary>
    public string TinhTienDichVu => DichVuDaChon is null ? "" : SoLuong is >= 1 and <= 999
        ? $"{SoLuong} × {DichVuDaChon.GiaDV:N0}đ/{DichVuDaChon.DonViTinh} = {SoLuong * DichVuDaChon.GiaDV:N0}đ"
        : "Số lượng phải từ 1 đến 999.";
    public string TinhTienGoi => GoiDaChon is null ? "" : SoLuongGoi is >= 1 and <= 999
        ? $"{SoLuongGoi} gói × {GoiDaChon.GiaGoi:N0}đ = {SoLuongGoi * GoiDaChon.GiaGoi:N0}đ (mua lẻ {SoLuongGoi * GoiDaChon.GiaLe:N0}đ)"
        : "Số lượng gói phải từ 1 đến 999.";
    public string TongHienThi => LuotDaChon is null ? "" : $"Phòng {LuotDaChon.MaPhong}: {DaDung.Count} dòng, tổng {TongCong:N0}đ" +
        (TongCaPhieu != TongCong ? $" · cả phiếu (mọi phòng) {TongCaPhieu:N0}đ" : "");
    public string TrangThaiDongHienThi => DongDaChon is null ? "" : DongDaChon.CoTheHuy
        ? "Dòng này hủy được (phiếu chưa lập hóa đơn): nhập lý do rồi bấm hủy."
        : $"Dòng này KHÔNG hủy được: {DongDaChon.LyDoKhongHuy}";
    public bool HuyDuoc => DongDaChon is { CoTheHuy: true } && !DangThem;

    public DichVuSuDungViewModel(ApiClient api, string? maPhieuNhan = null, string? maPhong = null)
    {
        _api = api;
        _ = TaiVaChonAsync(maPhieuNhan, maPhong);
    }

    private void DatThongBao(string noiDung, bool laLoi) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    private async Task TaiVaChonAsync(string? maPhieuNhan, string? maPhong)
    {
        await TaiAsync();
        if (maPhieuNhan is null || maPhong is null) return;
        LuotDaChon = Luot.FirstOrDefault(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong);
        if (LuotDaChon is null) DatThongBao($"Phòng {maPhong} không còn ở trạng thái đang ở.", true);
    }

    private static string BoDau(string s)
    {
        var d = (s ?? "").Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }

    partial void OnTuKhoaChanged(string value) { OnPropertyChanged(nameof(GoiYTimHien)); LocLuot(); }
    private void LocLuot()
    {
        var chon = LuotDaChon;
        var tk = BoDau(TuKhoa.Trim());
        Luot.Clear();
        foreach (var l in _tatCaLuot.Where(x => tk.Length == 0 || BoDau($"{x.MaPhong} {x.HoTenKhach} {x.MaPhieuNhan} {x.MaPhieuDat}").Contains(tk))) Luot.Add(l);
        OnPropertyChanged(nameof(KhongCoLuot));
        LuotDaChon = chon is null ? null : Luot.FirstOrDefault(x => x.MaPhieuNhan == chon.MaPhieuNhan && x.MaPhong == chon.MaPhong);
    }

    partial void OnLuotDaChonChanged(LuotLuuTruDto? value)
    {
        OnPropertyChanged(nameof(CoLuotDaChon)); OnPropertyChanged(nameof(DangGhiChoHienThi));
        DongDaChon = null; LyDoHuyDong = ""; DaDung.Clear(); NhatKyDichVu.Clear(); TongCong = 0; TongCaPhieu = 0; _tatCaDong = [];
        OnPropertyChanged(nameof(TongHienThi));
        if (value is not null) _ = TaiDaDungAsync();
    }

    partial void OnDichVuDaChonChanged(DichVuDto? value) => OnPropertyChanged(nameof(TinhTienDichVu));
    partial void OnSoLuongChanged(int value) => OnPropertyChanged(nameof(TinhTienDichVu));
    partial void OnGoiDaChonChanged(GoiDichVuDto? value) => OnPropertyChanged(nameof(TinhTienGoi));
    partial void OnSoLuongGoiChanged(int value) => OnPropertyChanged(nameof(TinhTienGoi));
    partial void OnDongDaChonChanged(DichVuSuDungDto? value) { OnPropertyChanged(nameof(TrangThaiDongHienThi)); OnPropertyChanged(nameof(HuyDuoc)); }
    partial void OnDangThemChanged(bool value) => OnPropertyChanged(nameof(HuyDuoc));

    /// <summary>Nhắc nếu vừa ghi đúng dịch vụ + số lượng này cho phòng này (tránh ghi trùng do bấm nhầm / hai lễ tân cùng ghi một yêu cầu của khách).</summary>
    private DichVuSuDungDto? GhiGanDay(string maDV, int soLuong)
        => _tatCaDong.Where(x => x.MaDV == maDV && x.SoLuong == soLuong && (x.MaPhong == LuotDaChon?.MaPhong) && x.MaGoi is null && (DateTime.Now - x.NgaySuDung).TotalMinutes <= 5)
            .OrderByDescending(x => x.NgaySuDung).FirstOrDefault();

    [RelayCommand]
    private async Task ThemAsync()
    {
        if (DangThem) return;

        if (LuotDaChon is null || DichVuDaChon is null)
        {
            DatThongBao("Vui lòng chọn khách đang ở và dịch vụ.", true);
            return;
        }

        if (SoLuong is < 1 or > 999)
        {
            DatThongBao("Số lượng phải từ 1 đến 999.", true);
            return;
        }

        var trung = GhiGanDay(DichVuDaChon.MaDV, SoLuong);
        var noiDung = $"Ghi dịch vụ cho: {LuotDaChon.KhachHienThi} · phòng {LuotDaChon.MaPhong}\nDịch vụ: {DichVuDaChon.TenDV}\n{TinhTienDichVu}\n" +
                      (trung is null ? "" : $"\n⚠ Vừa ghi dòng giống hệt lúc {trung.NgaySuDung:HH:mm} (người ghi: {trung.NguoiGhiHienThi}). Chắc chắn ghi thêm lần nữa?\n") +
                      "\nĐơn giá được chốt lúc ghi, thao tác được ghi nhật ký (ai, lúc nào). Xác nhận?";
        if (!HopThoai.XacNhan("Xác nhận ghi dịch vụ", noiDung)) return;

        DangThem = true;
        try
        {
            await _api.ThemDichVuAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong,
                new YeuCauDichVuSuDung(DichVuDaChon.MaDV, SoLuong));
            var ten = DichVuDaChon.TenDV; var tien = SoLuong * DichVuDaChon.GiaDV; var sl = SoLuong;
            await TaiDaDungAsync();
            SoLuong = 1;
            DatThongBao($"Đã ghi {sl} × {ten} = {tien:N0}đ cho phòng {LuotDaChon.MaPhong} ({LuotDaChon.KhachHienThi}). Tổng phòng này: {TongCong:N0}đ.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiAsync();   // lượt có thể vừa trả phòng ở nơi khác
        }
        finally
        {
            DangThem = false;
        }
    }

    private async Task TaiAsync()
    {
        try
        {
            Goi.Clear();
            foreach (var goi in (await _api.GetGoiDichVuAsync()).Where(x => x.CoTheBan))
                Goi.Add(goi);

            _tatCaLuot = (await _api.GetLuotDangOAsync()).OrderBy(x => x.MaPhong, StringComparer.Ordinal).ToList();
            LocLuot();

            DichVu.Clear();
            foreach (var dichVu in (await _api.GetServicesAsync()).Where(x => x.HoatDong))
                DichVu.Add(dichVu);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    /// <summary>Ghi một gói dịch vụ cho khách đang ở: mỗi dịch vụ trong gói thành một dòng sử dụng, tổng đúng bằng giá gói.</summary>
    [RelayCommand]
    private async Task ThemGoiAsync()
    {
        if (DangThemGoi) return;
        if (LuotDaChon is null || GoiDaChon is null) { DatThongBao("Vui lòng chọn khách đang ở và gói dịch vụ.", true); return; }
        if (SoLuongGoi is < 1 or > 999) { DatThongBao("Số lượng gói phải từ 1 đến 999.", true); return; }

        var noiDung = $"Ghi gói cho: {LuotDaChon.KhachHienThi} · phòng {LuotDaChon.MaPhong}\nGói: {GoiDaChon.TenGoi} ({GoiDaChon.NoiDungTomTat})\n{TinhTienGoi}\n\nThao tác được ghi nhật ký (ai, lúc nào). Xác nhận?";
        if (!HopThoai.XacNhan("Xác nhận ghi gói dịch vụ", noiDung)) return;

        DangThemGoi = true;
        try
        {
            var dong = await _api.GhiGoiDichVuAsync(LuotDaChon.MaPhieuNhan, LuotDaChon.MaPhong, GoiDaChon.MaGoi, SoLuongGoi);
            var ten = GoiDaChon.TenGoi; var sl = SoLuongGoi;
            await TaiDaDungAsync();
            SoLuongGoi = 1;
            DatThongBao($"Đã ghi {sl} gói \"{ten}\": {dong.Sum(x => x.ThanhTien):N0}đ cho phòng {LuotDaChon.MaPhong} ({LuotDaChon.KhachHienThi}). Tổng phòng này: {TongCong:N0}đ.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiAsync();
        }
        finally
        {
            DangThemGoi = false;
        }
    }

    [RelayCommand]
    private async Task HuyDongAsync()
    {
        if (DangThem) return;
        if (LuotDaChon is null || DongDaChon is null) { DatThongBao("Chọn dòng dịch vụ cần hủy ở bảng bên phải.", true); return; }
        if (!DongDaChon.CoTheHuy) { DatThongBao(DongDaChon.LyDoKhongHuy ?? "Dòng này không hủy được.", true); return; }
        if (string.IsNullOrWhiteSpace(LyDoHuyDong)) { DatThongBao("Cần nhập lý do hủy dòng dịch vụ (ghi nhầm dịch vụ / số lượng...).", true); return; }
        var dong = DongDaChon;
        if (!HopThoai.XacNhan("Xác nhận hủy dòng dịch vụ",
                $"Hủy dòng \"{dong.TenDV}\" (SL {dong.SoLuong}, {dong.ThanhTien:N0}đ, ghi lúc {dong.ThoiGianHienThi} bởi {dong.NguoiGhiHienThi}) khỏi phòng {LuotDaChon.MaPhong} của {LuotDaChon.KhachHienThi}?\n\nLý do: {LyDoHuyDong.Trim()}\n\nChỉ dùng khi ghi nhầm; số tiền này sẽ không còn tính cho khách. Nhật ký lưu người hủy, lý do và nội dung dòng bị hủy."))
            return;

        DangThem = true;
        try
        {
            await _api.HuyDongDichVuAsync(LuotDaChon.MaPhieuNhan, LuotDaChon.MaPhong, dong.MaChiTietDV, LyDoHuyDong.Trim());
            LyDoHuyDong = "";
            await TaiDaDungAsync();
            DatThongBao($"Đã hủy dòng \"{dong.TenDV}\" ({dong.ThanhTien:N0}đ). Tổng phòng này còn {TongCong:N0}đ.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiDaDungAsync();
        }
        finally
        {
            DangThem = false;
        }
    }

    private async Task TaiDaDungAsync()
    {
        if (LuotDaChon is null)
            return;

        var luot = LuotDaChon; var lan = ++_lanTai;
        try
        {
            var danhSach = await _api.GetDichVuSuDungAsync(luot.MaPhieuNhan, luot.MaPhong);
            var lichSu = await _api.GetLichSuLuuTruAsync(luot.MaPhieuNhan, luot.MaPhong);
            if (lan != _lanTai) return;   // người dùng đã chọn khách khác trong lúc chờ

            _tatCaDong = danhSach.OrderByDescending(x => x.NgaySuDung).ToList();
            DaDung.Clear();
            // Dòng của phòng đang chọn; dòng cũ chưa biết phòng (null) vẫn hiện để không sót.
            foreach (var d in _tatCaDong.Where(x => x.MaPhong is null || x.MaPhong == luot.MaPhong)) DaDung.Add(d);
            TongCong = DaDung.Sum(x => x.ThanhTien);
            TongCaPhieu = _tatCaDong.Sum(x => x.ThanhTien);
            OnPropertyChanged(nameof(TongHienThi));

            NhatKyDichVu.Clear();
            foreach (var n in lichSu.Where(x => x.HanhDong.Contains("DICH_VU", StringComparison.Ordinal))) NhatKyDichVu.Add(n);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }
}
