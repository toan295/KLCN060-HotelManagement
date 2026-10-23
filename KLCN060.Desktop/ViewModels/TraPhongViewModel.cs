using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

public partial class TraPhongViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private List<LuotLuuTruDto> _tatCaLuot = [];
    private int _lanTai;

    /// <summary>Báo cho màn hình chính mở màn Thanh toán với hóa đơn vừa lập (chỉ khi lễ tân bấm "Thu tiền").</summary>
    public event Action<string>? HoaDonDaTao;

    public ObservableCollection<LuotLuuTruDto> Luot { get; } = [];

    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private LuotLuuTruDto? luotDaChon;
    [ObservableProperty] private ThongTinTraPhongDto? thongTin;
    [ObservableProperty] private bool apDungPhuThuTreGio;
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;
    // Chặn bấm đúp "Xác nhận trả phòng" (mỗi lần bấm là một yêu cầu trả phòng/lập hóa đơn).
    [ObservableProperty] private bool dangTra;
    [ObservableProperty] private bool dangTai;
    [ObservableProperty] private string ketQua = "";
    [ObservableProperty] private string? maHoaDonVuaLap;

    public bool CoThongTin => ThongTin is not null;
    public bool GoiYTimHien => string.IsNullOrEmpty(TuKhoa);
    public bool KhongCoLuot => Luot.Count == 0;
    public bool CoKetQua => !string.IsNullOrEmpty(KetQua);
    public bool CoHoaDonVuaLap => !string.IsNullOrEmpty(MaHoaDonVuaLap);
    public bool CoCanhBao => ThongTin is { CanhBao.Count: > 0 };
    public bool KhongTraDuoc => ThongTin is { DuocTra: false };
    public bool TraSanSang => !DangTra && ThongTin is { DuocTra: true };
    public bool CoGiamGia => ThongTin is { TongHopFolio.TongKhuyenMai: > 0 };
    public bool CoCoc => ThongTin is { TongHopFolio.TienDaCoc: > 0 };
    public string DangChonHienThi => LuotDaChon is null ? "Chọn phòng khách đang trả ở bảng phía trên." : ThongTin?.DoiTuongHienThi ?? "";

    /// <summary>Mở sẵn lượt lưu trú (từ Tổng quan điều hành): chọn đúng phòng và tải folio để lễ tân kiểm tra tiền rồi trả phòng.</summary>
    public TraPhongViewModel(ApiClient api, string? maPhieuNhan = null, string? maPhong = null)
    {
        _api = api;
        _ = TaiVaChonAsync(maPhieuNhan, maPhong);
    }

    private void DatThongBao(string noiDung, bool laLoi) { ThongBao = noiDung; ThongBaoLaLoi = laLoi; }

    private async Task TaiVaChonAsync(string? maPhieuNhan, string? maPhong)
    {
        await TaiAsync();
        if (maPhieuNhan is null || maPhong is null) return;
        TuKhoa = "";
        var luot = Luot.FirstOrDefault(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong);
        if (luot is null) { DatThongBao($"Phòng {maPhong} không còn ở trạng thái đang ở (có thể đã được trả).", true); return; }
        LuotDaChon = luot;
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

    [RelayCommand]
    private async Task TaiAsync()
    {
        try
        {
            _tatCaLuot = (await _api.GetLuotDangOAsync()).OrderBy(x => x.MaPhong, StringComparer.Ordinal).ToList();
            LocLuot();
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    partial void OnLuotDaChonChanged(LuotLuuTruDto? value)
    {
        ApDungPhuThuTreGio = false;
        ThongTin = null;
        OnPropertyChanged(nameof(DangChonHienThi));
        if (value is not null) _ = XemFolioAsync();
    }

    partial void OnApDungPhuThuTreGioChanged(bool value) { if (LuotDaChon is not null) _ = XemFolioAsync(); }
    partial void OnDangTraChanged(bool value) => OnPropertyChanged(nameof(TraSanSang));
    partial void OnKetQuaChanged(string value) => OnPropertyChanged(nameof(CoKetQua));
    partial void OnMaHoaDonVuaLapChanged(string? value) => OnPropertyChanged(nameof(CoHoaDonVuaLap));
    partial void OnThongTinChanged(ThongTinTraPhongDto? value)
    {
        OnPropertyChanged(nameof(CoThongTin)); OnPropertyChanged(nameof(CoCanhBao)); OnPropertyChanged(nameof(KhongTraDuoc)); OnPropertyChanged(nameof(TraSanSang));
        OnPropertyChanged(nameof(CoGiamGia)); OnPropertyChanged(nameof(CoCoc)); OnPropertyChanged(nameof(DangChonHienThi));
    }

    /// <summary>Tải lại thông tin trả phòng + folio mới nhất (máy chủ tính ngay lúc này); chỉ kết quả của lần tải cuối được hiển thị.</summary>
    [RelayCommand]
    private async Task XemFolioAsync()
    {
        if (LuotDaChon is null)
        {
            DatThongBao("Vui lòng chọn phòng cần trả.", true);
            return;
        }

        var luot = LuotDaChon; var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var tt = await _api.GetThongTinTraPhongAsync(luot.MaPhieuNhan, luot.MaPhong, ApDungPhuThuTreGio);
            if (lan != _lanTai) return;
            ThongTin = tt;
            KetQua = ""; MaHoaDonVuaLap = null;
        }
        catch (LoiYeuCauApi loi)
        {
            if (lan == _lanTai) { ThongTin = null; DatThongBao(loi.Message, true); }
        }
        finally { if (lan == _lanTai) DangTai = false; }
    }

    [RelayCommand]
    private async Task TraPhongAsync()
    {
        if (DangTra) return;
        if (LuotDaChon is null || ThongTin is null)
        {
            DatThongBao("Vui lòng chọn phòng cần trả.", true);
            return;
        }
        if (!ThongTin.DuocTra)
        {
            DatThongBao($"Chưa trả phòng được: {ThongTin.LyDoKhongTra}", true);
            return;
        }

        var tt = ThongTin; var f = tt.TongHopFolio;
        var noiDung = $"Khách: {tt.DoiTuongHienThi}\n{tt.MaHienThi}\n{tt.NgayHienThi}\n\n" +
                      $"Tiền phòng {f.TienPhong:N0}đ + phụ thu {f.PhuThu:N0}đ + dịch vụ {f.TienDichVu:N0}đ = tổng {f.TongCong:N0}đ\n" +
                      (f.TongKhuyenMai > 0 ? $"Khuyến mãi −{f.TongKhuyenMai:N0}đ\n" : "") + (f.TienDaCoc > 0 ? $"Đã cọc −{f.TienDaCoc:N0}đ\n" : "") +
                      $"CÒN PHẢI THU: {f.ConPhaiThu:N0}đ\n\n{tt.PhamViHoaDon}\n" +
                      (ApDungPhuThuTreGio ? $"Có tính phụ thu trễ giờ {tt.PhuThuTreGioApDung:N0}đ.\n" : "") +
                      "\nPhòng sẽ chuyển sang \"Chưa dọn\" cho buồng phòng. Thao tác được ghi nhật ký (ai, lúc nào) và không hoàn tác được. Xác nhận trả phòng?";
        if (!HopThoai.XacNhan("Xác nhận trả phòng", noiDung)) return;

        DangTra = true;
        try
        {
            var kq = await _api.TraPhongAsync(LuotDaChon.MaPhieuNhan, LuotDaChon.MaPhong, ApDungPhuThuTreGio, f.ConPhaiThu);
            var dong1 = $"✔ Đã trả phòng {kq.MaPhong} của {tt.HoTenKhach ?? "khách"} lúc {kq.ThoiGianTra:HH:mm dd/MM/yyyy} · người thực hiện: {kq.NguoiThucHien ?? AppSession.Current.TenDangNhap} · ở {kq.SoDemO} đêm";
            var dong2 = kq.MaHoaDon is null
                ? $"Hóa đơn CHƯA lập: phiếu nhận còn {kq.SoPhongConDangO} phòng khác đang ở (hóa đơn lập khi trả phòng cuối)."
                : $"Hóa đơn {kq.MaHoaDon}: tổng {kq.TongHopFolio.TongCong:N0}đ, còn phải thu {kq.TongHopFolio.ConPhaiThu:N0}đ" + (kq.TrangThaiHoaDon == "DA_THANH_TOAN" ? " - ĐÃ THANH TOÁN (không còn gì phải thu)." : " - CHƯA thanh toán.");
            var dong3 = $"Phòng {kq.MaPhong} → Chưa dọn (đã báo buồng phòng)." + (kq.TrangThaiPhieuDat == "DA_TRA_PHONG" ? " Phiếu đặt → Đã trả phòng." : "");
            KetQua = $"{dong1}\n{dong2}\n{dong3}";
            MaHoaDonVuaLap = kq.MaHoaDon;
            DatThongBao(kq.MaHoaDon is null ? $"Đã trả phòng {kq.MaPhong} thành công." : $"Đã trả phòng {kq.MaPhong} thành công, hóa đơn {kq.MaHoaDon}.", false);
            await TaiAsync();
            ThongTin = null;
            LuotDaChon = null;
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
            // Có thể lần gọi trước đã thành công (mất mạng sau khi gửi) hoặc folio vừa đổi: tải lại để không đoán.
            var maPhieuNhan = LuotDaChon.MaPhieuNhan; var maPhong = LuotDaChon.MaPhong;
            await TaiAsync();
            if (_tatCaLuot.All(x => !(x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)))
            {
                DatThongBao($"Phòng {maPhong} không còn ở trạng thái đang ở - có thể đã được trả (kể cả ở lần bấm trước). Kiểm tra ở màn Hóa đơn trước khi thử lại. Chi tiết lỗi: {loi.Message}", true);
                ThongTin = null; LuotDaChon = null;
            }
            else await XemFolioAsync();
        }
        finally
        {
            DangTra = false;
        }
    }

    /// <summary>Sang màn Thanh toán với hóa đơn vừa lập (không tự chuyển để lễ tân đọc được kết quả trả phòng).</summary>
    [RelayCommand]
    private void ThuTien()
    {
        if (MaHoaDonVuaLap is { } ma) HoaDonDaTao?.Invoke(ma);
    }
}
