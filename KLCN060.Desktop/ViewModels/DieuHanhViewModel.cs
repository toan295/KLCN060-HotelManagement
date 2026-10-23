using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D22 - Tổng quan điều hành (Quản lý): tình hình phòng, hoạt động hôm nay, doanh thu, công suất / ADR / RevPAR, công nợ và các CẢNH BÁO cần xử lý
/// (phòng bảo trì lâu, công nợ quá hạn, booking VIP, hoàn cọc lớn...). Mỗi cảnh báo nêu rõ: cái gì, tại sao, mức độ, ảnh hưởng, hạn khi nào, xử lý thế nào.
/// Danh sách cảnh báo có lọc theo mức độ và phân trang để luôn gọn dù có hàng trăm cảnh báo. Số liệu tự làm mới mỗi phút.
/// Số liệu lấy từ GET /reports/dashboard và /reports/alerts.
/// Với Kế toán đây là MÀN HÌNH ĐIỀU HÀNH DUY NHẤT của vai trò: chỉ nói chuyện tiền (doanh thu, công nợ, nợ quá hạn, việc chờ xử lý),
/// không có tình hình phòng / khách đến-đi / buồng phòng; cảnh báo chỉ gồm các cảnh báo tài chính mà kế toán xử lý được ngay từ thanh bên.
/// </summary>
public partial class DieuHanhViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private System.Windows.Threading.DispatcherTimer? _dongHo;

    /// <summary>Bấm "Mở" ở một cảnh báo: phát ra nhóm màn hình liên quan (PHONG, DAT_PHONG, HOA_DON, DOI_SOAT, SAO_LUU, NHAN_VIEN...).</summary>
    public event Action<string>? MoManHinh;

    /// <summary>Đã tải xong cảnh báo: (số nghiêm trọng, tổng). Chỉ để thông tin - khung chính đếm lại từ danh sách cảnh báo (một cách đếm duy nhất).</summary>
    public event Action<int, int>? DaTaiCanhBao;

    /// <summary>Danh sách cảnh báo vừa tải (khung chính so với lần trước để hiện thông báo nổi khi có cảnh báo mới).</summary>
    public event Action<IReadOnlyList<CanhBaoDto>>? DaTaiDanhSachCanhBao;

    public ObservableCollection<CanhBaoDto> CanhBaos { get; } = [];
    public ObservableCollection<CanhBaoDaXuLyDto> DaXuLy { get; } = [];
    public ObservableCollection<DhTangDto> Tangs { get; } = [];
    public ObservableCollection<DhViecDto> KhachDen { get; } = [];
    public ObservableCollection<DhViecDto> KhachTra { get; } = [];

    [ObservableProperty] private BangDieuHanhDto? bang;
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private bool dangTai;
    /// <summary>Lần tải gần nhất thất bại: màn hình giữ số liệu cũ và báo rõ là dữ liệu cũ.</summary>
    [ObservableProperty] private bool duLieuCu;
    [ObservableProperty] private int soQuaHan;
    [ObservableProperty] private int soChuaNhan;


    // ----- Lọc + phân trang cảnh báo -----
    public const int CoTrang = 5;
    private List<CanhBaoDto> _tatCaCanhBao = [];
    [ObservableProperty] private string mucLoc = "TAT_CA";
    [ObservableProperty] private int trang = 1;
    [ObservableProperty] private int tongTrang = 1;
    [ObservableProperty] private int soKetQuaLoc;
    [ObservableProperty] private string daXuLyThem = "";

    public bool LocTatCa => MucLoc == "TAT_CA";
    public bool LocNghiemTrong => MucLoc == "NGHIEM_TRONG";
    public bool LocCanhBao => MucLoc == "CANH_BAO";
    public bool LocThongTin => MucLoc == "THONG_TIN";
    public bool LocQuaHan => MucLoc == "QUA_HAN";
    public string NhanTatCa => $"Tất cả ({_tatCaCanhBao.Count})";
    public string NhanNghiemTrong => $"Nghiêm trọng ({_tatCaCanhBao.Count(x => x.MucDo == "NGHIEM_TRONG")})";
    public string NhanCanhBao => $"Cảnh báo ({_tatCaCanhBao.Count(x => x.MucDo == "CANH_BAO")})";
    public string NhanThongTin => $"Thông tin ({_tatCaCanhBao.Count(x => x.MucDo == "THONG_TIN")})";
    public string NhanQuaHan => $"Quá hạn ({_tatCaCanhBao.Count(x => x.QuaHan)})";
    public bool CoNhieuTrang => TongTrang > 1;
    public bool CoTrangTruoc => Trang > 1;
    public bool CoTrangSau => Trang < TongTrang;
    public bool KhongCoKetQuaLoc => _tatCaCanhBao.Count > 0 && SoKetQuaLoc == 0;
    public string ThongTinTrang => SoKetQuaLoc == 0 ? "" : $"Hiển thị {(Trang - 1) * CoTrang + 1}–{Math.Min(Trang * CoTrang, SoKetQuaLoc)} / {SoKetQuaLoc}  ·  Trang {Trang}/{TongTrang}";

    partial void OnMucLocChanged(string value) { Trang = 1; LamMoiTrang(); foreach (var t in new[] { nameof(LocTatCa), nameof(LocNghiemTrong), nameof(LocCanhBao), nameof(LocThongTin), nameof(LocQuaHan) }) OnPropertyChanged(t); }
    partial void OnTrangChanged(int value) => LamMoiTrang();

    /// <summary>Dựng lại trang hiện tại từ toàn bộ cảnh báo theo bộ lọc; giữ nguyên trang đang xem khi tự làm mới (kẹp lại nếu hết trang).</summary>
    private void LamMoiTrang()
    {
        var loc = _tatCaCanhBao.Where(x => MucLoc switch
        {
            "NGHIEM_TRONG" => x.MucDo == "NGHIEM_TRONG",
            "CANH_BAO" => x.MucDo == "CANH_BAO",
            "THONG_TIN" => x.MucDo == "THONG_TIN",
            "QUA_HAN" => x.QuaHan,
            _ => true
        }).ToList();
        SoKetQuaLoc = loc.Count;
        TongTrang = Math.Max(1, (int)Math.Ceiling(loc.Count / (double)CoTrang));
        if (Trang > TongTrang) { Trang = TongTrang; return; }
        CanhBaos.Clear();
        foreach (var c in loc.Skip((Trang - 1) * CoTrang).Take(CoTrang)) CanhBaos.Add(c);
        foreach (var t in new[] { nameof(NhanTatCa), nameof(NhanNghiemTrong), nameof(NhanCanhBao), nameof(NhanThongTin), nameof(NhanQuaHan), nameof(CoNhieuTrang),
                     nameof(CoTrangTruoc), nameof(CoTrangSau), nameof(KhongCoKetQuaLoc), nameof(KhongCoCanhBao), nameof(ThongTinTrang) })
            OnPropertyChanged(t);
    }

    [RelayCommand] private void LocMuc(string? muc) => MucLoc = muc ?? "TAT_CA";
    [RelayCommand] private void TrangTruoc() { if (Trang > 1) Trang--; }
    [RelayCommand] private void TrangSau() { if (Trang < TongTrang) Trang++; }

    /// <summary>Định nghĩa KPI (từ điển chỉ số của màn Báo cáo) để hiện khi rê chuột: công thức và quy ước.</summary>
    private Dictionary<string, string> _dinhNghia = new();

    /// <summary>Cảnh báo mà Kế toán xử lý được ngay (mỗi cảnh báo mở được màn hình xử lý trên thanh bên của Kế toán).</summary>
    private static readonly HashSet<string> MaCanhBaoKeToan = ["DOI_SOAT_LOI", "CHUYEN_KHOAN_HET_HAN", "CONG_NO_QUA_HAN"];

    /// <summary>Lọc cảnh báo theo phạm vi của Kế toán (dùng chung cho màn điều hành và số cảnh báo trên thanh trên).</summary>
    public static List<CanhBaoDto> LocCanhBaoKeToan(IEnumerable<CanhBaoDto> canhBaos) => canhBaos.Where(x => MaCanhBaoKeToan.Contains(x.Ma)).ToList();

    /// <summary>Điều hành dành cho Kế toán: số liệu tiền thay cho số liệu phòng / lễ tân.</summary>
    public bool LaKeToan { get; }
    public bool LaKhongPhaiKeToan => !LaKeToan;
    public string TieuDeManHinh => "Tổng quan điều hành";
    public string MoTaManHinh => LaKeToan
        ? "Doanh thu, công nợ và việc cần xử lý. Mỗi việc có nút mở thẳng đúng màn hình để xử lý."
        : "Hiện tại khách sạn thế nào, vấn đề gì, mức độ ra sao, ảnh hưởng gì, hạn khi nào và xử lý thế nào - đủ để điều hành trong một màn hình.";

    // Việc của Kế toán: số liệu lấy từ bảng điều hành (công nợ) và cảnh báo (thông báo tiền về bất thường).
    [ObservableProperty] private int soThongBaoNganHangCanXuLy;
    public string NoQuaHanTong => Bang is null ? "" : Vnd(Bang.CongNo.TongQuaHan);
    public string NoQuaHanSoHoaDon => Bang is null ? "" : $"{Bang.CongNo.SoHoaDonQuaHan} hóa đơn · tổng công nợ {Vnd(Bang.CongNo.TongConNo)}";
    // Mỗi việc: có việc thì ghi rõ số lượng + tiền và nút bật; hết việc thì ghi "✓ ..." và khoá nút (không bắt người dùng mở màn hình trống).
    public string ThongBaoNganHangChiTiet => Bang is null ? "Đang tải..." : SoThongBaoNganHangCanXuLy == 0 ? "✓ Không có thông báo tiền về bất thường" : $"{SoThongBaoNganHangCanXuLy} thông báo tiền về bất thường chưa xử lý";
    public string NoQuaHanChiTiet => Bang is null ? "Đang tải..." : Bang.CongNo.SoHoaDonQuaHan == 0 ? "✓ Không có hóa đơn nợ quá hạn" : $"{Bang.CongNo.SoHoaDonQuaHan} hóa đơn nợ quá hạn, tổng {Vnd(Bang.CongNo.TongQuaHan)}";
    public bool CoThongBaoNganHang => SoThongBaoNganHangCanXuLy > 0;
    public bool CoNoQuaHan => Bang?.CongNo.SoHoaDonQuaHan > 0;

    partial void OnSoThongBaoNganHangCanXuLyChanged(int value)
    {
        OnPropertyChanged(nameof(ThongBaoNganHangChiTiet));
        OnPropertyChanged(nameof(CoThongBaoNganHang));
    }

    public DieuHanhViewModel(ApiClient api, bool laKeToan = false)
    {
        _api = api;
        LaKeToan = laKeToan;
        _ = TaiAsync();
        _ = TaiDinhNghiaAsync();
        BatDauTuLamMoi();
    }

    // ---------- Tự làm mới ----------
    public void BatDauTuLamMoi()
    {
        if (_dongHo is not null) return;
        _dongHo = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _dongHo.Tick += (_, _) => { _ = TaiAsync(); };
        _dongHo.Start();
    }

    public void DungTuLamMoi()
    {
        _dongHo?.Stop();
        _dongHo = null;
    }

    private async Task TaiDinhNghiaAsync()
    {
        try { _dinhNghia = (await _api.GetDinhNghiaChiSoAsync()).ToDictionary(x => x.Ma, x => $"{x.Ten}: {x.GiaiThich}"); OnPropertyChanged(nameof(GiaiThichCongSuat)); OnPropertyChanged(nameof(GiaiThichAdr)); OnPropertyChanged(nameof(GiaiThichRevPar)); OnPropertyChanged(nameof(GiaiThichDoanhThu)); }
        catch (LoiYeuCauApi) { /* chỉ là chú giải */ }
    }

    private string DinhNghia(string ma) => _dinhNghia.GetValueOrDefault(ma, "");
    public string GiaiThichCongSuat => DinhNghia("CONG_SUAT") + "\nCông suất 'tức thời' trên thẻ Tình hình phòng = phòng đang có khách ÷ tổng phòng tại thời điểm xem.";
    public string GiaiThichAdr => DinhNghia("ADR");
    public string GiaiThichRevPar => DinhNghia("REVPAR");
    public string GiaiThichDoanhThu => DinhNghia("DOANH_THU");

    // ---------- Chuỗi hiển thị (đã định dạng sẵn để XAML chỉ việc gắn) ----------
    public string DoanhThuHomNay => Vnd(Bang?.DoanhThu.HomNay);
    public string DoanhThuThang => Vnd(Bang?.DoanhThu.ThangNay);
    public string SoSanhThang
    {
        get
        {
            var thayDoi = Bang?.DoanhThu.ThayDoiSoVoiThangTruocPhanTram;
            if (Bang is null) return "";
            if (thayDoi is null) return "Chưa có số liệu cùng kỳ tháng trước để so sánh";
            return $"{(thayDoi >= 0 ? "▲" : "▼")} {Math.Abs(thayDoi.Value):0.#}% so với cùng kỳ tháng trước ({Vnd(Bang.DoanhThu.CungKyThangTruoc)})";
        }
    }
    public bool SoSanhThangTang => (Bang?.DoanhThu.ThayDoiSoVoiThangTruocPhanTram ?? 0) >= 0;
    public string CongSuat => Bang is null ? "" : $"{Bang.DoanhThu.CongSuatHomNayPhanTram:0.#}%";
    public string CongSuatThang => Bang is null ? "" : $"Tháng này: {Bang.DoanhThu.CongSuatThangPhanTram:0.#}% · tức thời: {Bang.Phong.CongSuatPhanTram:0.#}% · hôm qua: {Bang.HomQua.CongSuatPhanTram:0.#}%";
    public string Adr => Vnd(Bang?.DoanhThu.AdrHomNay);
    public string AdrThang => Bang is null ? "" : $"Tháng này: {Vnd(Bang.DoanhThu.AdrThang)} · hôm qua: {Vnd(Bang.HomQua.Adr)}";
    public string RevPar => Vnd(Bang?.DoanhThu.RevParHomNay);
    public string RevParThang => Bang is null ? "" : $"Tháng này: {Vnd(Bang.DoanhThu.RevParThang)}";
    public string DoanhThuHomQua => Bang is null ? "" : $"Hôm qua: {Vnd(Bang.HomQua.DoanhThu)} · tháng này: {Vnd(Bang.DoanhThu.ThangNay)}";
    public string CongNo => Vnd(Bang?.CongNo.TongConNo);
    public string CongNoChiTiet => Bang is null ? "" : $"{Bang.CongNo.SoHoaDonConNo} hóa đơn · quá hạn {Bang.CongNo.SoHoaDonQuaHan} ({Vnd(Bang.CongNo.TongQuaHan)})";
    public string TieuDeCanhBao => Bang is null ? "Cảnh báo" : $"Cảnh báo cần xử lý ({(LaKeToan ? _tatCaCanhBao.Count : Bang.CanhBao.Tong)})";
    public string TomTatCanhBao => Bang is null ? "" : (LaKeToan
        ? $"{_tatCaCanhBao.Count(x => x.MucDo == "NGHIEM_TRONG")} nghiêm trọng · {_tatCaCanhBao.Count(x => x.MucDo == "CANH_BAO")} cảnh báo · {_tatCaCanhBao.Count(x => x.MucDo == "THONG_TIN")} thông tin"
        : $"{Bang.CanhBao.NghiemTrong} nghiêm trọng · {Bang.CanhBao.CanhBao} cảnh báo · {Bang.CanhBao.ThongTin} thông tin") + (SoQuaHan > 0 ? $" · {SoQuaHan} QUÁ HẠN" : "") + (SoChuaNhan > 0 ? $" · {SoChuaNhan} chưa ai nhận" : "");
    public bool KhongCoCanhBao => Bang is not null && _tatCaCanhBao.Count == 0;
    public string TrangThaiDuLieu => DuLieuCu ? $"⚠ Không tải được số liệu mới - đang hiện số liệu cũ ({CapNhatLuc})" : CapNhatLuc;
    public bool CoViecDen => KhachDen.Count > 0;
    public bool CoViecTra => KhachTra.Count > 0;
    public string ViecDenTieuDe => Bang is null ? "" : $"Khách đến hôm nay chưa nhận phòng ({Bang.ViecHomNay.TongKhachDenChuaNhan})";
    public string ViecTraTieuDe => Bang is null ? "" : $"Khách ở dự kiến trả hôm nay ({Bang.ViecHomNay.TongKhachTraChuaTra})";
    public bool CoDaXuLy => DaXuLy.Count > 0;

    private static string Vnd(decimal? v) => v is null ? "" : $"{v:N0}đ";

    partial void OnBangChanged(BangDieuHanhDto? value)
    {
        foreach (var ten in new[] { nameof(DoanhThuHomNay), nameof(DoanhThuThang), nameof(SoSanhThang), nameof(SoSanhThangTang), nameof(CongSuat), nameof(CongSuatThang),
                     nameof(Adr), nameof(AdrThang), nameof(RevPar), nameof(RevParThang), nameof(CongNo), nameof(CongNoChiTiet), nameof(NoQuaHanTong), nameof(NoQuaHanSoHoaDon), nameof(NoQuaHanChiTiet), nameof(CoNoQuaHan), nameof(ThongBaoNganHangChiTiet), nameof(TieuDeCanhBao), nameof(KhongCoCanhBao),
                     nameof(DoanhThuHomQua), nameof(TomTatCanhBao), nameof(ViecDenTieuDe), nameof(ViecTraTieuDe), nameof(CoViecDen), nameof(CoViecTra) })
            OnPropertyChanged(ten);
    }

    partial void OnDuLieuCuChanged(bool value) => OnPropertyChanged(nameof(TrangThaiDuLieu));
    partial void OnCapNhatLucChanged(string value) => OnPropertyChanged(nameof(TrangThaiDuLieu));

    [RelayCommand]
    private async Task TaiAsync()
    {
        if (DangTai) return;
        DangTai = true;
        try
        {
            var bang = await _api.GetBangDieuHanhAsync();
            var canhBao = await _api.GetCanhBaoAsync();

            var danhSachCanhBao = LaKeToan ? LocCanhBaoKeToan(canhBao.CanhBaoDangCo) : canhBao.CanhBaoDangCo.ToList();
            SoThongBaoNganHangCanXuLy = canhBao.CanhBaoDangCo.FirstOrDefault(x => x.Ma == "DOI_SOAT_LOI")?.SoLuong ?? 0;

            _tatCaCanhBao = danhSachCanhBao;
            LamMoiTrang();
            DaXuLy.Clear();
            if (!LaKeToan) foreach (var c in canhBao.DaXuLyGanDay.Take(5)) DaXuLy.Add(c);
            DaXuLyThem = !LaKeToan && canhBao.DaXuLyGanDay.Count > 5 ? $"… và {canhBao.DaXuLyGanDay.Count - 5} cảnh báo khác đã xử lý" : "";
            Tangs.Clear();
            foreach (var t in bang.TheoTang) Tangs.Add(t);
            KhachDen.Clear(); foreach (var v in bang.ViecHomNay.KhachDenChuaNhan) KhachDen.Add(v);
            KhachTra.Clear(); foreach (var v in bang.ViecHomNay.KhachTraChuaTra) KhachTra.Add(v);
            SoQuaHan = LaKeToan ? danhSachCanhBao.Count(x => x.QuaHan) : canhBao.QuaHan;
            SoChuaNhan = LaKeToan ? danhSachCanhBao.Count(x => x.TrangThaiXuLy == "CHUA_NHAN") : canhBao.ChuaNhan;
            Bang = bang;
            OnPropertyChanged(nameof(KhongCoCanhBao)); OnPropertyChanged(nameof(CoDaXuLy)); OnPropertyChanged(nameof(TomTatCanhBao));
            CapNhatLuc = $"Cập nhật lúc {bang.ThoiDiem:HH:mm:ss dd/MM/yyyy}";
            DuLieuCu = false;
            ThongBao = "";
            if (LaKeToan) DaTaiCanhBao?.Invoke(danhSachCanhBao.Count(x => x.MucDo == "NGHIEM_TRONG"), danhSachCanhBao.Count);
            else DaTaiCanhBao?.Invoke(canhBao.NghiemTrong, canhBao.NghiemTrong + canhBao.CanhBao + canhBao.ThongTin);
            DaTaiDanhSachCanhBao?.Invoke(canhBao.CanhBaoDangCo);   // khung chính tự lọc theo vai trò và đếm một cách duy nhất
        }
        catch (LoiYeuCauApi loi)
        {
            ThongBao = loi.Message;
            DuLieuCu = Bang is not null;
        }
        catch (HttpRequestException loi)
        {
            ThongBao = $"Không kết nối được máy chủ: {loi.Message}";
            DuLieuCu = Bang is not null;
        }
        finally
        {
            DangTai = false;
        }
    }

    [RelayCommand]
    private void MoCanhBao(CanhBaoDto? canhBao)
    {
        if (canhBao is null) return;
        MoManHinh?.Invoke(canhBao.ManHinh);
    }

    [RelayCommand] private void MoNhom(string? manHinh) { if (!string.IsNullOrEmpty(manHinh)) MoManHinh?.Invoke(manHinh); }

    [RelayCommand] private void MoViec(DhViecDto? viec) { if (viec is not null) MoManHinh?.Invoke(viec.ManHinh); }
}
