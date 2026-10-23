using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class DoiPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private List<LuotLuuTruDto> _tatCaLuot = [];
    private int _lanXemTruoc;
    private int _lanXemTruocGiaHan;

    public ObservableCollection<LuotLuuTruDto> LuotDangO { get; } = [];
    public ObservableCollection<PhongDto> PhongTrong { get; } = [];
    public IReadOnlyList<string> LyDoThuongGap { get; } =
    [
        "Khách yêu cầu đổi phòng", "Phòng cũ có sự cố (điều hòa/nước/điện...)", "Phòng cũ ồn hoặc mùi khó chịu",
        "Khách muốn phòng rộng hơn / tầng cao hơn", "Khách đổi số người ở", "Sắp xếp lại phòng theo đoàn"
    ];

    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private PhongDto? phongMoiDaChon;
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private string? lyDoNhanh;
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    [ObservableProperty] private bool dangDoiPhong;
    [ObservableProperty] private XemTruocDoiPhongDto? xemTruoc;
    [ObservableProperty] private string ketQuaDoi = "";

    // Gia hạn lưu trú (extend stay): dời ngày trả dự kiến của lượt đang chọn; bị từ chối nếu phòng đã có đặt phòng khác trong những đêm thêm.
    [ObservableProperty] private DateTime? ngayGiaHan;
    [ObservableProperty] private string lyDoGiaHan = "";
    [ObservableProperty] private XemTruocGiaHanDto? xemTruocGiaHan;

    public bool CoLuotDaChon => LuotDaChon is not null;
    public bool GoiYTimHien => string.IsNullOrEmpty(TuKhoa);
    public bool KhongCoLuot => LuotDangO.Count == 0;
    public bool CoXemTruoc => XemTruoc is not null && XemTruoc.MaPhongMoi is not null;
    public bool KhongDoiDuoc => XemTruoc is { MaPhongMoi: not null, CoTheDoi: false };
    public bool CoCanhBao => XemTruoc is { CanhBao.Count: > 0 };
    public bool CoKetQuaDoi => !string.IsNullOrEmpty(KetQuaDoi);
    public bool CoXemTruocGiaHan => XemTruocGiaHan is not null;
    public bool NutDoiPhongSanSang => !DangDoiPhong && LuotDaChon is not null && XemTruoc is { CoTheDoi: true } && !string.IsNullOrWhiteSpace(GhiChu);
    public string LuotDaChonHienThi => LuotDaChon is null ? "" :
        $"{LuotDaChon.KhachHienThi} · phòng {LuotDaChon.MaPhong} ({LuotDaChon.TenLoai}) · phiếu nhận {LuotDaChon.MaPhieuNhan}" + (LuotDaChon.MaPhieuDat is null ? " (khách vãng lai)" : $" · phiếu đặt {LuotDaChon.MaPhieuDat}") +
        $"\nỞ từ {LuotDaChon.NgayNhanHienThi} đến {LuotDaChon.NgayTraHienThi} · {LuotDaChon.SoNguoi} người · {LuotDaChon.DonGiaHienThi}/đêm";

    public DoiPhongViewModel(ApiClient api, string? maPhieuNhan = null, string? maPhong = null)
    {
        _api = api;
        _ = TaiVaChonAsync(maPhieuNhan, maPhong);
    }

    private void DatThongBao(string noiDung, bool laLoi) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    private async Task TaiVaChonAsync(string? maPhieuNhan, string? maPhong)
    {
        await TaiDuLieuAsync();
        if (maPhieuNhan is null || maPhong is null) return;
        TuKhoa = "";
        LuotDaChon = LuotDangO.FirstOrDefault(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong);
        if (LuotDaChon is null) DatThongBao($"Phòng {maPhong} không còn ở trạng thái đang ở.", true);
    }

    partial void OnTuKhoaChanged(string value) { OnPropertyChanged(nameof(GoiYTimHien)); LocLuot(); }

    private static string BoDau(string s)
    {
        var d = (s ?? "").Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }

    private void LocLuot()
    {
        var chon = LuotDaChon;
        var tk = BoDau(TuKhoa.Trim());
        LuotDangO.Clear();
        foreach (var l in _tatCaLuot.Where(x => tk.Length == 0 || BoDau($"{x.MaPhong} {x.HoTenKhach} {x.MaPhieuNhan} {x.MaPhieuDat}").Contains(tk)))
            LuotDangO.Add(l);
        OnPropertyChanged(nameof(KhongCoLuot));
        LuotDaChon = chon is null ? null : LuotDangO.FirstOrDefault(x => x.MaPhieuNhan == chon.MaPhieuNhan && x.MaPhong == chon.MaPhong);
    }

    [RelayCommand]
    private async Task TaiDuLieuAsync()
    {
        try
        {
            _tatCaLuot = (await _api.GetLuotDangOAsync()).OrderBy(x => x.MaPhong, StringComparer.Ordinal).ToList();
            LocLuot();

            var danhSachPhong = await _api.GetRoomsAsync();
            PhongTrong.Clear();
            // Chỉ phòng bán được ngay: đã dọn/kiểm tra và chưa giữ cho khách sắp đến (máy chủ cũng kiểm tra lại cả những đêm còn lại).
            foreach (var phong in danhSachPhong.Where(x => x.BanDuocNgay))
                PhongTrong.Add(phong);

        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    partial void OnLuotDaChonChanged(LuotLuuTruDto? value)
    {
        OnPropertyChanged(nameof(CoLuotDaChon)); OnPropertyChanged(nameof(LuotDaChonHienThi));
        PhongMoiDaChon = null; XemTruoc = null; XemTruocGiaHan = null; NgayGiaHan = null; KetQuaDoi = "";
    }

    partial void OnPhongMoiDaChonChanged(PhongDto? value) { _ = TaiXemTruocAsync(); }
    partial void OnLyDoNhanhChanged(string? value) { if (!string.IsNullOrWhiteSpace(value)) GhiChu = value!; }
    partial void OnGhiChuChanged(string value) => OnPropertyChanged(nameof(NutDoiPhongSanSang));
    partial void OnDangDoiPhongChanged(bool value) => OnPropertyChanged(nameof(NutDoiPhongSanSang));
    partial void OnXemTruocChanged(XemTruocDoiPhongDto? value)
    {
        OnPropertyChanged(nameof(CoXemTruoc)); OnPropertyChanged(nameof(KhongDoiDuoc)); OnPropertyChanged(nameof(CoCanhBao)); OnPropertyChanged(nameof(NutDoiPhongSanSang));
    }
    partial void OnKetQuaDoiChanged(string value) => OnPropertyChanged(nameof(CoKetQuaDoi));
    partial void OnXemTruocGiaHanChanged(XemTruocGiaHanDto? value) => OnPropertyChanged(nameof(CoXemTruocGiaHan));
    partial void OnNgayGiaHanChanged(DateTime? value) => _ = TaiXemTruocGiaHanAsync();

    /// <summary>Máy chủ chạy đúng các bước kiểm tra của đổi phòng thật (không ghi gì) để lễ tân biết ngay: hạng, giá, chênh lệch, cần duyệt hay không, vì sao chưa đổi được.</summary>
    private async Task TaiXemTruocAsync()
    {
        var luot = LuotDaChon; var phong = PhongMoiDaChon;
        var lan = ++_lanXemTruoc;
        if (luot is null || phong is null) { XemTruoc = null; return; }
        try
        {
            var kq = await _api.XemTruocDoiPhongAsync(luot.MaPhieuNhan, luot.MaPhong, phong.MaPhong);
            if (lan == _lanXemTruoc) XemTruoc = kq;   // bỏ kết quả của lần chọn cũ
        }
        catch (LoiYeuCauApi e) { if (lan == _lanXemTruoc) { XemTruoc = null; DatThongBao(e.Message, true); } }
    }

    private async Task TaiXemTruocGiaHanAsync()
    {
        var luot = LuotDaChon; var lan = ++_lanXemTruocGiaHan;
        if (luot is null || NgayGiaHan is null) { XemTruocGiaHan = null; return; }
        try
        {
            var kq = await _api.XemTruocGiaHanAsync(luot.MaPhieuNhan, luot.MaPhong, DateOnly.FromDateTime(NgayGiaHan.Value));
            if (lan == _lanXemTruocGiaHan) XemTruocGiaHan = kq;
        }
        catch (LoiYeuCauApi e) { if (lan == _lanXemTruocGiaHan) { XemTruocGiaHan = null; DatThongBao(e.Message, true); } }
    }

    [RelayCommand]
    private async Task DoiPhongAsync()
    {
        if (DangDoiPhong) return;

        if (LuotDaChon is null || PhongMoiDaChon is null)
        {
            DatThongBao("Vui lòng chọn lượt lưu trú và phòng mới.", true);
            return;
        }
        if (XemTruoc is null || !XemTruoc.CoTheDoi)
        {
            DatThongBao(XemTruoc?.LyDoKhongDoi ?? "Chưa đổi được phòng này - xem lý do ở khung xem trước.", true);
            return;
        }
        if (string.IsNullOrWhiteSpace(GhiChu))
        {
            DatThongBao("Nhập lý do đổi phòng (chọn lý do thường gặp hoặc tự ghi) - bắt buộc, có ghi nhật ký.", true);
            return;
        }

        var xt = XemTruoc;
        var noiDung = $"Khách: {LuotDaChon.KhachHienThi} ({LuotDaChon.SoNguoi} người)\nĐổi: {xt.TuDenHienThi}\n\nLoại đổi: {xt.LoaiDoiHienThi}\n{xt.TienHienThi}\n\n" +
                      (xt.HuongDan is { Length: > 0 } ? xt.HuongDan + "\n\n" : "") + $"Lý do: {GhiChu.Trim()}\n\nPhòng cũ sẽ chuyển sang \"Chưa dọn\". Thao tác được ghi nhật ký (ai, lúc nào, từ đâu → đến đâu) và không hoàn tác được.";
        if (!HopThoai.XacNhan("Xác nhận đổi phòng", noiDung)) return;

        DangDoiPhong = true;
        try
        {
            var ketQua = await _api.DoiPhongAsync(
                LuotDaChon.MaPhieuNhan,
                LuotDaChon.MaPhong,
                new YeuCauDoiPhong(PhongMoiDaChon.MaPhong, GhiChu.Trim()));

            DatThongBao($"Đã đổi sang {ketQua.MaPhongMoi} ({ketQua.LoaiDoiHienThi}) thành công.", false);
            var maPhieuNhan = LuotDaChon.MaPhieuNhan;
            var tenKhach = LuotDaChon.KhachHienThi;
            GhiChu = ""; LyDoNhanh = null;
            await TaiDuLieuAsync();
            LuotDaChon = LuotDangO.FirstOrDefault(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == ketQua.MaPhongMoi);   // chuyển sang lượt ở mới của khách
            KetQuaDoi = $"✔ Đã đổi phòng cho {tenKhach}: {ketQua.MaPhongCu} → {ketQua.MaPhongMoi} ({ketQua.LoaiDoiHienThi}) — phiếu đổi {ketQua.MaPhieuDoi}\n" +
                        $"Lúc {ketQua.NgayDoi:HH:mm dd/MM/yyyy} · người thực hiện: {AppSession.Current.TenDangNhap} · chênh lệch {ketQua.ChenhLechGia:N0}đ/đêm" +
                        $"\nPhòng {ketQua.MaPhongCu} chuyển sang \"Chưa dọn\" (chờ buồng phòng), phòng {ketQua.MaPhongMoi} có khách.";
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiDuLieuAsync();       // phòng/phiếu có thể vừa đổi ở nơi khác → làm mới để không đổi theo dữ liệu cũ
            await TaiXemTruocAsync();
        }
        finally
        {
            DangDoiPhong = false;
        }
    }

    [RelayCommand]
    private async Task GiaHanAsync()
    {
        if (DangDoiPhong) return;
        if (LuotDaChon is null) { DatThongBao("Chọn lượt lưu trú cần gia hạn.", true); return; }
        if (NgayGiaHan is null) { DatThongBao("Chọn ngày trả mới.", true); return; }
        var ngayMoi = DateOnly.FromDateTime(NgayGiaHan.Value);
        if (ngayMoi <= DateOnly.FromDateTime(DateTime.Today)) { DatThongBao("Ngày trả mới phải sau hôm nay.", true); return; }
        if (LuotDaChon.NgayTraDuKien is { } hienTai && ngayMoi <= hienTai) { DatThongBao($"Ngày trả mới phải sau ngày trả dự kiến hiện tại ({hienTai:dd/MM/yyyy}).", true); return; }
        if (XemTruocGiaHan is { CoTheGiaHan: false }) { DatThongBao(XemTruocGiaHan.LyDoKhongGiaHan ?? "Chưa gia hạn được.", true); return; }

        var noiDung = $"Khách: {LuotDaChon.KhachHienThi} · phòng {LuotDaChon.MaPhong}\n{XemTruocGiaHan?.TomTat}\n\n" +
                      (string.IsNullOrWhiteSpace(LyDoGiaHan) ? "" : $"Lý do: {LyDoGiaHan.Trim()}\n\n") + "Thao tác được ghi nhật ký (ai, lúc nào, ngày trả cũ → mới). Xác nhận gia hạn?";
        if (!HopThoai.XacNhan("Xác nhận gia hạn", noiDung)) return;

        DangDoiPhong = true;
        try
        {
            var kq = await _api.GiaHanLuuTruAsync(LuotDaChon.MaPhieuNhan, LuotDaChon.MaPhong, ngayMoi, string.IsNullOrWhiteSpace(LyDoGiaHan) ? null : LyDoGiaHan.Trim());
            var phong = LuotDaChon.MaPhong; var maPhieuNhan = LuotDaChon.MaPhieuNhan;
            await TaiDuLieuAsync();
            LuotDaChon = LuotDangO.FirstOrDefault(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == phong);
            DatThongBao($"Đã gia hạn phòng {phong} tới {kq.NgayTraDuKienMoi:dd/MM/yyyy}: thêm {kq.SoDemThem} đêm, dự kiến thêm {kq.TienThemDuKien:N0}đ (người thực hiện: {AppSession.Current.TenDangNhap}).", false);
            LyDoGiaHan = "";
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            await TaiXemTruocGiaHanAsync();
        }
        finally
        {
            DangDoiPhong = false;
        }
    }
}
