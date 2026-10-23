using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace KLCN060.Desktop.Services;

// Các lời gọi API dành cho Quản lý điều hành: bảng điều hành, báo cáo, nhật ký, bảng giá, nhân viên, sao lưu.
public sealed partial class ApiClient
{
    // ---------------- Bảng điều hành / báo cáo / cảnh báo ----------------
    public Task<BangDieuHanhDto> GetBangDieuHanhAsync() => GetAsync<BangDieuHanhDto>("api/v1/reports/dashboard");
    public Task<BaoCaoCanhBaoDto> GetCanhBaoAsync() => GetAsync<BaoCaoCanhBaoDto>("api/v1/reports/alerts");
    public Task<TongQuanBaoCaoDto> GetTongQuanAsync(DateOnly tu, DateOnly den)
        => GetAsync<TongQuanBaoCaoDto>($"api/v1/reports/overview?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<DoanhThuChiTietDto> GetDoanhThuChiTietAsync(DateOnly tu, DateOnly den)
        => GetAsync<DoanhThuChiTietDto>($"api/v1/reports/revenue-breakdown?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<VanHanhBaoCaoDto> GetVanHanhAsync(DateOnly tu, DateOnly den)
        => GetAsync<VanHanhBaoCaoDto>($"api/v1/reports/operations?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<BaoCaoDatPhongDto> GetBaoCaoDatPhongAsync(DateOnly tu, DateOnly den)
        => GetAsync<BaoCaoDatPhongDto>($"api/v1/reports/bookings?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<CongNoBaoCaoDto> GetCongNoAsync() => GetAsync<CongNoBaoCaoDto>("api/v1/reports/debts");

    // ---------------- Đặt phòng ----------------
    public Task<List<PhieuDatTomTatDto>> TimPhieuDatNangCaoAsync(string tuKhoa, string? trangThai, string? nguon, string? loai, bool chiQuaHan, string? locTheoNgay = null, DateTime? tuNgay = null, DateTime? denNgay = null)
    {
        var url = $"api/v1/bookings?pageSize=100&q={Uri.EscapeDataString(tuKhoa)}";
        if (!string.IsNullOrEmpty(trangThai)) url += "&trangThai=" + Uri.EscapeDataString(trangThai);
        if (!string.IsNullOrEmpty(nguon)) url += "&nguon=" + Uri.EscapeDataString(nguon);
        if (!string.IsNullOrEmpty(loai)) url += "&loaiDatPhong=" + Uri.EscapeDataString(loai);
        if (chiQuaHan) url += "&quaHanChuaNhan=true";
        if (tuNgay is not null || denNgay is not null)
        {
            if (!string.IsNullOrEmpty(locTheoNgay)) url += "&locTheoNgay=" + Uri.EscapeDataString(locTheoNgay);
            if (tuNgay is { } t) url += "&tuNgay=" + t.ToString("yyyy-MM-dd");
            if (denNgay is { } d) url += "&denNgay=" + d.ToString("yyyy-MM-dd");
        }
        return GetAsync<List<PhieuDatTomTatDto>>(url);
    }
    public Task<ChiTietPhieuDatDto> DanhDauKhongDenAsync(string maPhieuDat, string? lyDo)
        => PostAsync<object, ChiTietPhieuDatDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/no-show", new { lyDo });
    public Task<TongQuanPhieuDatDto> GetTongQuanPhieuDatAsync(string maPhieuDat)
        => GetAsync<TongQuanPhieuDatDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/overview");

    // ---------------- Phòng ----------------

    // ---------------- Nhật ký ----------------
    private static string LocNhatKy(DateOnly? tu, DateOnly? den, string? taiKhoan, string? nhom, string tuKhoa, string? ketQua, string? nguon, string? doiTuong)
    {
        var url = $"q={Uri.EscapeDataString(tuKhoa ?? "")}";
        if (tu is not null) url += $"&tuNgay={tu:yyyy-MM-dd}";
        if (den is not null) url += $"&denNgay={den:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(taiKhoan)) url += "&taiKhoan=" + Uri.EscapeDataString(taiKhoan.Trim());
        if (!string.IsNullOrEmpty(nhom)) url += "&nhom=" + Uri.EscapeDataString(nhom);
        if (!string.IsNullOrEmpty(ketQua)) url += "&ketQua=" + Uri.EscapeDataString(ketQua);
        if (!string.IsNullOrEmpty(nguon)) url += "&nguon=" + Uri.EscapeDataString(nguon);
        if (!string.IsNullOrWhiteSpace(doiTuong)) url += "&doiTuong=" + Uri.EscapeDataString(doiTuong.Trim());
        return url;
    }
    public Task<KetQuaTrangDto<NhatKyDto>> GetNhatKyAsync(DateOnly? tu, DateOnly? den, string? taiKhoan, string? nhom, string tuKhoa, int trang,
        string? ketQua = null, string? nguon = null, string? doiTuong = null)
        => GetTrangAsync<NhatKyDto>($"api/v1/audit-logs?pageSize=50&page={trang}&" + LocNhatKy(tu, den, taiKhoan, nhom, tuKhoa, ketQua, nguon, doiTuong));
    public Task<NhatKyLienQuanDto> GetNhatKyLienQuanAsync(long maNhatKy) => GetAsync<NhatKyLienQuanDto>($"api/v1/audit-logs/{maNhatKy}/related");
    public Task<ToanVenNhatKyDto> KiemTraToanVenNhatKyAsync() => GetAsync<ToanVenNhatKyDto>("api/v1/audit-logs/integrity");
    /// <summary>Xuất nhật ký ra Excel (.xlsx) theo bộ lọc (cần quyền XUAT_NHAT_KY; việc xuất được ghi lại).</summary>
    public async Task<(byte[] NoiDung, string TenFile)> XuatNhatKyAsync(DateOnly? tu, DateOnly? den, string? taiKhoan, string? nhom, string tuKhoa, string? ketQua, string? nguon, string? doiTuong)
    {
        var duongDan = "api/v1/audit-logs/export?dinhDang=xlsx&" + LocNhatKy(tu, den, taiKhoan, nhom, tuKhoa, ketQua, nguon, doiTuong);
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return (await responseMoi.Content.ReadAsByteArrayAsync(), responseMoi.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"KLCN060Hotel_NhatKyHoatDong_{DateTime.Now:dd.MM.yyyy_HHmm}.xlsx");
        }
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"KLCN060Hotel_NhatKyHoatDong_{DateTime.Now:dd.MM.yyyy_HHmm}.xlsx");
    }
    public Task<List<NhomNhatKyDto>> GetNhomNhatKyAsync() => GetAsync<List<NhomNhatKyDto>>("api/v1/audit-logs/groups");

    // ---------------- Bảng giá ----------------
    public Task<List<QuyTacGiaDto>> GetQuyTacGiaAsync() => GetAsync<List<QuyTacGiaDto>>("api/v1/rates");
    public Task<QuyTacGiaDto> TaoQuyTacGiaAsync(YeuCauQuyTacGia x) => PostAsync<YeuCauQuyTacGia, QuyTacGiaDto>("api/v1/rates", x);
    public Task<QuyTacGiaDto> SuaQuyTacGiaAsync(int ma, YeuCauQuyTacGia x) => PutAsync<YeuCauQuyTacGia, QuyTacGiaDto>($"api/v1/rates/{ma}", x);
    public Task XoaQuyTacGiaAsync(int ma) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/rates/{ma}");
    public Task<List<LichSuQuyTacGiaDto>> GetLichSuQuyTacGiaAsync(int? ma) => GetAsync<List<LichSuQuyTacGiaDto>>("api/v1/rates/history" + (ma is null ? "" : $"?maQuyTac={ma}"));
    public Task<GiaChotDatPhongDto> GetGiaChotDatPhongAsync(string maPhieuDat) => GetAsync<GiaChotDatPhongDto>($"api/v1/rates/bookings/{Uri.EscapeDataString(maPhieuDat.Trim())}");
    public Task<BaoGiaDto> GetBaoGiaAsync(string maLoai, DateOnly nhan, DateOnly tra, string loaiDatPhong)
        => GetAsync<BaoGiaDto>($"api/v1/rates/quote?maLoai={Uri.EscapeDataString(maLoai)}&checkin={nhan:yyyy-MM-dd}&checkout={tra:yyyy-MM-dd}&loaiDatPhong={loaiDatPhong}");

    // ---------------- Nhân viên ----------------
    public Task<NhanVienDto> TaoNhanVienAsync(YeuCauTaoNhanVien x) => PostAsync<YeuCauTaoNhanVien, NhanVienDto>("api/v1/employees", x);
    public Task<NhanVienDto> SuaNhanVienAsync(string maNV, YeuCauSuaNhanVien x)
        => PutAsync<YeuCauSuaNhanVien, NhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}", x);
    public Task<KetQuaTaiKhoanNhanVienDto> KhoaNhanVienAsync(string maNV, string? lyDo = null)
        => PostAsync<YeuCauLyDoTaiKhoan, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}/lock", new YeuCauLyDoTaiKhoan(lyDo));

    // ---------------- Sao lưu ----------------
    public Task<TinhTrangSaoLuuDto> GetTinhTrangSaoLuuAsync() => GetAsync<TinhTrangSaoLuuDto>("api/v1/system/backups/status");
    public Task<KetQuaKiemTraSaoLuuDto> KiemTraSaoLuuAsync(int maLichSu)
        => PostAsync<object, KetQuaKiemTraSaoLuuDto>($"api/v1/system/backups/{maLichSu}/verify", new { });
    public Task<KetQuaKiemTraSaoLuuDto> KiemTraFileSaoLuuAsync(string duongDanFile)
        => PostAsync<object, KetQuaKiemTraSaoLuuDto>("api/v1/system/backups/verify-file", new { duongDanFile });

    // Danh sách có phân trang: envelope { data: [...], meta: { page, totalPages, totalItems } }.
    private async Task<KetQuaTrangDto<T>> GetTrangAsync<T>(string url)
    {
        using var request = Create(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, url);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            return await DocTrangAsync<T>(responseMoi);
        }
        return await DocTrangAsync<T>(response);
    }

    private async Task<KetQuaTrangDto<T>> DocTrangAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw await LoiTuPhanHoiAsync(response);
        var data = await response.Content.ReadFromJsonAsync<PhanHoiTrang<T>>(JsonOptions);
        if (data?.Success != true || data.Data is null) throw new LoiYeuCauApi(response.StatusCode, "API khong tra du lieu.");
        return new KetQuaTrangDto<T> { DanhSach = data.Data, TongSo = data.Meta?.TotalItems ?? data.Data.Count, TongSoTrang = Math.Max(1, data.Meta?.TotalPages ?? 1) };
    }
}

public sealed class PhanHoiTrang<T> { public bool Success { get; init; } public List<T>? Data { get; init; } public MetaTrang? Meta { get; init; } }
public sealed class MetaTrang { public int TotalItems { get; init; } public int TotalPages { get; init; } }
public sealed class KetQuaTrangDto<T> { public List<T> DanhSach { get; init; } = []; public int TongSo { get; init; } public int TongSoTrang { get; init; } = 1; }

// ---------------- Bảng điều hành ----------------
public sealed class BangDieuHanhDto
{
    public DateTime ThoiDiem { get; init; }
    public DhPhongDto Phong { get; init; } = new();
    public DhHomNayDto HomNay { get; init; } = new();
    public DhDoanhThuDto DoanhThu { get; init; } = new();
    public DhCongNoDto CongNo { get; init; } = new();
    public DhCanhBaoDto CanhBao { get; init; } = new();
    public List<DhTangDto> TheoTang { get; init; } = [];
    public DhHomQuaDto HomQua { get; init; } = new();
    public DhViecHomNayDto ViecHomNay { get; init; } = new();
    public List<DhDuBaoDto> DuBao { get; init; } = [];
    public DhDoiChieuDto DoiChieu { get; init; } = new();
}
public sealed class DhHomQuaDto { public decimal DoanhThu { get; init; } public decimal CongSuatPhanTram { get; init; } public decimal Adr { get; init; } public int SoDemPhongBan { get; init; } }
public sealed class DhViecDto
{
    public string Ma { get; init; } = ""; public string TieuDe { get; init; } = ""; public string ChiTiet { get; init; } = ""; public bool CanChuY { get; init; } public string ManHinh { get; init; } = "";
}
public sealed class DhViecHomNayDto
{
    public List<DhViecDto> KhachDenChuaNhan { get; init; } = []; public int TongKhachDenChuaNhan { get; init; }
    public List<DhViecDto> KhachTraChuaTra { get; init; } = []; public int TongKhachTraChuaTra { get; init; }
}
public sealed class DhDuBaoDto
{
    public DateOnly Ngay { get; init; } public int PhongDaGiu { get; init; } public int KhachDen { get; init; } public int KhachTra { get; init; } public decimal CongSuatDuKienPhanTram { get; init; }
    public string Dong => $"{Ngay:ddd dd/MM}: giữ {PhongDaGiu} phòng ({CongSuatDuKienPhanTram:0.#}%) · đến {KhachDen} · trả {KhachTra}";
}
public sealed class DhDoiChieuDto
{
    public int TongSoPhep { get; init; } public int SoKhop { get; init; } public List<KiemTraNhatQuanDto> Phep { get; init; } = [];
    public bool TatCaKhop => SoKhop == TongSoPhep;
}
public sealed class DhPhongDto
{
    public int TongSoPhong { get; init; }
    public int DangCoKhach { get; init; }
    public int Trong { get; init; }
    public int DangDon { get; init; }
    public int BaoTri { get; init; }
    public decimal CongSuatPhanTram { get; init; }
}
public sealed class DhHomNayDto
{
    public int CheckInDuKien { get; init; }
    public int CheckInDaLam { get; init; }
    public int CheckOutDuKien { get; init; }
    public int CheckOutDaLam { get; init; }
    public int BookingMoi { get; init; }
    public int BookingHuy { get; init; }
}
public sealed class DhDoanhThuDto
{
    public decimal HomNay { get; init; }
    public decimal ThangNay { get; init; }
    public decimal CungKyThangTruoc { get; init; }
    public decimal? ThayDoiSoVoiThangTruocPhanTram { get; init; }
    public decimal CongSuatThangPhanTram { get; init; }
    public decimal AdrThang { get; init; }
    public decimal RevParThang { get; init; }
    public decimal CongSuatHomNayPhanTram { get; init; }
    public decimal AdrHomNay { get; init; }
    public decimal RevParHomNay { get; init; }
}
public sealed class DhCongNoDto
{
    public int SoHoaDonConNo { get; init; }
    public decimal TongConNo { get; init; }
    public int SoHoaDonQuaHan { get; init; }
    public decimal TongQuaHan { get; init; }
}
public sealed class DhCanhBaoDto
{
    public int NghiemTrong { get; init; }
    public int CanhBao { get; init; }
    public int ThongTin { get; init; }
    public int Tong => NghiemTrong + CanhBao + ThongTin;
}
public sealed class DhTangDto
{
    public int Tang { get; init; }
    public int TongSoPhong { get; init; }
    public int DangCoKhach { get; init; }
    public int Trong { get; init; }
    public int DangDon { get; init; }
    public int BaoTri { get; init; }
    public string TenTang => $"Tầng {Tang}";
}

// ---------------- Cảnh báo ----------------
public sealed class CanhBaoDto
{
    public string Ma { get; init; } = "";
    public string MucDo { get; init; } = "";
    public string TieuDe { get; init; } = "";
    public string MoTa { get; init; } = "";
    public int SoLuong { get; init; }
    public string ManHinh { get; init; } = "";
    public List<string> ChiTiet { get; init; } = [];
    public string AnhHuong { get; init; } = "";
    public string NguoiXuLyMacDinh { get; init; } = "";
    public string CachXuLy { get; init; } = "";
    public string HanhDongDeXuat { get; init; } = "";
    public DateTime? HanXuLy { get; init; }
    public bool QuaHan { get; init; }
    public DateTime? PhatHienLuc { get; init; }
    public string TrangThaiXuLy { get; init; } = "CHUA_NHAN";
    public string? NguoiNhan { get; init; }
    public string? HoTenNguoiNhan { get; init; }
    public DateTime? NhanLuc { get; init; }
    public string? GhiChuNhan { get; init; }
    public string NguoiXuLyHienThi => TrangThaiXuLy == "DANG_XU_LY" ? $"Đang xử lý: {HoTenNguoiNhan ?? NguoiNhan}" + (string.IsNullOrEmpty(GhiChuNhan) ? "" : $" ({GhiChuNhan})") : $"Chưa ai nhận · thường do: {NguoiXuLyMacDinh}";
    public string HanHienThi => HanXuLy is { } h ? $"Hạn {h:dd/MM HH:mm}" + (QuaHan ? " - QUÁ HẠN" : "") : "";
    public string PhatHienHienThi => PhatHienLuc is { } p ? $"Phát hiện {p:dd/MM HH:mm}" : "";
    public bool DangXuLy => TrangThaiXuLy == "DANG_XU_LY";
    public string MucDoHienThi => MucDo switch { "NGHIEM_TRONG" => "Nghiêm trọng", "CANH_BAO" => "Cảnh báo", _ => "Thông tin" };
    public string ChiTietGop => string.Join("\n", ChiTiet.Select(x => "• " + x)) + (SoLuong > ChiTiet.Count ? $"\n… và {SoLuong - ChiTiet.Count} mục khác" : "");
    public string TieuDeKemSo => $"{TieuDe} ({SoLuong})";
}
public sealed class BaoCaoCanhBaoDto
{
    public DateTime ThoiDiem { get; init; }
    public int NghiemTrong { get; init; }
    public int CanhBao { get; init; }
    public int ThongTin { get; init; }
    public List<CanhBaoDto> CanhBaoDangCo { get; init; } = [];
    public List<CanhBaoDaXuLyDto> DaXuLyGanDay { get; init; } = [];
    public int QuaHan { get; init; }
    public int ChuaNhan { get; init; }
}
public sealed class CanhBaoDaXuLyDto
{
    public string Ma { get; init; } = ""; public string TieuDe { get; init; } = ""; public string MucDo { get; init; } = ""; public DateTime PhatHienLuc { get; init; }
    public DateTime DaXuLyLuc { get; init; } public double SoGioXuLy { get; init; } public string? NguoiNhan { get; init; } public string? HoTenNguoiNhan { get; init; }
    public string Dong => $"✔ {TieuDe} - hết lúc {DaXuLyLuc:dd/MM HH:mm} (sau {SoGioXuLy:0.#} giờ)" + (NguoiNhan is null ? "" : $" · người nhận: {HoTenNguoiNhan ?? NguoiNhan}");
}

// ---------------- Báo cáo ----------------
public sealed class TongQuanKyDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public int SoNgay { get; init; }
    public int TongSoPhong { get; init; }
    public decimal DoanhThu { get; init; }
    public decimal DoanhThuPhong { get; init; }
    public decimal DoanhThuDichVu { get; init; }
    public int SoDemPhongBan { get; init; }
    public int SoDemPhongKhaDung { get; init; }
    public decimal CongSuatPhanTram { get; init; }
    public decimal Adr { get; init; }
    public decimal RevPar { get; init; }
    public int SoDatPhongMoi { get; init; }
    public int SoHuy { get; init; }
    public int SoKhongDen { get; init; }
    public decimal TyLeHuyPhanTram { get; init; }
    public decimal TyLeKhongDenPhanTram { get; init; }
    public int SoCheckIn { get; init; }
    public int SoCheckOut { get; init; }
}
public sealed class ThayDoiKyDto
{
    public decimal? DoanhThu { get; init; }
    public decimal? DoanhThuPhong { get; init; }
    public decimal? CongSuat { get; init; }
    public decimal? Adr { get; init; }
    public decimal? RevPar { get; init; }
    public decimal? SoDatPhongMoi { get; init; }
}
public sealed class DiemChuoiDto
{
    public DateOnly Ngay { get; init; }
    public string Nhan { get; init; } = "";
    public decimal DoanhThu { get; init; }
    public decimal DoanhThuPhong { get; init; }
    public decimal CongSuatPhanTram { get; init; }
    public decimal Adr { get; init; }
    public decimal RevPar { get; init; }
    public int SoDatPhongMoi { get; init; }
}
public sealed class TongQuanBaoCaoDto
{
    public DateTime CapNhatLuc { get; init; }
    public TongQuanKyDto KyNay { get; init; } = new();
    public TongQuanKyDto KyTruoc { get; init; } = new();
    public ThayDoiKyDto ThayDoi { get; init; } = new();
    public string NhomTheo { get; init; } = "NGAY";
    public List<DiemChuoiDto> ChuoiKyNay { get; init; } = [];
    public List<DiemChuoiDto> ChuoiKyTruoc { get; init; } = [];
}
public sealed class PhanChiaDto
{
    public string Ma { get; init; } = "";
    public string Ten { get; init; } = "";
    public decimal DoanhThu { get; init; }
    public int SoLuong { get; init; }
    public decimal TyTrongPhanTram { get; init; }
}
public sealed class DoanhThuChiTietDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public decimal TongDoanhThu { get; init; }
    public List<DoanhThuTheoNgayDto> TheoNgay { get; init; } = [];
    public List<PhanChiaDto> TheoThang { get; init; } = [];
    public List<PhanChiaDto> TheoLoaiPhong { get; init; } = [];
    public List<PhanChiaDto> TheoNguonDat { get; init; } = [];
    public List<PhanChiaDto> TheoDichVu { get; init; } = [];
    public List<PhanChiaDto> TheoHinhThucThanhToan { get; init; } = [];

    // Các chiều phân tích của kế toán (API gửi cùng báo cáo doanh thu)
    public List<PhanChiaDto> TheoHinhThucGomCoc { get; init; } = [];
    public List<PhanChiaDto> TheoLoaiDoanhThu { get; init; } = [];
    public List<PhanChiaDto> TheoTuan { get; init; } = [];
    public List<PhanChiaDto> TheoQuy { get; init; } = [];
    public List<PhanChiaDto> TheoNam { get; init; } = [];
    public List<PhanChiaDto> TheoPhong { get; init; } = [];
    public List<PhanChiaDto> TheoTang { get; init; } = [];
}
public sealed class PhongCanXuLyDto
{
    public string MaPhong { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public DateTime? TuLuc { get; init; }
    public double? SoGio { get; init; }
    public string? LyDo { get; init; }
    public string ThoiGianHienThi => SoGio is null ? "" : SoGio >= 48 ? $"{SoGio / 24:0.#} ngày" : $"{SoGio:0.#} giờ";
    public string TinhTrangHienThi => TinhTrang == "OOO" ? "Bảo trì" : "Chưa dọn";
}
public sealed class VanHanhBaoCaoDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public VhLeTanDto LeTan { get; init; } = new();
    public VhBuongPhongDto BuongPhong { get; init; } = new();
    public VhKeToanDto KeToan { get; init; } = new();
    public VhBaoTriDto BaoTri { get; init; } = new();
}
public sealed class VhLeTanDto
{
    public int CheckIn { get; init; }
    public int CheckOut { get; init; }
    public int NoShow { get; init; }
    public int Huy { get; init; }
    public int KhachVangLai { get; init; }
    public int DatPhongMoi { get; init; }
    public decimal CongSuatTrungBinhPhanTram { get; init; }
}
public sealed class VhBuongPhongDto
{
    public int PhongDaDon { get; init; }
    public int PhongDangCho { get; init; }
    public int PhongCoVanDe { get; init; }
    public List<PhongCanXuLyDto> DanhSachCanXuLy { get; init; } = [];
}
public sealed class VhKeToanDto
{
    public decimal DoanhThu { get; init; }
    public decimal ThanhToan { get; init; }
    public int SoLanThanhToan { get; init; }
    public decimal CongNoHienTai { get; init; }
    public int SoHoaDonConNo { get; init; }
    public decimal HoanTien { get; init; }
    public int SoLanHoanTien { get; init; }
    public decimal CocGiuLai { get; init; }
}
public sealed class VhBaoTriDto
{
    public int SoSuCo { get; init; }
    public int DangXuLy { get; init; }
    public int DaXuLy { get; init; }
    public double? ThoiGianXuLyTrungBinhGio { get; init; }
    public List<PhongCanXuLyDto> PhongDangBaoTri { get; init; } = [];
}
public sealed class DemDatPhongDto
{
    public string Ma { get; init; } = "";
    public string Ten { get; init; } = "";
    public int SoLuong { get; init; }
    public decimal GiaTri { get; init; }
}
public sealed class DongHuyDto
{
    public string MaPhieuDat { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public DateTime? NgayHuy { get; init; }
    public string? LyDoHuy { get; init; }
    public string? HuyBoi { get; init; }
    public decimal TienCoc { get; init; }
    public decimal SoTienHoanCoc { get; init; }
    public string LoaiHienThi => TrangThai == "KHONG_DEN" ? "Không đến" : "Hủy";
}
public sealed class BaoCaoDatPhongDto
{
    public int TongSoPhieu { get; init; }
    public List<DemDatPhongDto> TheoTrangThai { get; init; } = [];
    public List<DemDatPhongDto> TheoNguon { get; init; } = [];
    public List<DemDatPhongDto> TheoLoai { get; init; } = [];
    public int SoPhieuDoan { get; init; }
    public int SoPhongDoan { get; init; }
    public decimal TyLeHuyPhanTram { get; init; }
    public decimal TyLeKhongDenPhanTram { get; init; }
    public decimal TongCocHoan { get; init; }
    public decimal TongCocGiuLai { get; init; }
    public int ChoXuLyKhongDen { get; init; }
    public List<DongHuyDto> HuyGanDay { get; init; } = [];
}
public sealed class DongNhomCongNoDto
{
    public string Ma { get; init; } = "";
    public string? HoTenKhach { get; init; }
    public int SoHoaDon { get; init; }
    public decimal ConNo { get; init; }
    public int SoNgayNoLonNhat { get; init; }
}
public sealed class DongCongNoDto
{
    public string MaHD { get; init; } = "";
    public string? HoTenKhach { get; init; }
    public string? MaPhieuDat { get; init; }
    public string? MaKhach { get; init; }
    public DateTime NgayLap { get; init; }
    public int SoNgayNo { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThanhToan { get; init; }
    public decimal ConNo { get; init; }
    public string NhomTuoiNo { get; init; } = "";
}
public sealed class CongNoBaoCaoDto
{
    public decimal TongConNo { get; init; }
    public int SoHoaDon { get; init; }
    public List<PhanChiaDto> TheoNhomTuoiNo { get; init; } = [];
    public List<DongCongNoDto> ChiTiet { get; init; } = [];
    public List<DongNhomCongNoDto> TheoKhach { get; init; } = [];
    public List<DongNhomCongNoDto> TheoDatPhong { get; init; } = [];
}

// ---------------- Phòng ----------------
// ---------------- Nhật ký ----------------
public sealed class NhatKyDto
{
    public long MaNhatKy { get; init; }
    public DateTime ThoiGian { get; init; }
    public string TaiKhoan { get; init; } = "";
    public string? HoTenNguoiThucHien { get; init; }
    public string? VaiTro { get; init; }
    public string HanhDong { get; init; } = "";
    public string MoTaHanhDong { get; init; } = "";
    public string Nhom { get; init; } = "";
    public string? DoiTuongTacDong { get; init; }
    public string? MaNguoiThucHien { get; init; }
    public string? ChiTiet { get; init; }
    public string KetQua { get; init; } = "THANH_CONG";
    public string? MaLoi { get; init; }
    public string NguonThucHien { get; init; } = "NGUOI_DUNG";
    public string? LyDo { get; init; }
    public List<NhatKyThayDoiDto> ThayDoi { get; init; } = [];
    public List<NhatKyThongTinDto> ThongTin { get; init; } = [];
    public List<string> DoiTuongLienQuan { get; init; } = [];
    public string KetQuaHienThi => KetQua switch { "THAT_BAI" => "Thất bại", "TU_CHOI" => "Bị từ chối", _ => "Thành công" };
    public string NguonHienThi => NguonThucHien == "HE_THONG" ? "Hệ thống" : "Người dùng";
    public bool KhongThanhCong => KetQua != "THANH_CONG";
    public string NguonGrid => NguonHienThi;
    public string KetQuaChiTiet => KetQua switch
    {
        "TU_CHOI" => "Bị từ chối: người dùng không đủ quyền" + (string.IsNullOrEmpty(MaLoi) ? "" : $" ({MaLoi})"),
        "THAT_BAI" => "Thất bại" + (string.IsNullOrEmpty(MaLoi) ? "" : $" ({MaLoi})"),
        _ => "Thành công"
    };
    public bool CoThayDoi => ThayDoi.Count > 0;
    public bool CoThongTin => ThongTin.Count > 0;
    public bool CoLyDo => !string.IsNullOrWhiteSpace(LyDo);
    public bool CoDoiTuongLienQuan => DoiTuongLienQuan.Count > 0;
    /// <summary>Chỉ họ tên người thực hiện (không kèm mã nhân viên / tên đăng nhập; tên đăng nhập xem ở khung chi tiết).</summary>
    public string NguoiHienThi => string.IsNullOrEmpty(HoTenNguoiThucHien) ? TaiKhoan : HoTenNguoiThucHien;

    private static readonly (string Tien, string Ten)[] LoaiMa =
    [
        ("BK", "Phiếu đặt phòng"), ("CI", "Phiếu nhận phòng"), ("HD", "Hóa đơn"), ("KH", "Khách hàng"), ("NV", "Nhân viên"), ("LP", "Loại phòng"),
        ("KM", "Khuyến mãi"), ("DV", "Dịch vụ"), ("GOI", "Gói dịch vụ"), ("VT", "Vật dụng"), ("SC", "Sự cố"),
        ("GD", "Giao dịch chuyển khoản"), ("CS", "Cơ sở vật chất"), ("TB", "Thiết bị"), ("QT", "Quy tắc giá")
    ];
    private static readonly (string Doan, string Ten)[] LoaiDuongDan =
    [
        ("bookings", "Phiếu đặt phòng"), ("check-ins", "Phiếu nhận phòng"), ("stays", "Lượt lưu trú"), ("invoices", "Hóa đơn"), ("payments", "Thanh toán"),
        ("guests", "Khách hàng"), ("rooms", "Phòng"), ("room-types", "Loại phòng"), ("services", "Dịch vụ"), ("promotions", "Khuyến mãi"),
        ("facilities", "Cơ sở vật chất"), ("housekeeping", "Buồng phòng"), ("frontdesk", "Bảng điều hành lễ tân"), ("users", "Tài khoản"), ("roles", "Vai trò"),
        ("auth", "Tài khoản đăng nhập"), ("reports", "Báo cáo"), ("system", "Hệ thống"), ("audit-logs", "Nhật ký hoạt động"),
        ("reconciliation", "Đối soát"), ("rates", "Bảng giá phòng"), ("alerts", "Cảnh báo")
    ];

    /// <summary>Đối tượng tác động dạng danh từ dễ hiểu: "Phiếu đặt phòng BK2026...", "Phòng 101", "Khách hàng KH05"... (thay cho mã trần hoặc đường dẫn API).</summary>
    public string DoiTuongHienThi
    {
        get
        {
            var v = DoiTuongTacDong?.Trim();
            if (string.IsNullOrEmpty(v)) return "—";
            if (v.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                var doan = v.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToList();   // bỏ "api", "v1"
                if (doan.Count == 0) return "Hệ thống";
                var ten = LoaiDuongDan.FirstOrDefault(x => x.Doan == doan[0].ToLowerInvariant()).Ten ?? doan[0];
                var ma = doan.Count > 1 && doan[1].Any(char.IsDigit) ? " " + doan[1] : "";
                return ten + ma;
            }
            if (v.Length > 1 && v[0] == 'P' && char.IsDigit(v[1])) return $"Phòng {v[1..]}";
            if (v.StartsWith("PLT", StringComparison.OrdinalIgnoreCase)) return $"Phòng {v[1..]}";
            foreach (var (tien, ten) in LoaiMa.OrderByDescending(x => x.Tien.Length))
                if (v.StartsWith(tien, StringComparison.OrdinalIgnoreCase) && v.Length > tien.Length && (char.IsDigit(v[tien.Length]) || v[tien.Length] == '-'))
                    return $"{ten} {v}";
            return v;
        }
    }
    public string VaiTroHienThi => VaiTro switch { "QUAN_LY" => "Quản lý", "LE_TAN" => "Lễ tân", "KE_TOAN" => "Kế toán", "BUONG_PHONG" => "Buồng phòng", "KHACH_HANG" => "Khách hàng", _ => VaiTro ?? "" };

    private static readonly System.Text.RegularExpressions.Regex NgayIso = new(@"\b(\d{4})-(\d{2})-(\d{2})\b", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static string DocGiaTri(string? v) => string.IsNullOrWhiteSpace(v) ? "(trống)" : NgayIso.Replace(v, "$3/$2/$1");

    /// <summary>Toàn bộ nội dung đã ghi nhận, mỗi ý một dòng, có nhãn tiếng Việt (thay cho chuỗi "khóa=giá trị; khóa=giá trị" gốc).</summary>
    public string DuLieuDeDoc
    {
        get
        {
            var dong = new List<string>();
            foreach (var t in ThongTin) dong.Add($"• {t.Truong}: {DocGiaTri(t.GiaTri)}");
            foreach (var t in ThayDoi) dong.Add($"• {t.Truong}: {DocGiaTri(t.Truoc)} → {DocGiaTri(t.Sau)}");
            if (!string.IsNullOrWhiteSpace(LyDo)) dong.Add($"• Lý do: {LyDo}");
            if (KetQua != "THANH_CONG" && !string.IsNullOrEmpty(MaLoi)) dong.Add($"• Mã lỗi: {MaLoi}");
            if (dong.Count == 0 && !string.IsNullOrWhiteSpace(ChiTiet)) dong.Add(NgayIso.Replace(ChiTiet!, "$3/$2/$1"));
            return dong.Count == 0 ? "Thao tác này không có nội dung ghi nhận thêm." : string.Join("\n", dong);
        }
    }
}
public sealed class NhomNhatKyDto { public string Ma { get; init; } = ""; public string Ten { get; init; } = ""; }
public sealed class NhatKyThayDoiDto
{
    public string Truong { get; init; } = ""; public string? Truoc { get; init; } public string? Sau { get; init; }
    public string TruocHienThi => string.IsNullOrEmpty(Truoc) ? "(trống)" : Truoc!;
    public string SauHienThi => string.IsNullOrEmpty(Sau) ? "(trống)" : Sau!;
}
public sealed class NhatKyThongTinDto { public string Truong { get; init; } = ""; public string GiaTri { get; init; } = ""; public string Dong => $"{Truong}: {GiaTri}"; }
public sealed class NhatKyTinhTrangPhongDto
{
    public string MaPhong { get; init; } = ""; public string TuTinhTrang { get; init; } = ""; public string DenTinhTrang { get; init; } = ""; public DateTime ThoiGian { get; init; }
    public string? NguoiThucHien { get; init; } public string? LyDo { get; init; }
    public string Dong => $"{ThoiGian:dd/MM HH:mm} · {TuTinhTrang} → {DenTinhTrang} · {NguoiThucHien ?? "hệ thống"}" + (string.IsNullOrEmpty(LyDo) ? "" : $" · {LyDo}");
}
public sealed class NhatKyLienQuanDto
{
    public long MaNhatKy { get; init; } public string? DoiTuong { get; init; }
    public List<NhatKyDto> ChuoiSuKien { get; init; } = [];
    public List<NhatKyTinhTrangPhongDto> LichSuTinhTrangPhong { get; init; } = [];
    public List<string> DoiTuongLienQuan { get; init; } = [];
}
public sealed class ToanVenNhatKyDto
{
    public int TongSoDong { get; init; } public int ChuKyHopLe { get; init; } public int ChuKySai { get; init; } public int ChuaCoChuKy { get; init; }
    public bool ToanVen { get; init; } public string KetLuan { get; init; } = ""; public List<long> MaDongSai { get; init; } = [];
}

// ---------------- Bảng giá ----------------
public sealed record YeuCauQuyTacGia(string TenQuyTac, string LoaiQuyTac, string? MaLoai, string KieuDieuChinh, decimal GiaTri, DateOnly? TuNgay, DateOnly? DenNgay,
    string? CacThuApDung, string? LoaiDatPhongApDung, int UuTien, bool HoatDong, string? GhiChu);
public sealed class QuyTacGiaDto
{
    public int MaQuyTac { get; init; }
    public string TenQuyTac { get; init; } = "";
    public string LoaiQuyTac { get; init; } = "";
    public string? MaLoai { get; init; }
    public string? TenLoai { get; init; }
    public string KieuDieuChinh { get; init; } = "";
    public decimal GiaTri { get; init; }
    public DateOnly? TuNgay { get; init; }
    public DateOnly? DenNgay { get; init; }
    public string? CacThuApDung { get; init; }
    public string? LoaiDatPhongApDung { get; init; }
    public int UuTien { get; init; }
    public bool HoatDong { get; init; }
    public string? GhiChu { get; init; }

    public string LoaiHienThi => LoaiQuyTac switch
    {
        "CUOI_TUAN" => "Cuối tuần", "MUA_CAO_DIEM" => "Mùa cao điểm", "LE_TET" => "Lễ / Tết", "DOANH_NGHIEP" => "Đoàn", _ => "Khác"
    };
    public string MucGiaHienThi => KieuDieuChinh == "PHAN_TRAM" ? $"{GiaTri:+0.##;-0.##}%" : $"{GiaTri:N0}đ/đêm";
    public string PhamViLoaiPhong => TenLoai ?? "Mọi loại phòng";
    public string ThoiGianHienThi
    {
        get
        {
            var parts = new List<string>();
            if (TuNgay is not null || DenNgay is not null) parts.Add($"{(TuNgay is null ? "…" : TuNgay.Value.ToString("dd/MM/yyyy"))} → {(DenNgay is null ? "…" : DenNgay.Value.ToString("dd/MM/yyyy"))}");
            if (!string.IsNullOrEmpty(CacThuApDung)) parts.Add("đêm " + string.Join(", ", CacThuApDung.Split(',').Select(TenThu)));
            if (LoaiDatPhongApDung is not null) parts.Add(LoaiDatPhongApDung == "DOAN" ? "phiếu ĐOÀN" : "phiếu cá nhân");
            return parts.Count == 0 ? "Mọi ngày" : string.Join(" · ", parts);
        }
    }
    public string HieuLuc { get; init; } = "DANG_AP_DUNG";
    public int SoDemDaChot { get; init; }
    public int SoPhieuDaChot { get; init; }
    public List<string> CanhBao { get; init; } = [];
    public string TrangThaiHienThi => HieuLuc switch { "TAT" => "Đang tắt", "CHUA_BAT_DAU" => "Chưa bắt đầu", "DA_HET_HAN" => "Đã hết hạn", _ => "Đang áp dụng" };
    public string DoiTuongHienThi => LoaiDatPhongApDung is null ? "Mọi loại đặt phòng" : LoaiDatPhongApDung == "DOAN" ? "Chỉ đặt đoàn" : "Chỉ đặt cá nhân";
    public string DaChotHienThi => SoDemDaChot == 0 ? "Chưa phiếu nào dùng" : $"{SoPhieuDaChot} phiếu · {SoDemDaChot} đêm đã chốt giá";
    public string CanhBaoGop => string.Join("\n", CanhBao);
    public bool CoCanhBao => CanhBao.Count > 0;
    private static string TenThu(string s) => s switch { "0" => "CN", "1" => "T2", "2" => "T3", "3" => "T4", "4" => "T5", "5" => "T6", "6" => "T7", _ => s };
}
public sealed class QuyTacKhopDto
{
    public int MaQuyTac { get; init; } public string TenQuyTac { get; init; } = ""; public int UuTien { get; init; } public string DieuChinh { get; init; } = ""; public bool DuocApDung { get; init; }
    public string HienThi => $"{(DuocApDung ? "✔" : "✖ bị thay thế")} {TenQuyTac} (ưu tiên {UuTien}, {DieuChinh})";
}
public sealed class DemBaoGiaDto
{
    public DateOnly Dem { get; init; } public decimal Gia { get; init; } public string? QuyTac { get; init; }
    public decimal GiaCoBan { get; init; } public string? DieuChinh { get; init; } public int? UuTien { get; init; }
    public List<QuyTacKhopDto> CacQuyTacKhop { get; init; } = [];
    public string QuyTacHienThi => QuyTac ?? "Giá cơ bản";
    public string QuyTacChiTiet => QuyTac is null ? "Giá cơ bản" : $"{QuyTac} ({DieuChinh})";
    public string ChenhLechHienThi => Gia == GiaCoBan ? "—" : $"{Gia - GiaCoBan:+#,##0;-#,##0}đ";
    public string GhiChuKhop => CacQuyTacKhop.Count <= 1 ? "" : string.Join(" | ", CacQuyTacKhop.Select(x => x.HienThi));
}
public sealed class LichSuQuyTacGiaDto
{
    public DateTime ThoiGian { get; init; } public string NguoiThucHien { get; init; } = ""; public string HanhDong { get; init; } = ""; public string MoTaHanhDong { get; init; } = "";
    public string? DoiTuong { get; init; } public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {MoTaHanhDong}" + (DoiTuong is null ? "" : $" ({DoiTuong})");
    public string ChiTietDoc => ChiTiet is null ? "" : ChiTiet.Replace("truoc=[", "TRƯỚC: ").Replace("]; sau=[", "\nSAU: ").TrimEnd(']').Replace("; ", " · ").Replace("tất cả", "tất cả");
}
public sealed class DemGiaChotDto
{
    public DateOnly Dem { get; init; } public decimal GiaCoBan { get; init; } public decimal Gia { get; init; } public string? TenQuyTac { get; init; } public string? DieuChinh { get; init; } public decimal GiaHienHanh { get; init; }
    public string QuyTacHienThi => TenQuyTac is null ? "Giá cơ bản" : $"{TenQuyTac} ({DieuChinh})";
    public string SoVoiHienHanh => GiaHienHanh == Gia ? "Bằng giá hiện hành" : $"Hiện hành {GiaHienHanh:N0}đ ({GiaHienHanh - Gia:+#,##0;-#,##0}đ)";
}
public sealed class PhongGiaChotDto
{
    public string MaPhong { get; init; } = ""; public string TenPhong { get; init; } = ""; public string TenLoaiPhong { get; init; } = "";
    public decimal DonGiaApDung { get; init; } public decimal ThanhTien { get; init; } public bool CoGiaTungDem { get; init; } public decimal TongTheoDem { get; init; } public decimal TongHienHanh { get; init; }
    public List<DemGiaChotDto> TheoDem { get; init; } = [];
    public string TieuDe => $"{TenPhong} ({TenLoaiPhong}): giá chốt {ThanhTien:N0}đ" + (CoGiaTungDem ? $" - bảng giá hiện hành sẽ cho {TongHienHanh:N0}đ" : $" = {DonGiaApDung:N0}đ × số đêm (phiếu cũ chưa lưu giá từng đêm)");
}
public sealed class GiaChotDatPhongDto
{
    public string MaPhieuDat { get; init; } = ""; public string LoaiDatPhong { get; init; } = ""; public DateOnly NgayDon { get; init; } public DateOnly NgayTra { get; init; } public int SoDem { get; init; }
    public decimal TongTienPhong { get; init; } public string QuyTacTinhLai { get; init; } = ""; public List<PhongGiaChotDto> Phong { get; init; } = [];
    public string TieuDe => $"{MaPhieuDat}: {NgayDon:dd/MM/yyyy} → {NgayTra:dd/MM/yyyy} ({SoDem} đêm, {(LoaiDatPhong == "DOAN" ? "đoàn" : "cá nhân")}) · tổng tiền phòng {TongTienPhong:N0}đ";
}
public sealed class BaoGiaDto
{
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public decimal DonGiaCoBan { get; init; }
    public int SoDem { get; init; }
    public decimal DonGiaBinhQuan { get; init; }
    public decimal TongTien { get; init; }
    public decimal TongCoBan { get; init; }
    public List<DemBaoGiaDto> TheoDem { get; init; } = [];
}

// ---------------- Nhân viên ----------------
public sealed record YeuCauTaoNhanVien(string HoTen, string SoDT, string ChucVu, int MaVaiTro, string TenDangNhap, string MatKhau);
public sealed record YeuCauSuaNhanVien(string HoTen, string SoDT, string ChucVu);

// ---------------- Sao lưu ----------------
public sealed class TinhTrangSaoLuuDto
{
    public string TinhTrang { get; init; } = "";
    public string TomTat { get; init; } = "";
    public DateTime? LanSaoLuuThanhCongCuoi { get; init; }
    public int? SoNgayTuLanSaoLuuCuoi { get; init; }
    public string? DuongDanBanSaoLuuCuoi { get; init; }
    public int TongSoBanSaoLuuThanhCong { get; init; }
    public DateTime? LanPhucHoiCuoi { get; init; }
    public DateTime? LanKiemTraCuoi { get; init; }
    public bool? LanKiemTraCuoiHopLe { get; init; }
    public bool LanSaoLuuCuoiThatBai { get; init; }
    public bool TuDongBat { get; init; }
    public int? ChuKyTuDongGio { get; init; }
    public DateTime? HanTuDongKeTiep { get; init; }
    public string TenCSDL { get; init; } = "";
    public DateTime CapNhatLuc { get; init; }
    public DateTime? LanSaoLuuCuoi { get; init; }
    public string? KetQuaLanSaoLuuCuoi { get; init; }
    public string? LoiLanSaoLuuCuoi { get; init; }
    public bool? BanCuoiDaKiemTra { get; init; }
    public long? BanCuoiKichThuocBytes { get; init; }
    public bool BanCuoiConFile { get; init; }
    public DateTime? LanTuDongThanhCongCuoi { get; init; }
    public DateTime? LanTuDongThatBaiCuoi { get; init; }
    public bool TuDongQuaHan { get; init; }
    public string? ThuMucSaoLuu { get; init; }
    public long? DungLuongTrongBytes { get; init; }
    public long? TongDungLuongCacBanBytes { get; init; }
    public long? KichThuocCSDLBytes { get; init; }
    public string MucDungLuong { get; init; } = "KHONG_RO";
    public string? GhiChuDungLuong { get; init; }
    public KhaNangPhucHoiDto KhaNangPhucHoi { get; init; } = new();
    public List<string> CanhBao { get; init; } = [];
}
public sealed class KhaNangPhucHoiDto
{
    public string MucDo { get; init; } = "KHONG_CO";
    public string KetLuan { get; init; } = "";
    public int? MaBanDeXuat { get; init; }
    public DateTime? ThoiDiemBanDeXuat { get; init; }
    public bool? BanDeXuatDaKiemTra { get; init; }
    public int? SoGioCoTheMatDuLieu { get; init; }
    public int SoThaoTacCoTheMat { get; init; }
    public List<string> CoCheBaoVe { get; init; } = [];
    public List<string> CanhBao { get; init; } = [];
}
public sealed class KetQuaKiemTraSaoLuuDto
{
    public bool HopLe { get; init; }
    public string DuongDanFile { get; init; } = "";
    public string ThongBao { get; init; } = "";
    public DateTime KiemTraLuc { get; init; }
    public string? TenCSDLTrongFile { get; init; }
    public DateTime? ThoiDiemSaoLuuTrongFile { get; init; }
    public long? KichThuocBytes { get; init; }
    public bool? DungCSDL { get; init; }
}
public sealed class XemTruocPhucHoiDto
{
    public string TenCSDL { get; init; } = "";
    public string DuongDanFile { get; init; } = "";
    public bool SanSang { get; init; }
    public string KetLuan { get; init; } = "";
    public KetQuaKiemTraSaoLuuDto KiemTra { get; init; } = new();
    public DateTime? ThoiDiemBanSaoLuu { get; init; }
    public int SoThaoTacSeMat { get; init; }
    public int SoDatPhongMoiSeMat { get; init; }
    public int SoHoaDonMoiSeMat { get; init; }
    public List<string> CanhBao { get; init; } = [];
}

// ---------------- Chi tiết phòng trên sơ đồ ----------------
public sealed class KhachDangOPhongDto
{
    public string HoTen { get; init; } = "";
    public string? SoDT { get; init; }
    public string MaPhieuNhan { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTraDuKien { get; init; }
    public int SoNguoi { get; init; }
    public string HienThi => $"{HoTen}{(string.IsNullOrEmpty(SoDT) ? "" : " · " + SoDT)} · {SoNguoi} người · nhận {NgayNhan:dd/MM}" + (NgayTraDuKien is null ? "" : $", dự kiến trả {NgayTraDuKien:dd/MM}")
        + (string.IsNullOrEmpty(MaPhieuDat) ? "" : $" · {MaPhieuDat}");
}
public sealed class DatPhongSapToiDto
{
    public string MaPhieuDat { get; init; } = "";
    public string HoTen { get; init; } = "";
    public string? SoDT { get; init; }
    public DateOnly NgayDon { get; init; }
    public DateOnly NgayTra { get; init; }
    public string TrangThai { get; init; } = "";
    public decimal TienCoc { get; init; }
    public string HienThi => $"{NgayDon:dd/MM} → {NgayTra:dd/MM} · {HoTen} · {MaPhieuDat} · {(TrangThai == "DA_XAC_NHAN" ? "đã xác nhận" : "chờ xác nhận")}";
}
public sealed class SuCoTomTatDto
{
    public int MaSuCo { get; init; }
    public string TieuDe { get; init; } = "";
    public string MucDo { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string HienThi => $"SC{MaSuCo:0000} · {TieuDe} · {SuCoPhongDto.TenMucDoSuCo(MucDo)}";
}
public sealed class ChiTietPhongDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public int Tang { get; init; }
    public string TenLoaiPhong { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public DateTime? TinhTrangTuLuc { get; init; }
    public string? LyDoTinhTrang { get; init; }
    public string? NguoiDoi { get; init; }
    public string? HoTenNguoiDoi { get; init; }
    public string? TinhTrangTruoc { get; init; }
    public KhachDangOPhongDto? KhachDangO { get; init; }
    public List<DatPhongSapToiDto> DatPhongSapToi { get; init; } = [];
    public List<SuCoTomTatDto> SuCo { get; init; } = [];
    public string? NguoiDangDon { get; init; }
    public string? CanhBao { get; init; }
    public string? MucCanhBao { get; init; }
    public string ViecTiepTheo { get; init; } = "";
    public string TinhTrangHienThi => KLCN060.Desktop.Converters.TrangThaiPhongConverter.MoTa(TinhTrang);
    /// <summary>"Từ 08/10 19:52 · do Phạm Thị Phòng đặt" - tách riêng để khung chi tiết không lặp lại tên tình trạng.</summary>
    public string DongTuLuc => (TinhTrangTuLuc is null ? "" : $"Từ {TinhTrangTuLuc:dd/MM/yyyy HH:mm}") + (string.IsNullOrEmpty(NguoiDoi) ? "" : $" · người đặt tình trạng: {HoTenNguoiDoi ?? NguoiDoi}");
    public string DongTruoc => string.IsNullOrEmpty(TinhTrangTruoc) ? "" : $"Trước đó: {KLCN060.Desktop.Converters.TrangThaiPhongConverter.MoTa(TinhTrangTruoc)}";
    public string DongTinhTrang => $"{TinhTrangHienThi}" + (TinhTrangTuLuc is null ? "" : $" từ {TinhTrangTuLuc:dd/MM HH:mm}")
        + (string.IsNullOrEmpty(NguoiDoi) ? "" : $" · do {HoTenNguoiDoi ?? NguoiDoi} đặt")
        + (string.IsNullOrEmpty(TinhTrangTruoc) ? "" : $" · trước đó: {KLCN060.Desktop.Converters.TrangThaiPhongConverter.MoTa(TinhTrangTruoc)}");
}

// ---------------- Tổng quan phiếu đặt phòng (D19) ----------------
public sealed class PhongTongQuanDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public string TenLoaiPhong { get; init; } = "";
    public int Tang { get; init; }
    public decimal DonGiaApDung { get; init; }
    public decimal ThanhTien { get; init; }
    public string TinhTrangPhong { get; init; } = "";
    public string LuuTru { get; init; } = "";
    public int? SoNguoi { get; init; }
    public string HienThi => $"{TenPhong} · {TenLoaiPhong} · {DonGiaApDung:N0}đ/đêm · " + (LuuTru switch { "DANG_O" => "đang ở", "DA_TRA" => "đã trả", _ => "chưa nhận" })
        + (LuuTru == "CHUA_NHAN" ? $" · phòng hiện {TinhTrangPhong}" : "");
}
public sealed class HoaDonTongQuanDto
{
    public string MaHD { get; init; } = "";
    public DateTime NgayLap { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThu { get; init; }
    public decimal ConNo { get; init; }
    public string TrangThai { get; init; } = "";
    public string HienThi => $"{MaHD}: tổng {TongTien:N0}đ · đã thu {DaThu:N0}đ · còn nợ {ConNo:N0}đ";
}
public sealed class ThaoTacPhieuDto
{
    public string Ma { get; init; } = "";
    public string Ten { get; init; } = "";
    public bool ChoPhep { get; init; }
    public string GiaiThich { get; init; } = "";
    public bool BatBuocLyDo { get; init; }
    public string HienThi => (ChoPhep ? "✔ " : "✖ ") + Ten + (string.IsNullOrEmpty(GiaiThich) ? "" : " - " + GiaiThich);
}
public sealed class LichSuPhieuGanNhatDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM HH:mm} · {NguoiThucHien} · {HanhDong}";
}
public sealed class TongQuanPhieuDatDto
{
    public string MaPhieuDat { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string NguonDat { get; init; } = "";
    public string LoaiDatPhong { get; init; } = "";
    public string MaKhach { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public string SoDTKhach { get; init; } = "";
    public string? EmailKhach { get; init; }
    public string? CCCDKhach { get; init; }
    public int SoNguoi { get; init; }
    public int SoNguoiOCung { get; init; }
    public DateOnly NgayDonDuKien { get; init; }
    public DateOnly NgayTraDuKien { get; init; }
    public int SoDem { get; init; }
    public List<PhongTongQuanDto> Phong { get; init; } = [];
    public string? MaPhieuNhan { get; init; }
    public decimal TongTienPhong { get; init; }
    public decimal TongKhuyenMai { get; init; }
    public decimal TongSauKhuyenMai { get; init; }
    public decimal TienCoc { get; init; }
    public bool DaThuCoc { get; init; }
    public decimal DaThu { get; init; }
    public decimal ConLai { get; init; }
    public string TrangThaiThanhToan { get; init; } = "";
    public string TomTatThanhToan { get; init; } = "";
    public List<HoaDonTongQuanDto> HoaDon { get; init; } = [];
    public DateTime NgayTao { get; init; }
    public string NguoiTao { get; init; } = "";
    public DateTime? NgaySuaCuoi { get; init; }
    public string? NguoiSuaCuoi { get; init; }
    public int SoLanSua { get; init; }
    public int SoNgayConLaiToiNgayDon { get; init; }
    public bool HoanCocNeuHuyBayGio { get; init; }
    public decimal SoTienHoanNeuHuy { get; init; }
    public decimal TienGiuLaiNeuHuy { get; init; }
    public bool QuaHanChuaNhan { get; init; }
    public List<ThaoTacPhieuDto> ThaoTac { get; init; } = [];
    public List<LichSuPhieuGanNhatDto> LichSuGanNhat { get; init; } = [];

    public bool ChoPhep(string ma) => ThaoTac.FirstOrDefault(x => x.Ma == ma)?.ChoPhep == true;
    public string KhachHienThi => $"{HoTenKhach} · {SoDTKhach}" + (string.IsNullOrEmpty(EmailKhach) ? "" : $" · {EmailKhach}") + (string.IsNullOrEmpty(CCCDKhach) ? "" : $" · CCCD {CCCDKhach}");
    /// <summary>Phiếu chưa nhận phòng thì CHƯA có số người thực tế (chỉ ghi khi làm thủ tục nhận phòng): chỉ nêu số đã khai báo trước, không khẳng định "1 người" cho cả đoàn.</summary>
    public string SoNguoiHienThi
    {
        get
        {
            if (!string.IsNullOrEmpty(MaPhieuNhan))
                return $"{SoNguoi} người ở thực tế (theo phiếu nhận phòng {MaPhieuNhan})";
            var daKhaiBao = 1 + SoNguoiOCung;
            var dong = $"Chưa nhận phòng - số người ở sẽ ghi khi làm thủ tục nhận phòng. Đã khai báo trước: {daKhaiBao} người (khách đặt" + (SoNguoiOCung > 0 ? $" + {SoNguoiOCung} người ở cùng)" : ")");
            if (LoaiDatPhong == "DOAN")
                dong += $" · đoàn {Phong.Count} phòng" + (daKhaiBao < Phong.Count ? ": khai báo còn ít hơn số phòng, hãy bổ sung người ở cùng." : ".");
            return dong;
        }
    }
    public string TienHienThi => $"Tổng tiền phòng {TongTienPhong:N0}đ" + (TongKhuyenMai > 0 ? $" - khuyến mãi {TongKhuyenMai:N0}đ = {TongSauKhuyenMai:N0}đ" : "") + $" · Đã thu {DaThu:N0}đ · Còn lại {ConLai:N0}đ";
    public string TaoSuaHienThi => $"Tạo: {NguoiTao} lúc {NgayTao:dd/MM/yyyy HH:mm}" + (NguoiSuaCuoi is null ? " · chưa sửa lần nào" : $" · sửa gần nhất: {NguoiSuaCuoi} lúc {NgaySuaCuoi:dd/MM HH:mm} ({SoLanSua} lần)");
    public string ChinhSachHuyHienThi => TrangThai is "CHO_XAC_NHAN" or "DA_XAC_NHAN"
        ? $"Còn {SoNgayConLaiToiNgayDon} ngày tới ngày đón → hủy bây giờ: " + (!DaThuCoc ? "chưa thu cọc nên không phát sinh tiền." : HoanCocNeuHuyBayGio ? $"HOÀN cọc {SoTienHoanNeuHuy:N0}đ." : $"KHÔNG hoàn, giữ lại {TienGiuLaiNeuHuy:N0}đ.") : "";
}

// ---------------- Hóa đơn (D20) ----------------
public sealed class BoLocHoaDon
{
    public string TuKhoa { get; init; } = "";
    public string TrangThai { get; init; } = "";
    /// <summary>"" hoặc QUA_HAN.</summary>
    public string DacBiet { get; init; } = "";
    public DateTime? TuNgay { get; init; }
    public DateTime? DenNgay { get; init; }
    public string SapXep { get; init; } = "";

    public string ToQuery()
    {
        var q = new List<string> { "q=" + Uri.EscapeDataString(TuKhoa.Trim()) };
        if (TrangThai != "") q.Add("trangThai=" + Uri.EscapeDataString(TrangThai));
        if (DacBiet == "QUA_HAN") q.Add("quaHan=true");
        if (TuNgay is { } t) q.Add("tuNgay=" + t.ToString("yyyy-MM-dd"));
        if (DenNgay is { } d) q.Add("denNgay=" + d.ToString("yyyy-MM-dd"));
        if (SapXep != "") q.Add("sapXep=" + SapXep);
        return string.Join("&", q);
    }
}

public sealed class ThongKeHoaDonDto
{
    public DateTime CapNhatLuc { get; init; }
    public int TongHoaDon { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThu { get; init; }
    public decimal ConNo { get; init; }
    public int SoChuaThanhToan { get; init; }
    public int SoThanhToanMotPhan { get; init; }
    public int SoDaThanhToan { get; init; }
    public int SoQuaHan { get; init; }
    public decimal ConNoQuaHan { get; init; }
    public int SoThuThua { get; init; }
    public int NgayQuaHan { get; init; }
    public string DoiChieu => SoThuThua == 0
        ? $"{TongTien:N0}đ = {DaThu:N0}đ đã thu + {ConNo:N0}đ còn nợ"
        : $"Tổng {TongTien:N0}đ; đã thu {DaThu:N0}đ; còn nợ {ConNo:N0}đ ({SoThuThua} hóa đơn thu thừa nên không cộng bù)";
}

public sealed class PhongHoaDonDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public string TenLoaiPhong { get; init; } = "";
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTra { get; init; }
    public int SoDem { get; init; }
    public decimal DonGia { get; init; }
    public string TrangThai { get; init; } = "";
    public string HienThi => $"Phòng {TenPhong} · {TenLoaiPhong} · {NgayNhan:dd/MM}→{(NgayTra is null ? "đang ở" : NgayTra.Value.ToString("dd/MM"))} ({SoDem} đêm) · {DonGia:N0}đ/đêm";
}

public sealed class DongTienHoaDonDto
{
    public string LoaiKhoanMuc { get; init; } = "";
    public string MoTa { get; init; } = "";
    public int SoLuong { get; init; }
    public decimal DonGia { get; init; }
    public decimal ThanhTien { get; init; }
    public string HienThi => $"{MoTa}{(SoLuong > 1 ? $" × {SoLuong}" : "")}: {ThanhTien:N0}đ";
}

public sealed class ThanhToanHoaDonDto
{
    public int MaThanhToan { get; init; }
    public DateTime ThoiGian { get; init; }
    public string HinhThuc { get; init; } = "";
    public decimal SoTien { get; init; }
    public string NguoiThu { get; init; } = "";
    public string? MaGiaoDich { get; init; }
    public string Nguon { get; init; } = "";
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {SoTien:N0}đ · "
        + (Nguon == "CHUYEN_KHOAN_QR" ? "chuyển khoản QR" : "tiền mặt tại quầy")
        + $" · thu bởi {NguoiThu}" + (string.IsNullOrEmpty(MaGiaoDich) ? "" : $" · mã GD {MaGiaoDich}");
}

public sealed class LichSuHoaDonDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM HH:mm} · {NguoiThucHien} · {HanhDong}";
}

public sealed class ThaoTacHoaDonDto
{
    public string Ma { get; init; } = "";
    public string Ten { get; init; } = "";
    public string AiThucHien { get; init; } = "";
    public bool ChoPhep { get; init; }
    public string GiaiThich { get; init; } = "";
    public string HienThi => (ChoPhep ? "✔ " : "✖ ") + Ten + " - " + AiThucHien + (string.IsNullOrEmpty(GiaiThich) ? "" : $". {GiaiThich}");
}

public sealed class TongQuanHoaDonDto
{
    public string MaHD { get; init; } = "";
    public DateTime NgayLap { get; init; }
    public string NguoiLap { get; init; } = "";
    public string? MaKhach { get; init; }
    public string? HoTenKhach { get; init; }
    public string? SoDTKhach { get; init; }
    public string MaPhieuNhan { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? NguonDat { get; init; }
    public List<PhongHoaDonDto> Phong { get; init; } = [];
    public List<DongTienHoaDonDto> CacKhoan { get; init; } = [];
    public decimal TienPhong { get; init; }
    public decimal TienDV { get; init; }
    public decimal PhuThu { get; init; }
    public decimal TongGiamGia { get; init; }
    public decimal TienDaCoc { get; init; }
    public decimal TongTien { get; init; }
    public decimal DaThu { get; init; }
    public decimal ConNo { get; init; }
    public string TrangThaiThanhToan { get; init; } = "";
    public bool CongThucKhop { get; init; }
    public string CongThucGhiChu { get; init; } = "";
    public bool QuaHan { get; init; }
    public string HanThanhToan { get; init; } = "";
    public List<ThanhToanHoaDonDto> ThanhToan { get; init; } = [];
    public List<LichSuHoaDonDto> LichSu { get; init; } = [];
    public List<ThaoTacHoaDonDto> ThaoTac { get; init; } = [];
    public List<string> CanhBao { get; init; } = [];

    public string KhachHienThi => $"{HoTenKhach} · {SoDTKhach}" + (MaKhach is null ? "" : $" · mã {MaKhach}");
    public string BookingHienThi => $"Phiếu nhận {MaPhieuNhan}" + (MaPhieuDat is null ? " (khách vãng lai, không qua đặt trước)" : $" · booking {MaPhieuDat} ({NguonDatHienThi})");
    private string NguonDatHienThi => NguonDat switch { "WEBSITE" => "đặt trên website", "QUAY_LE_TAN" => "đặt tại quầy", "DIEN_THOAI" => "đặt qua điện thoại", _ => NguonDat ?? "" };
    public string LapHienThi => $"Lập {NgayLap:dd/MM/yyyy HH:mm} bởi {NguoiLap}";
    public string CoCauHienThi => $"Phòng {TienPhong:N0}đ + dịch vụ {TienDV:N0}đ + phụ thu {PhuThu:N0}đ"
        + (TongGiamGia > 0 ? $" - giảm giá/khuyến mãi {TongGiamGia:N0}đ" : "") + (TienDaCoc > 0 ? $" - đã cọc {TienDaCoc:N0}đ" : "") + $" = phải thu {TongTien:N0}đ";
    public string TienHienThi => $"Đã thu {DaThu:N0}đ · Còn nợ {ConNo:N0}đ";
    public string TrangThaiHienThi => TrangThaiThanhToan switch { "CHUA_THANH_TOAN" => "Chưa thanh toán", "THANH_TOAN_MOT_PHAN" => "Thanh toán một phần", "DA_THANH_TOAN" => "Đã thanh toán", _ => TrangThaiThanhToan };
    public bool CoThanhToan => ThanhToan.Count > 0;
    public bool KhongCoThanhToan => ThanhToan.Count == 0;
    public bool CoCanhBao => CanhBao.Count > 0;
    public List<string> CanhBaoHienThi => CanhBao;
    /// <summary>Quản lý (chỉ xem hóa đơn và công nợ) không thấy thao tác xuất hóa đơn PDF của người thu tiền.</summary>
    public List<ThaoTacHoaDonDto> ThaoTacHienThi => AppSession.Current.CoQuyen(QuyenHeThong.ThanhToan) ? ThaoTac : ThaoTac.Where(x => x.Ma != "XUAT_PDF").ToList();

}

// ---------------- Đối soát chuyển khoản (D18) ----------------
public sealed class BoLocDoiSoat
{
    public string TuKhoa { get; init; } = "";
    public string Loai { get; init; } = "";
    public string TrangThaiXuLy { get; init; } = "";
    public DateOnly? TuNgay { get; init; }
    public DateOnly? DenNgay { get; init; }

    public static string Ky(DateOnly? tu, DateOnly? den)
    {
        var q = new List<string>();
        if (tu is { } t) q.Add("tuNgay=" + t.ToString("yyyy-MM-dd"));
        if (den is { } d) q.Add("denNgay=" + d.ToString("yyyy-MM-dd"));
        return string.Join("&", q);
    }

    public string ToQuery()
    {
        var q = new List<string> { "q=" + Uri.EscapeDataString(TuKhoa.Trim()) };
        if (Loai != "") q.Add("loai=" + Uri.EscapeDataString(Loai));
        if (TrangThaiXuLy != "") q.Add("trangThaiXuLy=" + Uri.EscapeDataString(TrangThaiXuLy));
        var ky = Ky(TuNgay, DenNgay);
        if (ky != "") q.Add(ky);
        return string.Join("&", q);
    }
}

public sealed class NhomNgoaiLeDoiSoatDto
{
    public string Loai { get; init; } = "";
    public string Ten { get; init; } = "";
    public int SoLuong { get; init; }
    public int SoChuaXuLy { get; init; }
    public decimal SoTien { get; init; }
    public decimal SoTienChuaXuLy { get; init; }
    public string HienThi => $"{Ten}: {SoLuong} ({SoTien:N0}đ)" + (SoChuaXuLy > 0 ? $" · chờ xử lý {SoChuaXuLy}" : "");
}

public sealed class TongQuanDoiSoatDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public DateTime CapNhatLuc { get; init; }
    public DateTime? ThongBaoMoiNhatLuc { get; init; }
    public decimal TongTienNganHangNhan { get; init; }
    public decimal TongTienDaKhop { get; init; }
    public int TongThongBao { get; init; }
    public int SoKhop { get; init; }
    public int SoChuaKhop { get; init; }
    public int SoChuaXuLy { get; init; }
    public int SoDaXuLy { get; init; }
    public decimal TongTienCanXuLy { get; init; }
    public decimal TongChenhLech { get; init; }
    public int SoChuaXuLyQuaHan { get; init; }
    public int NgayQuaHan { get; init; }
    public int SoYeuCauChoThanhToan { get; init; }
    public decimal TienYeuCauChoThanhToan { get; init; }
    public int SoYeuCauHetHan { get; init; }
    public List<NhomNgoaiLeDoiSoatDto> NhomNgoaiLe { get; init; } = [];

    public string KyHienThi => $"Kỳ {TuNgay:dd/MM/yyyy} - {DenNgay:dd/MM/yyyy}";
    public string CapNhatHienThi => $"Cập nhật {CapNhatLuc:HH:mm:ss}" + (ThongBaoMoiNhatLuc is { } t ? $" · thông báo ngân hàng mới nhất nhận lúc {t:dd/MM/yyyy HH:mm}" : " · chưa nhận thông báo nào từ ngân hàng");
    public string CongThucHienThi => $"Ngân hàng báo {TongTienNganHangNhan:N0}đ = {TongTienDaKhop:N0}đ đã khớp + {TongTienNganHangNhan - TongTienDaKhop:N0}đ chưa khớp";
}

public class DongDoiSoatDto
{
    public int MaThongBao { get; init; }
    public DateTime ThoiGianNganHang { get; init; }
    public DateTime ThoiGianNhan { get; init; }
    public string MaGiaoDichNganHang { get; init; } = "";
    public string SoTaiKhoanNhan { get; init; } = "";
    public decimal SoTienNganHang { get; init; }
    public string NoiDung { get; init; } = "";
    public string KetQua { get; init; } = "";
    public string Loai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public string LyDo { get; init; } = "";
    public string GoiYXuLy { get; init; } = "";
    public string? MaGiaoDich { get; init; }
    public string? TrangThaiGiaoDich { get; init; }
    public string? LoaiThamChieu { get; init; }
    public string? MaThamChieu { get; init; }
    public string? MaPhieuDat { get; init; }
    public string? MaHoaDon { get; init; }
    public string? TenKhach { get; init; }
    public decimal? SoTienPhaiThu { get; init; }
    public decimal SoTienDaGhiNhan { get; init; }
    public decimal ChenhLech { get; init; }
    public string TrangThaiXuLy { get; init; } = "";
    public int SoNgayTonDong { get; init; }
    public bool QuaHanXuLy { get; init; }
    public string? NguoiXuLy { get; init; }
    public DateTime? NgayXuLy { get; init; }
    public string? HuongXuLy { get; init; }
    public string? GhiChuXuLy { get; init; }

    public bool CanXuLy => TrangThaiXuLy == "CAN_XU_LY";
    /// <summary>Ai đã xử lý khoản này, lúc nào, bằng cách nào, ghi chú gì (rỗng nếu chưa xử lý / tự khớp).</summary>
    public string ThongTinXuLyHienThi => TrangThaiXuLy != "DA_XU_LY" ? "" :
        $"Đã xử lý bởi {(string.IsNullOrEmpty(NguoiXuLy) ? "(không rõ)" : NguoiXuLy)} lúc {NgayXuLy:dd/MM/yyyy HH:mm}"
        + (HuongXuLy switch { "DA_XU_LY_NGOAI" => " · cách xử lý: đã xử lý ngoài hệ thống", "BO_QUA" => " · cách xử lý: bỏ qua (không phải tiền khách sạn)", _ => "" })
        + (string.IsNullOrWhiteSpace(GhiChuXuLy) ? "" : $"\nGhi chú: {GhiChuXuLy}");
    public string DongDauHienThi => $"{SoTienNganHang:N0}đ · {MaGiaoDichNganHang}";
    public string DongHaiHienThi => $"{ThoiGianNhan:dd/MM/yyyy HH:mm}" + (string.IsNullOrEmpty(TenKhach) ? "" : $" · {TenKhach}");
    public string DoiTuongHienThi => MaGiaoDich is null ? "(chưa xác định yêu cầu)" : $"{MaGiaoDich} → " + (MaHoaDon is not null ? $"HĐ {MaHoaDon}" : MaPhieuDat is not null ? $"cọc {MaPhieuDat}" : MaThamChieu);
    public string ChenhLechHienThi => Loai == "KHOP" ? "0đ" : (ChenhLech > 0 ? "+" : "") + $"{ChenhLech:N0}đ";
    public string XuLyHienThi => TrangThaiXuLy switch
    {
        "KHONG_CAN" => "Tự khớp",
        "CAN_XU_LY" => QuaHanXuLy ? $"CHỜ XỬ LÝ {SoNgayTonDong} ngày (quá hạn)" : "CHỜ XỬ LÝ",
        "DA_XU_LY" => "Đã xử lý" + (HuongXuLy switch { "DA_XU_LY_NGOAI" => " (ngoài hệ thống)", "BO_QUA" => " (bỏ qua)", _ => "" }),
        _ => TrangThaiXuLy
    };
}

public sealed class LichSuDoiSoatDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string? ChiTiet { get; init; }
    public string HienThi => $"{ThoiGian:dd/MM/yyyy HH:mm} · {NguoiThucHien} · {HanhDong}";
}

public sealed class ChiTietDoiSoatDto : DongDoiSoatDto
{
    public DateTime? NgayTaoYeuCau { get; init; }
    public DateTime? HanThanhToan { get; init; }
    public string? NguoiTaoYeuCau { get; init; }
    public List<DongDoiSoatDto> ThongBaoCungGiaoDich { get; init; } = [];
    public List<LichSuDoiSoatDto> LichSu { get; init; } = [];
    public List<string> HuongXuLyChoPhep { get; init; } = [];
}
