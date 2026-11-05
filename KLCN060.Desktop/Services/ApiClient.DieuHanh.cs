using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace KLCN060.Desktop.Services;

// Các lời gọi API: báo cáo thống kê, đặt phòng, chi tiết phòng, hóa đơn, nhân viên và sao lưu / phục hồi.
public sealed partial class ApiClient
{
    // ---------------- Bảng điều hành / báo cáo / cảnh báo ----------------
    public Task<TongQuanBaoCaoDto> GetTongQuanAsync(DateOnly tu, DateOnly den)
        => GetAsync<TongQuanBaoCaoDto>($"api/v1/reports/overview?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
    public Task<DoanhThuChiTietDto> GetDoanhThuChiTietAsync(DateOnly tu, DateOnly den)
        => GetAsync<DoanhThuChiTietDto>($"api/v1/reports/revenue-breakdown?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}");
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


    // ---------------- Nhân viên ----------------
    public Task<NhanVienDto> TaoNhanVienAsync(YeuCauTaoNhanVien x) => PostAsync<YeuCauTaoNhanVien, NhanVienDto>("api/v1/employees", x);
    public Task<NhanVienDto> SuaNhanVienAsync(string maNV, YeuCauSuaNhanVien x)
        => PutAsync<YeuCauSuaNhanVien, NhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}", x);
    public Task<KetQuaTaiKhoanNhanVienDto> KhoaNhanVienAsync(string maNV, string? lyDo = null)
        => PostAsync<YeuCauLyDoTaiKhoan, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}/lock", new YeuCauLyDoTaiKhoan(lyDo));

    // ---------------- Sao lưu ----------------
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
// ---------------- Cảnh báo ----------------
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
    public bool QuaHan { get; init; }
    public string QuaHanText => QuaHan ? "Quá hạn" : "";
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
// ---------------- Bảng giá ----------------
// ---------------- Nhân viên ----------------
public sealed record YeuCauTaoNhanVien(string HoTen, string SoDT, string ChucVu, int MaVaiTro, string TenDangNhap, string MatKhau);
public sealed record YeuCauSuaNhanVien(string HoTen, string SoDT, string ChucVu);

// ---------------- Sao lưu ----------------
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

