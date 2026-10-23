using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;
using Microsoft.Win32;

namespace KLCN060.Desktop.ViewModels;

public partial class SaoLuuViewModel : ObservableObject
{
    private readonly ApiClient _api;

    public ObservableCollection<LichSuSaoLuuDto> LichSu { get; } = [];

    [ObservableProperty] private string ketQua = "";
    [ObservableProperty] private bool ketQuaLaLoi;
    // Chặn bấm đúp "Sao lưu ngay" - bấm đúp tạo thêm 1 file .bak dư không cần thiết.
    [ObservableProperty] private bool dangSaoLuu;

    // Thư mục lưu bản sao lưu do người dùng chọn; để trống = thư mục mặc định do API/SQL Server quyết định.
    [ObservableProperty] private string thuMucLuu = "";
    [ObservableProperty] private string thuMucMacDinh = "";
    [ObservableProperty] private string goiYThuMuc = "";

    // Tình trạng sao lưu (lần cuối, số bản, kết quả kiểm tra gần nhất, cảnh báo quá lâu chưa sao lưu)
    [ObservableProperty] private TinhTrangSaoLuuDto? tinhTrang;
    public string TinhTrangTomTat => TinhTrang?.TomTat ?? "";
    public string TinhTrangChiTiet => TinhTrang is null ? "" :
        $"Tổng số bản sao lưu thành công: {TinhTrang.TongSoBanSaoLuuThanhCong}"
        + (TinhTrang.LanSaoLuuThanhCongCuoi is { } l ? $" · gần nhất {l:dd/MM/yyyy HH:mm}" : "")
        + (TinhTrang.LanKiemTraCuoi is { } k ? $"\nKiểm tra gần nhất {k:dd/MM/yyyy HH:mm}: {(TinhTrang.LanKiemTraCuoiHopLe == true ? "toàn vẹn" : "KHÔNG hợp lệ")}" : "\nChưa kiểm tra bản sao lưu nào")
        + (TinhTrang.LanPhucHoiCuoi is { } p ? $"\nPhục hồi gần nhất: {p:dd/MM/yyyy HH:mm}" : "");

    /// <summary>Bản sao lưu gần nhất: thời điểm, kết quả, đã kiểm tra chưa, dung lượng, còn file không.</summary>
    public string BanGanNhatText
    {
        get
        {
            var t = TinhTrang; if (t is null) return "";
            if (t.LanSaoLuuThanhCongCuoi is null) return "Chưa có bản sao lưu thành công nào.";
            var kt = t.BanCuoiDaKiemTra == true ? "đã kiểm tra toàn vẹn: ĐẠT" : t.BanCuoiDaKiemTra == false ? "kiểm tra toàn vẹn: KHÔNG ĐẠT" : "CHƯA kiểm tra toàn vẹn";
            return $"{t.LanSaoLuuThanhCongCuoi:dd/MM/yyyy HH:mm} (thành công) · {kt}"
                + (t.BanCuoiKichThuocBytes is { } b ? $" · {SaoLuuDinhDang.Dung(b)}" : "")
                + (t.BanCuoiConFile ? " · file còn đủ" : " · FILE ĐÃ MẤT")
                + (t.LanSaoLuuCuoiThatBai ? $"\nLần thử gần nhất THẤT BẠI lúc {t.LanSaoLuuCuoi:dd/MM/yyyy HH:mm}: {t.LoiLanSaoLuuCuoi}" : "");
        }
    }

    public string TuDongText
    {
        get
        {
            var t = TinhTrang; if (t is null) return "";
            if (!t.TuDongBat) return "ĐANG TẮT - dữ liệu chỉ được bảo vệ khi có người bấm sao lưu. Bật ở mục \"Sao lưu tự động\" bên dưới.";
            return $"BẬT, mỗi {t.ChuKyTuDongGio} giờ" + (t.HanTuDongKeTiep is { } han ? $" · hạn kế tiếp {han:dd/MM/yyyy HH:mm}" : "")
                + (t.LanTuDongThanhCongCuoi is { } tc ? $" · lần tự động thành công gần nhất {tc:dd/MM HH:mm}" : " · chưa có lần tự động nào thành công")
                + (t.LanTuDongThatBaiCuoi is { } tb ? $" · lần thất bại gần nhất {tb:dd/MM HH:mm}" : "")
                + (t.TuDongQuaHan ? "\nĐÃ QUÁ HẠN so với chu kỳ - máy chủ API có thể đang tắt hoặc lần chạy gần nhất thất bại; hãy kiểm tra rồi bấm \"Sao lưu ngay\"." : "");
        }
    }

    public string DungLuongText => TinhTrang is null ? "" :
        (TinhTrang.GhiChuDungLuong ?? "")
        + (TinhTrang.KichThuocCSDLBytes is { } d ? $"\nDữ liệu hiện tại: {SaoLuuDinhDang.Dung(d)}" : "")
        + (TinhTrang.TongDungLuongCacBanBytes is { } tong && tong > 0 ? $" · các bản sao lưu còn file chiếm {SaoLuuDinhDang.Dung(tong)}" : "")
        + (string.IsNullOrEmpty(TinhTrang.ThuMucSaoLuu) ? "" : $"\nNơi lưu: {TinhTrang.ThuMucSaoLuu}");
    public bool DungLuongThap => TinhTrang?.MucDungLuong == "THAP";

    public string PhucHoiKetLuan => TinhTrang?.KhaNangPhucHoi.KetLuan ?? "";
    public string PhucHoiMucDoText => TinhTrang?.KhaNangPhucHoi.MucDo switch { "SAN_SANG" => "SẴN SÀNG PHỤC HỒI", "RUI_RO" => "CÓ RỦI RO", "KHONG_CO" => "KHÔNG THỂ PHỤC HỒI", _ => "" };
    public bool PhucHoiSanSang => TinhTrang?.KhaNangPhucHoi.MucDo == "SAN_SANG";
    public string CoCheBaoVeText => TinhTrang is null ? "" : string.Join("\n", TinhTrang.KhaNangPhucHoi.CoCheBaoVe.Select(x => "✔ " + x));
    public string CanhBaoText => TinhTrang is null ? "" : string.Join("\n", TinhTrang.CanhBao.Select(x => "⚠ " + x));
    public bool CoCanhBao => TinhTrang is { CanhBao.Count: > 0 };
    public string CapNhatLucText => TinhTrang is null ? "" : $"Cập nhật {TinhTrang.CapNhatLuc:HH:mm:ss}" + (TinhTrang.TenCSDL.Length > 0 ? $" · CSDL {TinhTrang.TenCSDL}" : "");
    public bool TinhTrangTot => TinhTrang?.TinhTrang == "TOT";
    partial void OnTinhTrangChanged(TinhTrangSaoLuuDto? value)
    {
        OnPropertyChanged(nameof(TinhTrangTomTat));
        OnPropertyChanged(nameof(TinhTrangChiTiet));
        OnPropertyChanged(nameof(TinhTrangTot));
        foreach (var n in new[] { nameof(BanGanNhatText), nameof(TuDongText), nameof(DungLuongText), nameof(DungLuongThap), nameof(PhucHoiKetLuan), nameof(PhucHoiMucDoText),
                     nameof(PhucHoiSanSang), nameof(CoCheBaoVeText), nameof(CanhBaoText), nameof(CoCanhBao), nameof(CapNhatLucText) })
            OnPropertyChanged(n);
    }

    public SaoLuuViewModel(ApiClient api)
    {
        _api = api;
        thuMucLuu = DocCaiDat().ThuMucSaoLuu ?? "";   // gán field (không qua property) để khỏi kích hoạt thông báo khi mới khởi tạo
        CapNhatGoiY();
        _ = KhoiTaoAsync();
    }

    partial void OnThuMucLuuChanged(string value) => CapNhatGoiY();
    partial void OnThuMucMacDinhChanged(string value) => CapNhatGoiY();

    private void CapNhatGoiY()
    {
        GoiYThuMuc = string.IsNullOrWhiteSpace(ThuMucLuu)
            ? $"Để trống: lưu vào thư mục mặc định của SQL Server{(string.IsNullOrEmpty(ThuMucMacDinh) ? "" : $" ({ThuMucMacDinh})")}."
            : $"Bản sao lưu sẽ được lưu vào: {ThuMucLuu.Trim()} (đường dẫn tính trên máy đang chạy API; thư mục chưa có sẽ được tạo tự động).";
    }

    private async Task KhoiTaoAsync()
    {
        try { ThuMucMacDinh = (await _api.GetCaiDatSaoLuuAsync()).ThuMucMacDinh; }
        catch (LoiYeuCauApi) { /* chỉ để gợi ý - không có cũng không sao */ }
        await TaiLichTuDongAsync();
        await TaiLichSuAsync();
    }

    // ===== Bật / tắt sao lưu tự động ngay trên màn hình (không phải sửa cấu hình API) =====
    public IReadOnlyList<LuaChon> ChuKyTuyChon { get; } =
    [
        new("1", "Mỗi 1 giờ"), new("6", "Mỗi 6 giờ"), new("12", "Mỗi 12 giờ"), new("24", "Mỗi 24 giờ (hằng ngày)"), new("48", "Mỗi 2 ngày"), new("72", "Mỗi 3 ngày"), new("168", "Mỗi tuần")
    ];
    [ObservableProperty] private bool tuDongBatChon;
    [ObservableProperty] private string chuKyChon = "24";
    private bool _tuDongBatDaLuu; private string _chuKyDaLuu = "24";
    public bool CoThayDoiLich => TuDongBatChon != _tuDongBatDaLuu || (TuDongBatChon && ChuKyChon != _chuKyDaLuu);
    public string GhiChuLich => TuDongBatChon
        ? $"Khi bật: cứ {ChuKyTuyChon.FirstOrDefault(x => x.Ma == ChuKyChon)?.Ten.ToLowerInvariant() ?? ChuKyChon + " giờ"}, nếu bản sao lưu thành công gần nhất đã quá chu kỳ thì hệ thống tự sao lưu (máy chủ kiểm tra khoảng 10 phút một lần), không cần mở ứng dụng nhưng máy chủ API phải đang chạy. Bản tự động nằm trong lịch sử bên dưới."
        : "Đang tắt: hệ thống không tự sao lưu. Bật để dữ liệu luôn có bản sao lưu gần đây ngay cả khi không ai bấm \"Sao lưu ngay\".";
    partial void OnTuDongBatChonChanged(bool value) { OnPropertyChanged(nameof(CoThayDoiLich)); OnPropertyChanged(nameof(GhiChuLich)); }
    partial void OnChuKyChonChanged(string value) { OnPropertyChanged(nameof(CoThayDoiLich)); OnPropertyChanged(nameof(GhiChuLich)); }

    private async Task TaiLichTuDongAsync()
    {
        try
        {
            var l = await _api.GetLichSaoLuuTuDongAsync();
            _tuDongBatDaLuu = l.TuDongBat; _chuKyDaLuu = l.ChuKyGio.ToString();
            TuDongBatChon = l.TuDongBat;
            ChuKyChon = ChuKyTuyChon.Any(x => x.Ma == _chuKyDaLuu) ? _chuKyDaLuu : "24";
            OnPropertyChanged(nameof(CoThayDoiLich));
        }
        catch (LoiYeuCauApi) { /* chỉ là thông tin phụ */ }
        catch (HttpRequestException) { }
    }

    [RelayCommand]
    private async Task LuuLichTuDongAsync()
    {
        if (!CoThayDoiLich) { DatKetQua("Cài đặt sao lưu tự động chưa thay đổi.", false); return; }
        try
        {
            var l = await _api.DatLichSaoLuuTuDongAsync(TuDongBatChon, int.Parse(ChuKyChon));
            _tuDongBatDaLuu = l.TuDongBat; _chuKyDaLuu = l.ChuKyGio.ToString();
            OnPropertyChanged(nameof(CoThayDoiLich));
            DatKetQua(l.TuDongBat ? $"Đã BẬT sao lưu tự động, chu kỳ {l.ChuKyGio} giờ. Bản đầu tiên sẽ chạy trong khoảng {Math.Max(l.KiemTraMoiPhut, 1)} phút nếu chưa có bản nào trong chu kỳ." : "Đã TẮT sao lưu tự động.", false);
            await TaiTinhTrangAsync();
        }
        catch (LoiYeuCauApi loi) { DatKetQua(loi.Message, true); }
    }

    private async Task TaiTinhTrangAsync()
    {
        try { TinhTrang = await _api.GetTinhTrangSaoLuuAsync(); }
        catch (LoiYeuCauApi) { /* chỉ là thông tin phụ */ }
        catch (HttpRequestException) { }
    }

    /// <summary>Kiểm tra toàn vẹn (RESTORE VERIFYONLY) một bản sao lưu trong lịch sử - không đụng vào dữ liệu đang chạy.</summary>
    [RelayCommand]
    private async Task KiemTraAsync(LichSuSaoLuuDto? banSaoLuu)
    {
        if (banSaoLuu is null || banSaoLuu.LoaiThaoTac != "SAO_LUU" || banSaoLuu.KetQua != "THANH_CONG") return;
        try
        {
            var kq = await _api.KiemTraSaoLuuAsync(banSaoLuu.MaLichSu);
            DatKetQua(kq.HopLe ? $"Bản sao lưu #{banSaoLuu.MaLichSu}: {kq.ThongBao}" : $"Bản sao lưu #{banSaoLuu.MaLichSu}: {kq.ThongBao}", !kq.HopLe);
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
    }

    /// <summary>Kiểm tra toàn vẹn một file .bak bất kỳ (VD file chép từ máy khác) trước khi quyết định phục hồi.</summary>
    [RelayCommand]
    private async Task KiemTraFileAsync()
    {
        var hopThoai = new OpenFileDialog { Title = "Chọn file sao lưu để kiểm tra", Filter = "Bản sao lưu SQL Server (*.bak)|*.bak", CheckFileExists = true };
        if (!string.IsNullOrWhiteSpace(ThuMucLuu) && Directory.Exists(ThuMucLuu.Trim())) hopThoai.InitialDirectory = ThuMucLuu.Trim();
        if (hopThoai.ShowDialog() != true) return;
        try
        {
            var kq = await _api.KiemTraFileSaoLuuAsync(hopThoai.FileName);
            DatKetQua($"{hopThoai.FileName}: {kq.ThongBao}", !kq.HopLe);
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
    }

    [RelayCommand]
    private void ChonThuMuc()
    {
        var hopThoai = new OpenFolderDialog { Title = "Chọn thư mục lưu bản sao lưu" };
        if (!string.IsNullOrWhiteSpace(ThuMucLuu) && Directory.Exists(ThuMucLuu.Trim()))
            hopThoai.InitialDirectory = ThuMucLuu.Trim();

        if (hopThoai.ShowDialog() != true) return;

        ThuMucLuu = hopThoai.FolderName;
        LuuCaiDat();
    }

    [RelayCommand]
    private void DungMacDinh()
    {
        ThuMucLuu = "";
        LuuCaiDat();
    }

    [RelayCommand]
    private async Task SaoLuuNgayAsync()
    {
        if (DangSaoLuu) return;

        DangSaoLuu = true;
        try
        {
            var thuMuc = string.IsNullOrWhiteSpace(ThuMucLuu) ? null : ThuMucLuu.Trim();
            var banSaoLuu = await _api.SaoLuuAsync(thuMuc);
            LuuCaiDat();
            DatKetQua($"Sao lưu thành công lúc {banSaoLuu.ThoiGianThucHien:dd/MM/yyyy HH:mm}. File: {banSaoLuu.DuongDanFile}", false);
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
        finally
        {
            DangSaoLuu = false;
        }
    }

    // ===== Phục hồi: xem trước (kiểm tra + dữ liệu sẽ mất) -> nhập lý do + gõ lại tên CSDL -> phục hồi -> kết quả kiểm tra sau phục hồi =====
    [ObservableProperty] private bool dangPhucHoi;
    [ObservableProperty] private XemTruocPhucHoiDto? xemTruoc;
    [ObservableProperty] private string lyDoPhucHoi = "";
    [ObservableProperty] private string xacNhanTenCsdl = "";
    [ObservableProperty] private bool boQuaBanAnToan;
    [ObservableProperty] private bool canBoQuaBanAnToan;
    [ObservableProperty] private bool dangThucHienPhucHoi;
    private int? _maBanPhucHoi;
    private string? _filePhucHoi;

    public string XemTruocTieuDe => XemTruoc is null ? "" : $"Phục hồi vào CSDL \"{XemTruoc.TenCSDL}\"";
    public string XemTruocChiTiet => XemTruoc is null ? "" :
        $"Nguồn: {XemTruoc.DuongDanFile}\n{XemTruoc.KiemTra.ThongBao}"
        + (XemTruoc.ThoiDiemBanSaoLuu is { } t ? $"\nBản sao lưu lúc: {t:dd/MM/yyyy HH:mm}" : "")
        + (XemTruoc.KiemTra.KichThuocBytes is { } k ? $" · dung lượng {SaoLuuDinhDang.Dung(k)}" : "");
    public string XemTruocCanhBao => XemTruoc is null ? "" : string.Join("\n", XemTruoc.CanhBao.Select(x => "⚠ " + x));
    public bool DuDieuKienPhucHoi => XemTruoc is { SanSang: true } && !DangThucHienPhucHoi && !string.IsNullOrWhiteSpace(LyDoPhucHoi)
        && string.Equals(XacNhanTenCsdl.Trim(), XemTruoc.TenCSDL, StringComparison.OrdinalIgnoreCase);
    partial void OnXemTruocChanged(XemTruocPhucHoiDto? value) { OnPropertyChanged(nameof(XemTruocTieuDe)); OnPropertyChanged(nameof(XemTruocChiTiet)); OnPropertyChanged(nameof(XemTruocCanhBao)); OnPropertyChanged(nameof(DuDieuKienPhucHoi)); }
    partial void OnLyDoPhucHoiChanged(string value) => OnPropertyChanged(nameof(DuDieuKienPhucHoi));
    partial void OnXacNhanTenCsdlChanged(string value) => OnPropertyChanged(nameof(DuDieuKienPhucHoi));
    partial void OnDangThucHienPhucHoiChanged(bool value) => OnPropertyChanged(nameof(DuDieuKienPhucHoi));

    [RelayCommand]
    private async Task PhucHoiAsync(LichSuSaoLuuDto? banSaoLuu)
    {
        if (banSaoLuu is null || !banSaoLuu.LaBanSaoLuuDungDuoc) return;
        try
        {
            var xt = await _api.XemTruocPhucHoiAsync(banSaoLuu.MaLichSu);
            MoHopPhucHoi(xt, banSaoLuu.MaLichSu, null);
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
    }

    /// <summary>Phục hồi từ một file .bak bất kỳ (VD file chép từ máy khác, hoặc bản cũ không còn trong lịch sử).</summary>
    [RelayCommand]
    private async Task PhucHoiTuFileAsync()
    {
        var hopThoai = new OpenFileDialog
        {
            Title = "Chọn file sao lưu để phục hồi",
            Filter = "Bản sao lưu SQL Server (*.bak)|*.bak",
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(ThuMucLuu) && Directory.Exists(ThuMucLuu.Trim()))
            hopThoai.InitialDirectory = ThuMucLuu.Trim();

        if (hopThoai.ShowDialog() != true) return;
        try
        {
            var xt = await _api.XemTruocPhucHoiFileAsync(hopThoai.FileName);
            MoHopPhucHoi(xt, null, hopThoai.FileName);
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
    }

    private void MoHopPhucHoi(XemTruocPhucHoiDto xt, int? maBan, string? file)
    {
        _maBanPhucHoi = maBan; _filePhucHoi = file;
        LyDoPhucHoi = ""; XacNhanTenCsdl = ""; BoQuaBanAnToan = false; CanBoQuaBanAnToan = false;
        XemTruoc = xt;
        DangPhucHoi = true;
    }

    [RelayCommand]
    private void HuyPhucHoi()
    {
        if (DangThucHienPhucHoi) return;
        DangPhucHoi = false; XemTruoc = null;
    }

    [RelayCommand]
    private async Task XacNhanPhucHoiAsync()
    {
        if (!DuDieuKienPhucHoi || XemTruoc is null) return;
        if (!HopThoai.XacNhan("Xác nhận phục hồi dữ liệu",
                $"Bạn sắp GHI ĐÈ toàn bộ dữ liệu của CSDL \"{XemTruoc.TenCSDL}\" bằng bản sao lưu lúc {XemTruoc.ThoiDiemBanSaoLuu:dd/MM/yyyy HH:mm}.\n\n"
                + $"Sẽ mất: {XemTruoc.SoThaoTacSeMat} thao tác, {XemTruoc.SoDatPhongMoiSeMat} phiếu đặt phòng mới, {XemTruoc.SoHoaDonMoiSeMat} hóa đơn phát sinh sau thời điểm đó.\n\nLý do: {LyDoPhucHoi.Trim()}\n\nTiếp tục?"))
            return;

        DangThucHienPhucHoi = true;
        try
        {
            var kq = _maBanPhucHoi is { } ma
                ? await _api.PhucHoiAsync(ma, LyDoPhucHoi.Trim(), XacNhanTenCsdl.Trim(), BoQuaBanAnToan)
                : await _api.PhucHoiTuFileAsync(_filePhucHoi!, LyDoPhucHoi.Trim(), XacNhanTenCsdl.Trim(), BoQuaBanAnToan);
            DangPhucHoi = false; XemTruoc = null;
            DatKetQua("Đã phục hồi dữ liệu. " + (kq.KiemTraSauPhucHoi ?? "") + " Hãy đăng nhập lại nếu bị ngắt phiên.", kq.DaKiemTraHopLe == false);
            await TaiLichSuAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            // Không chụp được bản an toàn: cho phép người dùng chủ động chấp nhận rủi ro (chỉ khi CSDL đang hỏng).
            if (loi.Message.Contains("bản sao lưu an toàn")) CanBoQuaBanAnToan = true;
            DatKetQua(loi.Message, true);
        }
        finally
        {
            DangThucHienPhucHoi = false;
        }
    }

    [RelayCommand]
    private Task TaiLaiAsync() => TaiLichSuAsync();

    /// <summary>Mở Windows Explorer tại file sao lưu (hoặc thư mục chứa nó nếu file không còn).</summary>
    [RelayCommand]
    private void MoViTri(LichSuSaoLuuDto? banGhi)
    {
        var duongDan = banGhi?.DuongDanFile;
        if (string.IsNullOrWhiteSpace(duongDan)) return;

        try
        {
            if (File.Exists(duongDan))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{duongDan}\"") { UseShellExecute = true });
            else if (Directory.Exists(Path.GetDirectoryName(duongDan)))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetDirectoryName(duongDan)}\"") { UseShellExecute = true });
            else
                DatKetQua($"Không tìm thấy file/thư mục: {duongDan}. Nếu API chạy trên máy khác thì vị trí này nằm trên máy đó.", true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            DatKetQua($"Không mở được vị trí file: {ex.Message}", true);
        }
    }

    private void DatKetQua(string noiDung, bool laLoi)
    {
        KetQua = noiDung;
        KetQuaLaLoi = laLoi;
    }

    private async Task TaiLichSuAsync()
    {
        try
        {
            LichSu.Clear();
            foreach (var banSaoLuu in await _api.GetLichSuSaoLuuAsync())
                LichSu.Add(banSaoLuu);
            await TaiTinhTrangAsync();
        }
        catch (LoiYeuCauApi loi)
        {
            DatKetQua(loi.Message, true);
        }
        catch (HttpRequestException loi)
        {
            DatKetQua($"Không kết nối được máy chủ: {loi.Message}", true);
        }
    }

    // Nhớ thư mục lần trước để lần sau mở màn hình không phải chọn lại. Chỉ là tiện ích: lỗi đọc/ghi file cài đặt thì bỏ qua.
    private sealed class CaiDatNguoiDung { public string? ThuMucSaoLuu { get; set; } }

    private static string DuongDanCaiDat => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KLCN060", "desktop-settings.json");

    private static CaiDatNguoiDung DocCaiDat()
    {
        try
        {
            return File.Exists(DuongDanCaiDat)
                ? JsonSerializer.Deserialize<CaiDatNguoiDung>(File.ReadAllText(DuongDanCaiDat)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    private void LuuCaiDat()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DuongDanCaiDat)!);
            File.WriteAllText(DuongDanCaiDat, JsonSerializer.Serialize(new CaiDatNguoiDung
            {
                ThuMucSaoLuu = string.IsNullOrWhiteSpace(ThuMucLuu) ? null : ThuMucLuu.Trim()
            }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
