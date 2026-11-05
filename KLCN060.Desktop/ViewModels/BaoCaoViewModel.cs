using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KLCN060.Desktop.Services;

namespace KLCN060.Desktop.ViewModels;

/// <summary>Một ô chỉ số trong bảng tổng quan: nhãn, giá trị, so sánh với kỳ trước.</summary>
public sealed record TheKpi(string Tieu, string GiaTri, string SoSanh, bool CoSoSanh, bool Tang, string GhiChu = "");

/// <summary>Một cột trong biểu đồ (cột kỳ này + cột kỳ trước để so sánh).</summary>
public sealed record CotBieuDo(string Nhan, bool HienNhan, double ChieuCao, double ChieuCaoTruoc, string ChuThich);

/// <summary>
/// D17 - Báo cáo: tổng quan KPI (doanh thu, công suất/Occupancy, ADR, RevPAR) kèm so sánh kỳ trước và biểu đồ; doanh thu theo ngày/tháng/loại phòng/nguồn
/// đặt/dịch vụ/hình thức thanh toán; thống kê đặt phòng; công nợ; xuất báo cáo Excel/PDF.
/// Chọn nhanh khoảng ngày (hôm nay, 7 ngày, tháng này, năm nay) hoặc tự chọn.
/// </summary>
public partial class BaoCaoViewModel : ObservableObject
{
    private const double ChieuCaoToiDa = 130;

    private readonly ApiClient _api;
    private int _lanTai;

    public IReadOnlyList<LuaChon> ChiSoTuyChon { get; } =
    [
        new("DOANH_THU", "Doanh thu (hóa đơn)"), new("DOANH_THU_PHONG", "Doanh thu phòng"), new("CONG_SUAT", "Công suất phòng (%)"),
        new("ADR", "ADR - giá phòng bình quân"), new("REVPAR", "RevPAR"), new("DAT_PHONG", "Số đặt phòng mới")
    ];

    public ObservableCollection<TheKpi> Kpis { get; } = [];
    public ObservableCollection<CotBieuDo> Cots { get; } = [];

    [ObservableProperty] private DateTime tuNgay = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime denNgay = DateTime.Today;
    /// <summary>TONG_QUAN, DOANH_THU, DAT_PHONG hoặc CONG_NO.</summary>
    [ObservableProperty] private string tabDangChon = "TONG_QUAN";
    [ObservableProperty] private string chiSoBieuDo = "DOANH_THU";
    [ObservableProperty] private string kyDangXem = "";
    [ObservableProperty] private string tomTatBieuDo = "";
    [ObservableProperty] private string thongBao = "";
    [ObservableProperty] private bool dangTai;
    /// <summary>Thời điểm máy chủ tính số liệu đang hiển thị (để biết số có mới không).</summary>
    [ObservableProperty] private string capNhatLuc = "";
    [ObservableProperty] private bool soLieuCu;
    [ObservableProperty] private TongQuanBaoCaoDto? tongQuan;
    [ObservableProperty] private DoanhThuChiTietDto? doanhThu;
    [ObservableProperty] private BaoCaoDatPhongDto? datPhong;
    [ObservableProperty] private CongNoBaoCaoDto? congNo;

    public BaoCaoViewModel(ApiClient api, string tabBanDau = "TONG_QUAN")
    {
        _api = api;
        tabDangChon = tabBanDau;
        _ = TaiBaoCaoAsync();
    }

    partial void OnChiSoBieuDoChanged(string value) => DungBieuDo();

    // ---------- Xuất báo cáo ----------
    public static Func<string, string, string?> ChonNoiLuu { get; set; } = (tenGoiY, phanMoRong) =>
    {
        var hop = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Lưu báo cáo quản trị", FileName = tenGoiY, DefaultExt = phanMoRong,
            Filter = phanMoRong == ".pdf" ? "Tệp PDF (*.pdf)|*.pdf" : "Tệp Excel (*.xlsx)|*.xlsx"
        };
        return hop.ShowDialog() == true ? hop.FileName : null;
    };
    [ObservableProperty] private bool dangXuat;
    [ObservableProperty] private bool thongBaoLaLoi = true;

    /// <summary>Xuất toàn bộ báo cáo của khoảng ngày đang chọn ra Excel (nhiều trang tính) hoặc PDF.</summary>
    [RelayCommand]
    private async Task XuatAsync(string dinhDang)
    {
        if (DangXuat) return;
        if (TuNgay.Date > DenNgay.Date) { ThongBao = "Từ ngày không được lớn hơn đến ngày."; ThongBaoLaLoi = true; return; }
        if ((DenNgay.Date - TuNgay.Date).TotalDays + 1 > 366) { ThongBao = "Khoảng báo cáo tối đa 366 ngày."; ThongBaoLaLoi = true; return; }
        var dd = dinhDang == "pdf" ? "pdf" : "xlsx";
        DangXuat = true;
        try
        {
            var (noiDung, tenFile) = await _api.XuatBaoCaoQuanTriAsync(DateOnly.FromDateTime(TuNgay), DateOnly.FromDateTime(DenNgay), dd);
            var duongDan = ChonNoiLuu(tenFile, "." + dd);
            if (duongDan is null) { ThongBao = "Đã hủy lưu file ."; ThongBaoLaLoi = false; return; }
            await System.IO.File.WriteAllBytesAsync(duongDan, noiDung);
            ThongBao = $"Đã xuất báo cáo {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy} ra {duongDan}"; ThongBaoLaLoi = false;
        }
        catch (LoiYeuCauApi loi) { ThongBao = loi.Message; ThongBaoLaLoi = true; }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException or System.IO.IOException or UnauthorizedAccessException)
        { ThongBao = $"Không xuất được báo cáo: {e.Message}"; ThongBaoLaLoi = true; }
        finally { DangXuat = false; }
    }

    // ---------- Chọn khoảng ngày ----------
    [RelayCommand]
    private async Task ChonKhoangAsync(string ma)
    {
        var homNay = DateTime.Today;
        (TuNgay, DenNgay) = ma switch
        {
            "HOM_NAY" => (homNay, homNay),
            "7_NGAY" => (homNay.AddDays(-6), homNay),
            "THANG" => (new DateTime(homNay.Year, homNay.Month, 1), homNay),
            "NAM" => (new DateTime(homNay.Year, 1, 1), homNay),
            _ => (TuNgay, DenNgay)
        };
        await TaiBaoCaoAsync();
    }

    [RelayCommand] private void ChonTab(string tab) => TabDangChon = tab;

    [RelayCommand]
    private async Task TaiBaoCaoAsync()
    {
        if (TuNgay.Date > DenNgay.Date)
        {
            ThongBao = "Từ ngày không được lớn hơn đến ngày.";
            return;
        }
        if ((DenNgay.Date - TuNgay.Date).TotalDays + 1 > 366)
        {
            ThongBao = "Khoảng báo cáo tối đa 366 ngày.";
            return;
        }

        var lan = ++_lanTai;
        DangTai = true;
        try
        {
            var tu = DateOnly.FromDateTime(TuNgay);
            var den = DateOnly.FromDateTime(DenNgay);
            var tongQuan = await _api.GetTongQuanAsync(tu, den);
            var doanhThu = await _api.GetDoanhThuChiTietAsync(tu, den);
            var datPhong = await _api.GetBaoCaoDatPhongAsync(tu, den);
            var congNo = await _api.GetCongNoAsync();
            if (lan != _lanTai) return;
            CapNhatLuc = $"Số liệu tính lúc {tongQuan.CapNhatLuc:HH:mm:ss dd/MM/yyyy}";
            SoLieuCu = false;

            TongQuan = tongQuan;
            DoanhThu = doanhThu;
            DatPhong = datPhong;
            CongNo = congNo;
            KyDangXem = $"Kỳ này: {tu:dd/MM/yyyy} - {den:dd/MM/yyyy} ({tongQuan.KyNay.SoNgay} ngày) · so với kỳ trước: {tongQuan.KyTruoc.TuNgay:dd/MM/yyyy} - {tongQuan.KyTruoc.DenNgay:dd/MM/yyyy}";
            DungKpi(tongQuan);
            DungBieuDo();
            ThongBao = ""; ThongBaoLaLoi = true;
        }
        catch (LoiYeuCauApi loi)
        {
            // Giữ số liệu cũ đang hiển thị nhưng báo rõ là có thể đã cũ.
            ThongBao = loi.Message + (TongQuan is null ? "" : " - đang hiển thị số liệu tải lần trước.");
            SoLieuCu = TongQuan is not null;
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ThongBao = $"Không kết nối được máy chủ: {e.Message}" + (TongQuan is null ? "" : " - đang hiển thị số liệu tải lần trước.");
            SoLieuCu = TongQuan is not null;
        }
        finally
        {
            if (lan == _lanTai) DangTai = false;
        }
    }

    private void DungKpi(TongQuanBaoCaoDto bc)
    {
        var ky = bc.KyNay;
        var dt = bc.ThayDoi;
        TheKpi The(string tieu, string giaTri, decimal? thayDoi, string ghiChu = "")
            => new(tieu, giaTri, thayDoi is null ? "Chưa có kỳ trước để so sánh" : $"{(thayDoi >= 0 ? "▲" : "▼")} {Math.Abs(thayDoi.Value):0.#}% so với kỳ trước", thayDoi is not null, (thayDoi ?? 0) >= 0, ghiChu);

        Kpis.Clear();
        Kpis.Add(The("DOANH THU (HÓA ĐƠN)", $"{ky.DoanhThu:N0}đ", dt.DoanhThu, $"Dịch vụ: {ky.DoanhThuDichVu:N0}đ"));
        Kpis.Add(The("DOANH THU PHÒNG (THEO ĐÊM Ở)", $"{ky.DoanhThuPhong:N0}đ", dt.DoanhThuPhong, $"{ky.SoDemPhongBan:N0} đêm phòng đã bán"));
        Kpis.Add(The("CÔNG SUẤT (OCCUPANCY)", $"{ky.CongSuatPhanTram:0.#}%", dt.CongSuat, $"{ky.SoDemPhongBan:N0} / {ky.SoDemPhongKhaDung:N0} đêm phòng"));
        Kpis.Add(The("ADR", $"{ky.Adr:N0}đ", dt.Adr, "Doanh thu phòng ÷ đêm phòng bán"));
        Kpis.Add(The("REVPAR", $"{ky.RevPar:N0}đ", dt.RevPar, "Doanh thu phòng ÷ đêm phòng khả dụng"));
        Kpis.Add(The("ĐẶT PHÒNG MỚI", $"{ky.SoDatPhongMoi:N0}", dt.SoDatPhongMoi, $"Hủy {ky.TyLeHuyPhanTram:0.#}% · không đến {ky.TyLeKhongDenPhanTram:0.#}%"));
        Kpis.Add(new TheKpi("CHECK-IN / CHECK-OUT", $"{ky.SoCheckIn:N0} / {ky.SoCheckOut:N0}", "", false, true, $"Hủy {ky.SoHuy} · không đến {ky.SoKhongDen}"));
    }

    private void DungBieuDo()
    {
        Cots.Clear();
        if (TongQuan is null) return;

        Func<DiemChuoiDto, double> lay = ChiSoBieuDo switch
        {
            "DOANH_THU_PHONG" => x => (double)x.DoanhThuPhong,
            "CONG_SUAT" => x => (double)x.CongSuatPhanTram,
            "ADR" => x => (double)x.Adr,
            "REVPAR" => x => (double)x.RevPar,
            "DAT_PHONG" => x => x.SoDatPhongMoi,
            _ => x => (double)x.DoanhThu
        };
        string dinhDang(double v) => ChiSoBieuDo switch
        {
            "CONG_SUAT" => $"{v:0.#}%",
            "DAT_PHONG" => $"{v:0}",
            _ => $"{v:N0}đ"
        };

        var nay = TongQuan.ChuoiKyNay;
        var truoc = TongQuan.ChuoiKyTruoc;
        var lonNhat = Math.Max(nay.Select(lay).DefaultIfEmpty(0).Max(), truoc.Select(lay).DefaultIfEmpty(0).Max());
        var buoc = Math.Max(1, (int)Math.Ceiling(nay.Count / 12.0));   // tối đa ~12 nhãn trên trục
        for (var i = 0; i < nay.Count; i++)
        {
            var giaTriNay = lay(nay[i]);
            var giaTriTruoc = i < truoc.Count ? lay(truoc[i]) : 0;
            Cots.Add(new CotBieuDo(nay[i].Nhan, i % buoc == 0,
                lonNhat <= 0 ? 0 : Math.Max(giaTriNay > 0 ? 2 : 0, giaTriNay / lonNhat * ChieuCaoToiDa),
                lonNhat <= 0 ? 0 : Math.Max(giaTriTruoc > 0 ? 2 : 0, giaTriTruoc / lonNhat * ChieuCaoToiDa),
                $"{nay[i].Nhan}: {dinhDang(giaTriNay)}" + (i < truoc.Count ? $"\nKỳ trước ({truoc[i].Nhan}): {dinhDang(giaTriTruoc)}" : "")));
        }
        var tong = nay.Sum(lay);
        var chiSoTrungBinh = ChiSoBieuDo switch { "CONG_SUAT" => (double)TongQuan.KyNay.CongSuatPhanTram, "ADR" => (double)TongQuan.KyNay.Adr, "REVPAR" => (double)TongQuan.KyNay.RevPar, _ => 0 };
        TomTatBieuDo = ChiSoBieuDo is "CONG_SUAT" or "ADR" or "REVPAR" ? $"Cả kỳ: {dinhDang(chiSoTrungBinh)}" : $"Tổng kỳ này: {dinhDang(tong)}";
    }
}
