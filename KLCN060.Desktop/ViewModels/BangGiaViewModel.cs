using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>
/// D25 - Bảng giá phòng (Quản lý): quy tắc giá cuối tuần, mùa cao điểm, lễ/tết, giá đoàn/doanh nghiệp theo ngày, thứ trong tuần, loại phòng và loại đặt phòng.
/// Công thức duy nhất cho mỗi đêm: quy tắc đang bật khớp có độ ưu tiên cao nhất thắng (bằng nhau → quy tắc tạo sau), các quy tắc KHÔNG cộng dồn.
/// Màn hình cho tự kiểm tra: "Báo giá thử" (giá cơ bản → quy tắc nào → điều chỉnh → giá cuối từng đêm, kể cả quy tắc bị thay thế) và "Giá đã chốt của một đặt phòng"
/// (snapshot từng đêm, so với bảng giá hiện hành); mọi thay đổi quy tắc có lịch sử và không làm đổi giá phiếu đã đặt.
/// </summary>
public partial class BangGiaViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private bool _dangHoanTac;
    private List<QuyTacGiaDto> _tatCa = [];
    private (string Ten, string Loai, string MaLoai, string Kieu, string GiaTri, DateTime? Tu, DateTime? Den, string Thu, string DoiTuong, string UuTien, bool Bat, string GhiChu) _goc =
        ("", "CUOI_TUAN", "", "PHAN_TRAM", "", null, null, "", "", "10", true, "");

    public ObservableCollection<QuyTacGiaDto> DanhSach { get; } = [];
    public ObservableCollection<LuaChon> LoaiPhongs { get; } = [];
    public ObservableCollection<DemBaoGiaDto> KetQuaTheoDem { get; } = [];
    public ObservableCollection<LichSuQuyTacGiaDto> LichSu { get; } = [];
    public ObservableCollection<PhongGiaChotDto> GiaChotPhong { get; } = [];

    public static IReadOnlyList<LuaChon> LoaiQuyTacTuyChon { get; } =
    [
        new("CUOI_TUAN", "Cuối tuần"), new("MUA_CAO_DIEM", "Mùa cao điểm"), new("LE_TET", "Lễ / Tết"), new("DOANH_NGHIEP", "Đoàn"), new("KHAC", "Khác")
    ];
    public static IReadOnlyList<LuaChon> KieuTuyChon { get; } = [new("PHAN_TRAM", "Tăng/giảm theo %"), new("GIA_CO_DINH", "Giá cố định mỗi đêm")];
    public static IReadOnlyList<LuaChon> DoiTuongTuyChon { get; } = [new("", "Mọi loại đặt phòng"), new("CA_NHAN", "Chỉ đặt cá nhân"), new("DOAN", "Chỉ đặt đoàn")];

    public string CongThuc => "Công thức mỗi đêm: lấy giá cơ bản của loại phòng; trong các quy tắc ĐANG BẬT khớp đêm đó (loại phòng, ngày, thứ, loại đặt phòng) chỉ quy tắc có ĐỘ ƯU TIÊN CAO NHẤT được áp dụng (bằng nhau → quy tắc tạo sau); các quy tắc không cộng dồn. "
        + "Khi đặt phòng, giá từng đêm được CHỐT vào phiếu: đổi / bật / tắt / xóa quy tắc sau đó không làm đổi phiếu đã đặt. Giá chỉ được tính lại khi phiếu đổi ngày ở, đổi phòng hoặc nhận phòng sớm.";

    [ObservableProperty] private QuyTacGiaDto? quyTacDaChon;

    // ---- tổng hợp + bộ lọc
    [ObservableProperty] private string thongKe = "";
    [ObservableProperty] private string tieuDe = "";
    [ObservableProperty] private string tuKhoa = "";
    [ObservableProperty] private string locHieuLuc = "";    // "", DANG_AP_DUNG, TAT, HET_HAN, CANH_BAO
    [ObservableProperty] private bool dangTai;
    /// <summary>Công cụ phía dưới danh sách: BAO_GIA (báo giá thử), GIA_CHOT (giá đã chốt của đặt phòng), LICH_SU (lịch sử thay đổi bảng giá).</summary>
    [ObservableProperty] private string tab = "BAO_GIA";
    [RelayCommand] private void ChonTab(string ma) => Tab = ma;
    partial void OnTuKhoaChanged(string value) => ApDungLoc();
    partial void OnLocHieuLucChanged(string value) => ApDungLoc();
    [RelayCommand] private void XoaBoLoc() { TuKhoa = ""; LocHieuLuc = ""; }
    [RelayCommand] private void LocNhanh(string ma) => LocHieuLuc = LocHieuLuc == ma ? "" : ma;
    public bool TrongDanhSach => !DangTai && DanhSach.Count == 0;

    // ---- form quy tắc
    [ObservableProperty] private string tenQuyTac = "";
    [ObservableProperty] private string loaiQuyTac = "CUOI_TUAN";
    [ObservableProperty] private string maLoai = "";
    [ObservableProperty] private string kieu = "PHAN_TRAM";
    [ObservableProperty] private string giaTri = "";
    [ObservableProperty] private DateTime? tuNgay;
    [ObservableProperty] private DateTime? denNgay;
    [ObservableProperty] private bool thuHai, thuBa, thuTu, thuNam, thuSau, thuBay, chuNhat;
    [ObservableProperty] private string doiTuong = "";
    [ObservableProperty] private string uuTien = "10";
    [ObservableProperty] private bool hoatDong = true;
    [ObservableProperty] private string ghiChu = "";
    [ObservableProperty] private bool dangSua;
    [ObservableProperty] private string loiTen = "";
    [ObservableProperty] private string loiGiaTri = "";
    [ObservableProperty] private string loiNgay = "";
    [ObservableProperty] private string loiUuTien = "";

    // ---- báo giá thử
    [ObservableProperty] private string maLoaiBaoGia = "";
    [ObservableProperty] private DateTime nhanBaoGia = DateTime.Today.AddDays(1);
    [ObservableProperty] private DateTime traBaoGia = DateTime.Today.AddDays(4);
    [ObservableProperty] private string loaiDatBaoGia = "CA_NHAN";
    [ObservableProperty] private string tomTatBaoGia = "";

    // ---- giá đã chốt của một đặt phòng
    [ObservableProperty] private string maPhieuTraCuu = "";
    [ObservableProperty] private string tieuDeGiaChot = "";
    [ObservableProperty] private string quyTacTinhLai = "";

    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool thongBaoLaLoi = true;

    public bool CoTheQuanLy { get; } = AppSession.Current.CoQuyen(QuyenHeThong.QuanLyGia);
    public bool CoForm => true;
    public bool DaChonQuyTac => QuyTacDaChon is not null && DangSua;
    public bool CoThayDoi => (TenQuyTac, LoaiQuyTac, MaLoai, Kieu, GiaTri, TuNgay, DenNgay, CacThuChon() ?? "", DoiTuong, UuTien, HoatDong, GhiChu) != _goc && (DangSua || TenQuyTac != "" || GiaTri != "");
    public string TieuDeForm => DangSua ? $"Sửa quy tắc #{QuyTacDaChon?.MaQuyTac}" : "Thêm quy tắc giá";
    /// <summary>Giá cố định mỗi đêm là số tiền (tự chèn dấu chấm hàng nghìn khi gõ); phần trăm thì giữ nguyên số thập phân / số âm.</summary>
    public bool GiaTriLaTien => Kieu == "GIA_CO_DINH";
    private decimal? DocGiaTri() => decimal.TryParse((GiaTriLaTien ? GiaTri.Replace(".", "") : GiaTri).Replace(",", ".").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    public string GoiYGiaTri => Kieu == "PHAN_TRAM" ? "Ví dụ 20 = tăng 20%, -10 = giảm 10% (từ -90 đến 300)" : "Giá mỗi đêm (VND), ví dụ 900.000 (tối đa 100.000.000)";
    public string ThongTinQuyTac => QuyTacDaChon is not { } q ? "" :
        $"{q.TrangThaiHienThi} · {q.DoiTuongHienThi} · {q.PhamViLoaiPhong} · {q.ThoiGianHienThi}\n{q.DaChotHienThi}" + (string.IsNullOrEmpty(q.CanhBaoGop) ? "" : "\n⚠ " + q.CanhBaoGop.Replace("\n", "\n⚠ "));
    public bool CoCanhBaoQuyTac => QuyTacDaChon?.CoCanhBao == true;

    public BangGiaViewModel(ApiClient api)
    {
        _api = api;
        _ = TaiAsync();
    }

    partial void OnKieuChanged(string value) { LoiGiaTri = ""; OnPropertyChanged(nameof(GiaTriLaTien)); if (value != "GIA_CO_DINH" && GiaTri.Contains('.')) GiaTri = GiaTri.Replace(".", ""); OnPropertyChanged(nameof(GoiYGiaTri)); OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnDangSuaChanged(bool value) { OnPropertyChanged(nameof(TieuDeForm)); OnPropertyChanged(nameof(DaChonQuyTac)); }
    partial void OnTenQuyTacChanged(string value) { LoiTen = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnGiaTriChanged(string value) { LoiGiaTri = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnTuNgayChanged(DateTime? value) { LoiNgay = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnDenNgayChanged(DateTime? value) { LoiNgay = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnUuTienChanged(string value) { LoiUuTien = ""; OnPropertyChanged(nameof(CoThayDoi)); }
    partial void OnLoaiQuyTacChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnMaLoaiChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnDoiTuongChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnHoatDongChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnGhiChuChanged(string value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuHaiChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuBaChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuTuChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuNamChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuSauChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnThuBayChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));
    partial void OnChuNhatChanged(bool value) => OnPropertyChanged(nameof(CoThayDoi));

    partial void OnQuyTacDaChonChanged(QuyTacGiaDto? oldValue, QuyTacGiaDto? newValue)
    {
        if (_dangHoanTac) { CapNhatChiTietQuyTac(); return; }
        if (CoThayDoi && oldValue is not null && !HopThoai.XacNhan("Thay đổi chưa lưu", "Quy tắc đang sửa chưa được lưu. Chuyển sang quy tắc khác và bỏ các thay đổi này?"))
        {
            _dangHoanTac = true; QuyTacDaChon = oldValue; _dangHoanTac = false;
            return;
        }
        if (newValue is null) { CapNhatChiTietQuyTac(); return; }
        NapForm(newValue);
        _ = NapLichSuAsync(newValue.MaQuyTac);
    }

    private void NapForm(QuyTacGiaDto value)
    {
        DangSua = true;
        TenQuyTac = value.TenQuyTac;
        LoaiQuyTac = value.LoaiQuyTac;
        MaLoai = value.MaLoai ?? "";
        Kieu = value.KieuDieuChinh;
        GiaTri = value.KieuDieuChinh == "GIA_CO_DINH" ? KLCN060.Desktop.Controls.MoneyInput.Dinh(value.GiaTri) : value.GiaTri.ToString("0.##", CultureInfo.InvariantCulture);
        TuNgay = value.TuNgay?.ToDateTime(TimeOnly.MinValue);
        DenNgay = value.DenNgay?.ToDateTime(TimeOnly.MinValue);
        var thu = (value.CacThuApDung ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        ThuHai = thu.Contains("1"); ThuBa = thu.Contains("2"); ThuTu = thu.Contains("3"); ThuNam = thu.Contains("4");
        ThuSau = thu.Contains("5"); ThuBay = thu.Contains("6"); ChuNhat = thu.Contains("0");
        DoiTuong = value.LoaiDatPhongApDung ?? "";
        UuTien = value.UuTien.ToString();
        HoatDong = value.HoatDong;
        GhiChu = value.GhiChu ?? "";
        LoiTen = LoiGiaTri = LoiNgay = LoiUuTien = "";
        _goc = (TenQuyTac, LoaiQuyTac, MaLoai, Kieu, GiaTri, TuNgay, DenNgay, CacThuChon() ?? "", DoiTuong, UuTien, HoatDong, GhiChu);
        CapNhatChiTietQuyTac();
    }

    private void CapNhatChiTietQuyTac()
    {
        OnPropertyChanged(nameof(TieuDeForm)); OnPropertyChanged(nameof(ThongTinQuyTac)); OnPropertyChanged(nameof(CoCanhBaoQuyTac)); OnPropertyChanged(nameof(CoThayDoi)); OnPropertyChanged(nameof(DaChonQuyTac));
    }

    // ------------------------------------------------------------------ nạp + lọc
    private async Task KhoiTaoAsync()
    {
        try
        {
            LoaiPhongs.Clear();
            LoaiPhongs.Add(new("", "Mọi loại phòng"));
            foreach (var loai in await _api.GetRoomTypesAsync()) LoaiPhongs.Add(new(loai.MaLoai, $"{loai.TenLoai} ({loai.DonGia:N0}đ)"));
            if (string.IsNullOrEmpty(MaLoaiBaoGia)) MaLoaiBaoGia = LoaiPhongs.Skip(1).FirstOrDefault()?.Ma ?? "";
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    [RelayCommand]
    private async Task TaiAsync()
    {
        DangTai = true;
        try
        {
            await KhoiTaoAsync();
            var giu = QuyTacDaChon?.MaQuyTac;
            _tatCa = await _api.GetQuyTacGiaAsync();
            ApDungLoc();
            if (giu is not null)
            {
                _dangHoanTac = true; QuyTacDaChon = _tatCa.FirstOrDefault(x => x.MaQuyTac == giu); _dangHoanTac = false;
                CapNhatChiTietQuyTac();
            }
            else _ = NapLichSuAsync(null);
            DatThongBao("", false);
        }
        catch (Exception e) when (e is LoiYeuCauApi or HttpRequestException)
        {
            DatThongBao(e is LoiYeuCauApi l ? l.Message : $"Không kết nối được máy chủ: {e.Message}", true);
        }
        finally { DangTai = false; OnPropertyChanged(nameof(TrongDanhSach)); }
    }

    private void ApDungLoc()
    {
        IEnumerable<QuyTacGiaDto> q = _tatCa;
        if (!string.IsNullOrWhiteSpace(TuKhoa))
            foreach (var t in TuKhoa.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = t;
                q = q.Where(x => x.TenQuyTac.Contains(k, StringComparison.OrdinalIgnoreCase) || (x.GhiChu?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                    || x.LoaiHienThi.Contains(k, StringComparison.OrdinalIgnoreCase) || x.PhamViLoaiPhong.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
        q = LocHieuLuc switch
        {
            "DANG_AP_DUNG" => q.Where(x => x.HieuLuc == "DANG_AP_DUNG"),
            "TAT" => q.Where(x => x.HieuLuc == "TAT"),
            "HET_HAN" => q.Where(x => x.HieuLuc is "DA_HET_HAN" or "CHUA_BAT_DAU"),
            "CANH_BAO" => q.Where(x => x.CoCanhBao),
            _ => q
        };
        var ds = q.ToList();
        DanhSach.Clear(); foreach (var x in ds) DanhSach.Add(x);
        TieuDe = ds.Count == _tatCa.Count ? $"{ds.Count} quy tắc" : $"{ds.Count}/{_tatCa.Count} quy tắc khớp bộ lọc";
        ThongKe = $"Đang áp dụng {_tatCa.Count(x => x.HieuLuc == "DANG_AP_DUNG")} · đang tắt {_tatCa.Count(x => x.HieuLuc == "TAT")} · chưa bắt đầu / hết hạn {_tatCa.Count(x => x.HieuLuc is "CHUA_BAT_DAU" or "DA_HET_HAN")}"
            + (_tatCa.Any(x => x.CoCanhBao) ? $" · ⚠ {_tatCa.Count(x => x.CoCanhBao)} quy tắc cùng ưu tiên, phạm vi giao nhau" : "");
        OnPropertyChanged(nameof(TrongDanhSach));
    }

    private async Task NapLichSuAsync(int? ma)
    {
        LichSu.Clear();
        try
        {
            var ds = await _api.GetLichSuQuyTacGiaAsync(ma);
            if (QuyTacDaChon?.MaQuyTac != ma && !(ma is null && QuyTacDaChon is null)) return;
            foreach (var x in ds) LichSu.Add(x);
        }
        catch (LoiYeuCauApi) { /* lịch sử chỉ để tham khảo */ }
    }

    // ------------------------------------------------------------------ thao tác quy tắc
    [RelayCommand]
    private void QuyTacMoi()
    {
        if (CoThayDoi && !HopThoai.XacNhan("Thay đổi chưa lưu", "Bỏ các thay đổi chưa lưu và thêm quy tắc mới?")) return;
        DatLaiFormQuyTacMoi();
    }

    /// <summary>Dựng biểu mẫu trống cho quy tắc mới, KHÔNG hỏi xác nhận (dùng sau khi xóa: dữ liệu cũ của quy tắc vừa xóa không phải "thay đổi chưa lưu").</summary>
    private void DatLaiFormQuyTacMoi()
    {
        _dangHoanTac = true; QuyTacDaChon = null; _dangHoanTac = false;
        DangSua = false;
        TenQuyTac = "";
        LoaiQuyTac = "CUOI_TUAN";
        MaLoai = "";
        Kieu = "PHAN_TRAM";
        GiaTri = "";
        TuNgay = DenNgay = null;
        ThuHai = ThuBa = ThuTu = ThuNam = ThuSau = ThuBay = ChuNhat = false;
        DoiTuong = "";
        UuTien = "10";
        HoatDong = true;
        GhiChu = "";
        LoiTen = LoiGiaTri = LoiNgay = LoiUuTien = "";
        _goc = (TenQuyTac, LoaiQuyTac, MaLoai, Kieu, GiaTri, TuNgay, DenNgay, "", DoiTuong, UuTien, HoatDong, GhiChu);
        CapNhatChiTietQuyTac();
        _ = NapLichSuAsync(null);
        DatThongBao("", false);
    }

    [RelayCommand]
    private void HoanTac() { if (QuyTacDaChon is not null && DangSua) NapForm(QuyTacDaChon); else QuyTacMoi(); }

    private string? CacThuChon()
    {
        var thu = new List<string>();
        if (ChuNhat) thu.Add("0");
        if (ThuHai) thu.Add("1");
        if (ThuBa) thu.Add("2");
        if (ThuTu) thu.Add("3");
        if (ThuNam) thu.Add("4");
        if (ThuSau) thu.Add("5");
        if (ThuBay) thu.Add("6");
        return thu.Count == 0 ? null : string.Join(",", thu);
    }

    [RelayCommand]
    private async Task LuuAsync()
    {
        if (!CoTheQuanLy) { DatThongBao("Tài khoản của bạn không có quyền quản lý bảng giá.", true); return; }
        LoiTen = string.IsNullOrWhiteSpace(TenQuyTac) ? "Nhập tên quy tắc." : TenQuyTac.Trim().Length > 100 ? "Tên tối đa 100 ký tự." : "";
        LoiGiaTri = ""; decimal giaTri = 0;
        if (DocGiaTri() is not { } giaTriDoc) giaTri = 0;
        else giaTri = giaTriDoc;
        if (DocGiaTri() is null)
            LoiGiaTri = Kieu == "PHAN_TRAM" ? "Nhập phần trăm điều chỉnh (số, có thể âm)." : "Nhập giá mỗi đêm (số).";
        else if (Kieu == "PHAN_TRAM" && (giaTri == 0 || giaTri < -90 || giaTri > 300)) LoiGiaTri = "Phần trăm phải khác 0 và nằm trong khoảng -90 đến 300.";
        else if (Kieu == "GIA_CO_DINH" && (giaTri <= 0 || giaTri > 100_000_000m)) LoiGiaTri = "Giá cố định phải lớn hơn 0 và không quá 100.000.000đ.";
        LoiNgay = TuNgay is { } t && DenNgay is { } d && d < t ? "Đến ngày phải sau hoặc bằng từ ngày." : "";
        if (LoiNgay == "" && TuNgay is null && DenNgay is null && CacThuChon() is null && string.IsNullOrEmpty(DoiTuong) && string.IsNullOrEmpty(MaLoai))
            LoiNgay = "Hãy giới hạn phạm vi: khoảng ngày, thứ trong tuần, loại phòng hoặc loại đặt phòng (nếu không sẽ đổi giá mọi đêm).";
        LoiUuTien = int.TryParse(UuTien.Trim(), out var uuTien) && uuTien is >= 0 and <= 1000 ? "" : "Độ ưu tiên là số nguyên từ 0 đến 1000 (số lớn = ưu tiên cao).";
        if (LoiTen != "" || LoiGiaTri != "" || LoiNgay != "" || LoiUuTien != "")
        {
            DatThongBao(LoiTen != "" ? LoiTen : LoiGiaTri != "" && DocGiaTri() is null ? LoiGiaTri : "Vui lòng sửa các ô báo lỗi màu đỏ rồi lưu lại.", true);
            return;
        }
        if (DangSua && QuyTacDaChon is not null && !CoThayDoi) { DatThongBao("Chưa có thay đổi nào để lưu.", false); return; }

        var yeuCau = new YeuCauQuyTacGia(TenQuyTac.Trim(), LoaiQuyTac, string.IsNullOrEmpty(MaLoai) ? null : MaLoai, Kieu, giaTri,
            TuNgay is null ? null : DateOnly.FromDateTime(TuNgay.Value), DenNgay is null ? null : DateOnly.FromDateTime(DenNgay.Value),
            CacThuChon(), string.IsNullOrEmpty(DoiTuong) ? null : DoiTuong, uuTien, HoatDong, string.IsNullOrWhiteSpace(GhiChu) ? null : GhiChu.Trim());
        try
        {
            var luu = DangSua && QuyTacDaChon is not null
                ? await _api.SuaQuyTacGiaAsync(QuyTacDaChon.MaQuyTac, yeuCau)
                : await _api.TaoQuyTacGiaAsync(yeuCau);
            _dangHoanTac = true; QuyTacDaChon = null; _dangHoanTac = false;
            await TaiAsync();
            _dangHoanTac = true; QuyTacDaChon = _tatCa.FirstOrDefault(x => x.MaQuyTac == luu.MaQuyTac); _dangHoanTac = false;
            if (QuyTacDaChon is not null) { NapForm(QuyTacDaChon); await NapLichSuAsync(luu.MaQuyTac); }
            DatThongBao($"Đã lưu quy tắc \"{luu.TenQuyTac}\" ({luu.TrangThaiHienThi.ToLower()}). Áp dụng cho các đặt phòng, đổi ngày/đổi phòng và nhận sớm MỚI; phiếu đã đặt giữ giá đã chốt.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao("Chưa lưu được: " + loi.Message, true);
        }
    }

    [RelayCommand]
    private async Task BatTatAsync()
    {
        if (QuyTacDaChon is null) { DatThongBao("Chọn một quy tắc trong bảng.", true); return; }
        var q = QuyTacDaChon;
        try
        {
            await _api.SuaQuyTacGiaAsync(q.MaQuyTac, new YeuCauQuyTacGia(q.TenQuyTac, q.LoaiQuyTac, q.MaLoai, q.KieuDieuChinh, q.GiaTri, q.TuNgay, q.DenNgay,
                q.CacThuApDung, q.LoaiDatPhongApDung, q.UuTien, !q.HoatDong, q.GhiChu));
            _dangHoanTac = true; QuyTacDaChon = null; _dangHoanTac = false;
            await TaiAsync();
            _dangHoanTac = true; QuyTacDaChon = _tatCa.FirstOrDefault(x => x.MaQuyTac == q.MaQuyTac); _dangHoanTac = false;
            if (QuyTacDaChon is not null) { NapForm(QuyTacDaChon); await NapLichSuAsync(q.MaQuyTac); }
            DatThongBao(q.HoatDong ? $"Đã TẮT quy tắc \"{q.TenQuyTac}\": đặt phòng mới không còn áp dụng; phiếu đã đặt giữ giá cũ." : $"Đã BẬT quy tắc \"{q.TenQuyTac}\": áp dụng cho đặt phòng mới.", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    [RelayCommand]
    private async Task XoaAsync()
    {
        if (QuyTacDaChon is null) { DatThongBao("Chọn một quy tắc trong bảng.", true); return; }
        var q = QuyTacDaChon;
        var daChot = q.SoDemDaChot > 0 ? $"\n{q.SoPhieuDaChot} phiếu đặt ({q.SoDemDaChot} đêm) đã chốt giá bằng quy tắc này - giá của các phiếu đó GIỮ NGUYÊN, và vẫn tra được tên quy tắc đã áp dụng." : "\nChưa có phiếu đặt nào dùng quy tắc này.";
        if (!HopThoai.XacNhan("Xóa quy tắc giá", $"Xóa quy tắc giá \"{q.TenQuyTac}\"?{daChot}\nNếu chỉ muốn ngừng áp dụng tạm thời, hãy dùng \"Bật / Tắt\"."))
            return;
        try
        {
            await _api.XoaQuyTacGiaAsync(q.MaQuyTac);
            _dangHoanTac = true; QuyTacDaChon = null; _dangHoanTac = false;
            DangSua = false;
            await TaiAsync();
            DatLaiFormQuyTacMoi();
            DatThongBao($"Đã xóa quy tắc \"{q.TenQuyTac}\" (vẫn xem được trong lịch sử thay đổi bảng giá).", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    // ------------------------------------------------------------------ báo giá thử
    [RelayCommand]
    private async Task XemBaoGiaAsync()
    {
        if (string.IsNullOrEmpty(MaLoaiBaoGia)) { DatThongBao("Chọn loại phòng để báo giá.", true); return; }
        if (TraBaoGia.Date <= NhanBaoGia.Date) { DatThongBao("Ngày trả phải sau ngày nhận.", true); return; }
        try
        {
            var bg = await _api.GetBaoGiaAsync(MaLoaiBaoGia, DateOnly.FromDateTime(NhanBaoGia), DateOnly.FromDateTime(TraBaoGia), LoaiDatBaoGia);
            KetQuaTheoDem.Clear();
            foreach (var d in bg.TheoDem) KetQuaTheoDem.Add(d);
            var chenh = bg.TongTien - bg.TongCoBan;
            TomTatBaoGia = $"{bg.SoDem} đêm · giá cơ bản {bg.DonGiaCoBan:N0}đ/đêm (tổng {bg.TongCoBan:N0}đ) · chênh lệch do quy tắc {chenh:+#,##0;-#,##0;0}đ · TỔNG {bg.TongTien:N0}đ (bình quân {bg.DonGiaBinhQuan:N0}đ/đêm)";
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    // ------------------------------------------------------------------ giá đã chốt của một đặt phòng
    [RelayCommand]
    private async Task TraCuuGiaChotAsync()
    {
        GiaChotPhong.Clear(); TieuDeGiaChot = "";
        if (string.IsNullOrWhiteSpace(MaPhieuTraCuu)) { DatThongBao("Nhập mã phiếu đặt phòng (VD BK20261008-0001).", true); return; }
        try
        {
            var g = await _api.GetGiaChotDatPhongAsync(MaPhieuTraCuu);
            foreach (var p in g.Phong) GiaChotPhong.Add(p);
            TieuDeGiaChot = g.TieuDe;
            QuyTacTinhLai = g.QuyTacTinhLai;
            DatThongBao("", false);
        }
        catch (LoiYeuCauApi loi)
        {
            DatThongBao(loi.Message, true);
        }
    }

    private void DatThongBao(string noiDung, bool laLoi)
    {
        ThongBao = noiDung;
        ThongBaoLaLoi = laLoi;
    }
}
