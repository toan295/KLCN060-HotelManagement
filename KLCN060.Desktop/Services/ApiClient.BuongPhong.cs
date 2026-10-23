using System.IO;
using System.Net;
using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Các lời gọi API của nghiệp vụ buồng phòng: việc dọn phòng, sự cố, danh mục đồ dùng + kiểm phòng/minibar, đồ khách bỏ quên.
public sealed partial class ApiClient
{
    // ---------------- Việc dọn phòng ----------------
    public Task<BangViecDonDto> GetViecDonPhongAsync() => GetAsync<BangViecDonDto>("api/v1/housekeeping/tasks");
    public Task<ViecDonPhongDto> NhanDonPhongAsync(string maPhong) => PostAsync<object, ViecDonPhongDto>($"api/v1/housekeeping/rooms/{Uri.EscapeDataString(maPhong)}/claim", new { });
    public Task BoNhanDonPhongAsync(string maPhong) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/housekeeping/rooms/{Uri.EscapeDataString(maPhong)}/claim");
    public Task<TongQuanBuongPhongDto> GetTongQuanBuongPhongAsync() => GetAsync<TongQuanBuongPhongDto>("api/v1/housekeeping/overview");
    public Task<ViecDonPhongDto> GiaoViecDonPhongAsync(string maPhong, string nguoiDon)
        => PostAsync<object, ViecDonPhongDto>($"api/v1/housekeeping/rooms/{Uri.EscapeDataString(maPhong)}/assign", new { nguoiDon });
    public Task<List<NhatKyBuongPhongDto>> GetNhatKyBuongPhongAsync(string? maPhong, DateOnly tu, DateOnly den)
        => GetAsync<List<NhatKyBuongPhongDto>>($"api/v1/housekeeping/activity?tuNgay={tu:yyyy-MM-dd}&denNgay={den:yyyy-MM-dd}&toiDa=200" + (string.IsNullOrEmpty(maPhong) ? "" : "&maPhong=" + Uri.EscapeDataString(maPhong)));
    public Task<BaoCaoDonPhongDto> GetBaoCaoDonPhongAsync(DateOnly tu, DateOnly den)
        => GetAsync<BaoCaoDonPhongDto>($"api/v1/housekeeping/cleaning-report?tuNgay={tu:yyyy-MM-dd}&denNgay={den:yyyy-MM-dd}");

    // ---------------- Sự cố phòng ----------------
    public Task<List<SuCoPhongDto>> GetSuCoAsync(string? trangThai = null, string? maPhong = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrEmpty(trangThai)) q.Add("trangThai=" + Uri.EscapeDataString(trangThai));
        if (!string.IsNullOrEmpty(maPhong)) q.Add("maPhong=" + Uri.EscapeDataString(maPhong));
        return GetAsync<List<SuCoPhongDto>>("api/v1/incidents" + (q.Count == 0 ? "" : "?" + string.Join("&", q)));
    }
    public Task<SuCoPhongDto> BaoSuCoAsync(YeuCauBaoSuCo x) => PostAsync<YeuCauBaoSuCo, SuCoPhongDto>("api/v1/incidents", x);
    public Task<SuCoPhongDto> DoiTrangThaiSuCoAsync(int maSuCo, string trangThai, string? ghiChu)
        => PatchAsync<object, SuCoPhongDto>($"api/v1/incidents/{maSuCo}/status", new { trangThai, ghiChu });

    // ---------------- Danh mục đồ dùng ----------------
    public Task<List<VatDungDto>> GetVatDungAsync(bool chiHoatDong = true) => GetAsync<List<VatDungDto>>($"api/v1/supplies?chiHoatDong={chiHoatDong.ToString().ToLowerInvariant()}");
    public Task<List<LoaiPhongVatDungDto>> GetLoaiPhongChoVatDungAsync() => GetAsync<List<LoaiPhongVatDungDto>>("api/v1/supplies/room-types");
    public Task<VatDungDto> TaoVatDungAsync(YeuCauVatDung x) => PostAsync<YeuCauVatDung, VatDungDto>("api/v1/supplies", x);
    public Task<VatDungDto> SuaVatDungAsync(string ma, YeuCauVatDung x) => PutAsync<YeuCauVatDung, VatDungDto>($"api/v1/supplies/{Uri.EscapeDataString(ma)}", x);
    public Task XoaVatDungAsync(string ma) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/supplies/{Uri.EscapeDataString(ma)}");

    // ---------------- Kiểm phòng / minibar ----------------
    public Task<MauKiemPhongDto> GetMauKiemPhongAsync(string maPhong) => GetAsync<MauKiemPhongDto>($"api/v1/room-checks/template/{Uri.EscapeDataString(maPhong)}");
    public Task<PhieuKiemPhongDto> TaoPhieuKiemAsync(YeuCauPhieuKiem x) => PostAsync<YeuCauPhieuKiem, PhieuKiemPhongDto>("api/v1/room-checks", x);
    public Task<List<PhieuKiemPhongDto>> GetPhieuKiemAsync(string? maPhong = null)
        => GetAsync<List<PhieuKiemPhongDto>>("api/v1/room-checks" + (string.IsNullOrEmpty(maPhong) ? "" : "?maPhong=" + Uri.EscapeDataString(maPhong)));
    public Task HuyPhieuKiemAsync(int maPhieuKiem, string lyDo) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/room-checks/{maPhieuKiem}?lyDo={Uri.EscapeDataString(lyDo)}");
    public Task<List<KhoanMinibarDto>> GetKhoanMinibarAsync(string? trangThai = null)
        => GetAsync<List<KhoanMinibarDto>>("api/v1/room-checks/minibar-charges" + (string.IsNullOrEmpty(trangThai) ? "" : "?trangThai=" + Uri.EscapeDataString(trangThai)));
    public Task<KhoanMinibarDto> TinhPhiMinibarAsync(int maChiTiet) => PostAsync<object, KhoanMinibarDto>($"api/v1/room-checks/minibar-charges/{maChiTiet}/charge", new { });
    public Task<KhoanMinibarDto> BoQuaMinibarAsync(int maChiTiet, string lyDo) => PostAsync<object, KhoanMinibarDto>($"api/v1/room-checks/minibar-charges/{maChiTiet}/waive", new { lyDo });
    public Task<List<HaoHutDto>> GetBaoCaoHaoHutAsync(DateOnly tu, DateOnly den)
        => GetAsync<List<HaoHutDto>>($"api/v1/room-checks/loss-report?tuNgay={tu:yyyy-MM-dd}&denNgay={den:yyyy-MM-dd}");

    // ---------------- Đồ bỏ quên ----------------
    public Task<List<DoBoQuenDto>> GetDoBoQuenAsync(string? trangThai = null, string? tuKhoa = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrEmpty(trangThai)) q.Add("trangThai=" + Uri.EscapeDataString(trangThai));
        if (!string.IsNullOrWhiteSpace(tuKhoa)) q.Add("tuKhoa=" + Uri.EscapeDataString(tuKhoa.Trim()));
        return GetAsync<List<DoBoQuenDto>>("api/v1/lost-found" + (q.Count == 0 ? "" : "?" + string.Join("&", q)));
    }
    public Task<DoBoQuenDto> GhiNhanDoBoQuenAsync(YeuCauDoBoQuen x) => PostAsync<YeuCauDoBoQuen, DoBoQuenDto>("api/v1/lost-found", x);
    public Task<DoBoQuenDto> SuaDoBoQuenAsync(int ma, YeuCauSuaDoBoQuen x) => PutAsync<YeuCauSuaDoBoQuen, DoBoQuenDto>($"api/v1/lost-found/{ma}", x);
    public Task<DoBoQuenDto> LuuKhoDoBoQuenAsync(int ma, string viTri) => PostAsync<object, DoBoQuenDto>($"api/v1/lost-found/{ma}/store", new { viTriLuuGiu = viTri });
    public Task<DoBoQuenDto> TraDoBoQuenAsync(int ma, string nguoiNhanLai, string giayTo, string? ghiChu)
        => PostAsync<object, DoBoQuenDto>($"api/v1/lost-found/{ma}/return", new { nguoiNhanLai, giayToNguoiNhan = giayTo, ghiChu });
    public Task<DoBoQuenDto> ThanhLyDoBoQuenAsync(int ma, string lyDo) => PostAsync<object, DoBoQuenDto>($"api/v1/lost-found/{ma}/dispose", new { lyDo });
    public Task<DoBoQuenDto> ThemAnhDoBoQuenAsync(int ma, string duongDanFile)
    {
        var duLieu = File.ReadAllBytes(duongDanFile);
        // Tên file gửi lên không quan trọng (API nhận dạng ảnh theo nội dung) - dùng tên ASCII an toàn.
        var tenGui = "anh" + Path.GetExtension(duongDanFile);
        return GuiCoLamMoiAsync<DoBoQuenDto>(HttpMethod.Post, $"api/v1/lost-found/{ma}/images", null, () =>
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(duLieu), "file", tenGui);
            return form;
        });
    }
    public Task<DoBoQuenDto> XoaAnhDoBoQuenAsync(int ma, int maAnh) => GuiCoLamMoiAsync<DoBoQuenDto>(HttpMethod.Delete, $"api/v1/lost-found/{ma}/images/{maAnh}", null);

    /// <summary>Tải nội dung ảnh đồ bỏ quên (cần đăng nhập - không có đường công khai).</summary>
    public async Task<byte[]> TaiAnhDoBoQuenAsync(int ma, int maAnh)
    {
        var duongDan = $"api/v1/lost-found/{ma}/images/{maAnh}";
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return await responseMoi.Content.ReadAsByteArrayAsync();
        }

        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }
}

// ===================== Việc dọn phòng =====================

public sealed class ViecDonPhongDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public int Tang { get; init; }
    public string TenLoaiPhong { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public DateTime? TuLuc { get; init; }
    public double? SoGioCho { get; init; }
    public string MucUuTien { get; init; } = "BINH_THUONG";
    public string? LyDoUuTien { get; init; }
    public bool KhachDenHomNay { get; init; }
    public string? NguoiDon { get; init; }
    public string? HoTenNguoiDon { get; init; }
    public DateTime? BatDauDonLuc { get; init; }
    public int SuCoDangMo { get; init; }

    public string LoaiViecHienThi => TinhTrang == "OD" ? "Có khách - dọn phòng" : "Trống - dọn sau trả phòng";
    public string UuTienHienThi => MucUuTien switch { "KHAN" => "Khẩn", "CAO" => "Cao", _ => "Bình thường" };
    public string ChoHienThi => SoGioCho is null ? "" : SoGioCho < 1 ? "dưới 1 giờ" : $"{SoGioCho:0.#} giờ";
    public string? NguoiPhanCong { get; init; }
    public DateTime? GiaoLuc { get; init; }
    public string NguoiDonHienThi => string.IsNullOrEmpty(NguoiDon) ? "Chưa ai nhận"
        : BatDauDonLuc is null ? $"{HoTenNguoiDon ?? NguoiDon} (được giao{(GiaoLuc is null ? "" : $" lúc {GiaoLuc:HH:mm}")}, chưa bắt đầu)"
        : $"{HoTenNguoiDon ?? NguoiDon} (từ {BatDauDonLuc:HH:mm})";
    public string SuCoHienThi => SuCoDangMo > 0 ? $"{SuCoDangMo} sự cố" : "";
    public string GhiChuHienThi => string.Join(" · ", new[] { LyDoUuTien, SuCoDangMo > 0 ? $"phòng còn {SuCoDangMo} sự cố chưa xử lý" : null }.Where(x => !string.IsNullOrEmpty(x)));
}

public sealed class BangViecDonDto
{
    public DateTime CapNhatLuc { get; init; }
    public int TongCanDon { get; init; }
    public int PhongTrongCanDon { get; init; }
    public int PhongCoKhachCanDon { get; init; }
    public int DangDon { get; init; }
    public int PhongKhan { get; init; }
    public int DaDonHomNay { get; init; }
    public int DaDonHomNayCuaToi { get; init; }
    public int TongPhong { get; init; }
    public int PhongSanSang { get; init; }
    public int PhongDaKiemTra { get; init; }
    public int PhongCoKhach { get; init; }
    public int PhongBaoTri { get; init; }
    public List<ViecDonPhongDto> Viec { get; init; } = [];
}

public sealed class DongBaoCaoDonPhongDto
{
    public string NguoiDon { get; init; } = "";
    public string? HoTen { get; init; }
    public int SoPhongDaDon { get; init; }
    public int SoPhongCoNhanViec { get; init; }
    public double? ThoiGianTrungBinhPhut { get; init; }
    public string TenHienThi => HoTen ?? NguoiDon;
    public string ThoiGianHienThi => ThoiGianTrungBinhPhut is null ? "" : $"{ThoiGianTrungBinhPhut:0.#} phút";
}

public sealed class BaoCaoDonPhongDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public int TongPhongDaDon { get; init; }
    public double? ThoiGianTrungBinhPhut { get; init; }
    public List<DongBaoCaoDonPhongDto> Dong { get; init; } = [];
}

// ===================== Sự cố =====================

public sealed record YeuCauBaoSuCo(string MaPhong, string Loai, string TieuDe, string? MoTa, string MucDo);

public sealed class SuCoPhongDto
{
    public int MaSuCo { get; init; }
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public string TinhTrangPhong { get; init; } = "";
    public string Loai { get; init; } = "";
    public string TieuDe { get; init; } = "";
    public string? MoTa { get; init; }
    public string MucDo { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string NguoiBao { get; init; } = "";
    public string? HoTenNguoiBao { get; init; }
    public DateTime ThoiGianBao { get; init; }
    public string? NguoiXuLy { get; init; }
    public string? HoTenNguoiXuLy { get; init; }
    public DateTime? ThoiGianTiepNhan { get; init; }
    public DateTime? ThoiGianXong { get; init; }
    public string? GhiChuXuLy { get; init; }
    public string? NguoiDong { get; init; }
    public string? HoTenNguoiDong { get; init; }
    public double SoGio { get; init; }

    public string MaHienThi => $"SC{MaSuCo:0000}";
    public string LoaiHienThi => TenLoaiSuCo(Loai);
    public string MucDoHienThi => TenMucDoSuCo(MucDo);
    public string TrangThaiHienThi => TrangThai switch
    {
        "MOI" => "Mới báo", "DA_TIEP_NHAN" => "Đã tiếp nhận", "DANG_XU_LY" => "Đang xử lý", "DA_XU_LY" => "Đã xử lý", "DA_HUY" => "Đã hủy", _ => TrangThai
    };
    public string NguoiBaoHienThi => HoTenNguoiBao ?? NguoiBao;
    public string NguoiXuLyHienThi => HoTenNguoiXuLy ?? NguoiXuLy ?? "";
    /// <summary>Dòng thông tin phụ của sự cố: chỉ nêu phần đã có dữ liệu (không còn dấu "·" trống).</summary>
    public string ThongTinPhuHienThi
    {
        get
        {
            var phan = new List<string> { $"Người báo: {NguoiBaoHienThi} lúc {ThoiGianBao:dd/MM HH:mm}" };
            if (!string.IsNullOrWhiteSpace(NguoiXuLyHienThi)) phan.Add($"Người xử lý: {NguoiXuLyHienThi}");
            if (!string.IsNullOrWhiteSpace(DongBoiHienThi)) phan.Add(DongBoiHienThi);
            if (!string.IsNullOrWhiteSpace(GhiChuXuLy)) phan.Add($"Ghi chú xử lý: {GhiChuXuLy}");
            return string.Join("  ·  ", phan);
        }
    }
    /// <summary>Ai đóng sự cố (xong hoặc hủy) và lúc nào; dữ liệu cũ chưa lưu người đóng thì hiện người xử lý cuối.</summary>
    public string DongBoiHienThi => TrangThai is not ("DA_XU_LY" or "DA_HUY") ? "" : $"{(TrangThai == "DA_HUY" ? "Hủy" : "Xong")} bởi {HoTenNguoiDong ?? NguoiDong ?? HoTenNguoiXuLy ?? NguoiXuLy ?? "không rõ"}" + (ThoiGianXong is { } t ? $" lúc {t:dd/MM HH:mm}" : "");
    public string ThoiGianHienThi => SoGio < 1 ? "dưới 1 giờ" : SoGio < 48 ? $"{SoGio:0.#} giờ" : $"{SoGio / 24:0.#} ngày";
    public string PhongHienThi => $"{TenPhong} ({TinhTrangPhong})";
    public bool DangMo => TrangThai is "MOI" or "DA_TIEP_NHAN" or "DANG_XU_LY";

    public static string TenLoaiSuCo(string loai) => loai switch
    {
        "DIEU_HOA" => "Điều hòa", "DIEN_CHIEU_SANG" => "Điện / chiếu sáng", "NUOC" => "Nước / đường ống", "TV_THIET_BI_DIEN_TU" => "TV / thiết bị điện tử",
        "NOI_THAT" => "Nội thất", "THIEU_TRANG_THIET_BI" => "Thiếu trang thiết bị", "VE_SINH" => "Vệ sinh", "KHAC" => "Khác", _ => loai
    };
    public static string TenMucDoSuCo(string mucDo) => mucDo switch { "THAP" => "Thấp", "TRUNG_BINH" => "Trung bình", "CAO" => "Cao", "KHAN_CAP" => "Khẩn cấp", _ => mucDo };
}

// ===================== Danh mục đồ dùng, kiểm phòng, minibar =====================

public sealed record YeuCauVatDung(string TenVatDung, string Nhom, string DonViTinh, int SoLuongChuan, string? MaDV, bool HoatDong, string CachTinh = "MOI_PHONG", List<string>? MaLoaiPhongs = null);

public sealed class LoaiPhongVatDungDto
{
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public string? LoaiGiuong { get; init; }
    public int SoGiuong { get; init; }
    public int SoNguoiTieuChuan { get; init; }
    public string MoTa => $"{SoGiuong} giường · {SoNguoiTieuChuan} người" + (string.IsNullOrEmpty(LoaiGiuong) ? "" : $" ({LoaiGiuong})");
}

public sealed class DinhMucLoaiPhongDto
{
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public int SoGiuong { get; init; }
    public int SoNguoiTieuChuan { get; init; }
    public int DinhMuc { get; init; }
}

public sealed class VatDungDto
{
    public string MaVatDung { get; init; } = "";
    public string TenVatDung { get; init; } = "";
    public string Nhom { get; init; } = "";
    public string DonViTinh { get; init; } = "";
    public int SoLuongChuan { get; init; }
    public string CachTinh { get; init; } = "MOI_PHONG";
    public List<string> MaLoaiPhongs { get; init; } = [];
    public List<DinhMucLoaiPhongDto> DinhMucTheoLoai { get; init; } = [];
    public string? MaDV { get; init; }
    public string? TenDV { get; init; }
    public decimal? GiaTinhPhi { get; init; }
    public bool HoatDong { get; init; }

    public string DinhMucHienThi => $"{SoLuongChuan} {DonViTinh} / " + (CachTinh switch { "MOI_GIUONG" => "giường", "MOI_NGUOI" => "người", _ => "phòng" });
    public string PhamViHienThi => MaLoaiPhongs.Count == 0 ? "Mọi loại phòng" : string.Join(", ", DinhMucTheoLoai.Select(x => x.TenLoai));
    public string DinhMucTheoLoaiHienThi => string.Join("  ·  ", DinhMucTheoLoai.Select(x => $"{x.TenLoai} ({x.SoGiuong} giường, {x.SoNguoiTieuChuan} người): {x.DinhMuc} {DonViTinh}"));

    public string NhomHienThi => TenNhomVatDung(Nhom);
    public string TinhPhiHienThi => MaDV is null ? "Không tính phí" : $"{TenDV} - {GiaTinhPhi:N0}đ/{DonViTinh}";
    public string TrangThaiHienThi => HoatDong ? "Đang dùng" : "Ngừng dùng";

    public static string TenNhomVatDung(string nhom) => nhom switch
    {
        "KHAN_GA_GOI" => "Khăn / ga / gối", "AMENITIES" => "Amenities", "NUOC_UONG" => "Nước uống", "MINIBAR" => "Minibar", "KHAC" => "Đồ dùng khác", _ => nhom
    };
}

public sealed class DongMauKiemDto
{
    public string MaVatDung { get; init; } = "";
    public string TenVatDung { get; init; } = "";
    public string Nhom { get; init; } = "";
    public string DonViTinh { get; init; } = "";
    public int SoLuongChuan { get; init; }
    public int SoLuongTruoc { get; init; }
    public bool TinhPhi { get; init; }
    public decimal? GiaTinhPhi { get; init; }
}

public sealed class MauKiemPhongDto
{
    public string MaPhong { get; init; } = "";
    public string TinhTrangPhong { get; init; } = "";
    public string? MaPhieuNhan { get; init; }
    public DateTime? KiemGanNhatLuc { get; init; }
    public List<DongMauKiemDto> Dong { get; init; } = [];
}

public sealed record YeuCauDongKiem(string MaVatDung, int? SoLuongTruoc, int SoLuongSau, int SoLuongBoSung, string? LyDo, string? GhiChu);
public sealed record YeuCauPhieuKiem(string MaPhong, string? GhiChu, List<YeuCauDongKiem> Dong);

public sealed class DongKiemDto
{
    public int MaChiTiet { get; init; }
    public string MaVatDung { get; init; } = "";
    public string TenVatDung { get; init; } = "";
    public string DonViTinh { get; init; } = "";
    public int SoLuongTruoc { get; init; }
    public int SoLuongSau { get; init; }
    public int SoLuongBoSung { get; init; }
    public int HaoHut { get; init; }
    public string? LyDo { get; init; }
    public string? GhiChu { get; init; }
    public int SoLuongTinhPhi { get; init; }
    public decimal ThanhTien { get; init; }
    public string TrangThaiTinhPhi { get; init; } = "";

    public string LyDoHienThi => TenLyDoHaoHut(LyDo);
    public string TinhPhiHienThi => TrangThaiTinhPhi switch
    {
        "CHO_TINH_PHI" => $"Chờ lễ tân tính phí ({ThanhTien:N0}đ)", "DA_TINH_PHI" => $"Đã tính phí ({ThanhTien:N0}đ)", "BO_QUA" => "Đã bỏ qua", _ => ""
    };

    public static string TenLyDoHaoHut(string? lyDo) => lyDo switch { "TIEU_THU" => "Tiêu thụ", "MAT_THIEU" => "Mất / thiếu", "HU_HONG" => "Hư hỏng", _ => "" };
}

public sealed class PhieuKiemPhongDto
{
    public int MaPhieuKiem { get; init; }
    public string MaPhong { get; init; } = "";
    public string? MaPhieuNhan { get; init; }
    public string NguoiKiem { get; init; } = "";
    public string? HoTenNguoiKiem { get; init; }
    public DateTime ThoiGian { get; init; }
    public string? GhiChu { get; init; }
    public List<DongKiemDto> Dong { get; init; } = [];

    public string MaHienThi => $"PK{MaPhieuKiem:00000}";
    public string NguoiKiemHienThi => HoTenNguoiKiem ?? NguoiKiem;
    public string TomTat => string.Join("; ", Dong.Where(x => x.HaoHut > 0 || x.SoLuongBoSung > 0)
        .Select(x => x.HaoHut > 0 ? $"{x.TenVatDung}: {x.SoLuongTruoc}→{x.SoLuongSau}" + (x.SoLuongBoSung > 0 ? $" (+{x.SoLuongBoSung})" : "") : $"{x.TenVatDung}: bổ sung {x.SoLuongBoSung}"));
    public string KhachHienThi => string.IsNullOrEmpty(MaPhieuNhan) ? "Phòng trống" : $"Lượt ở {MaPhieuNhan}";
}

public sealed class KhoanMinibarDto
{
    public int MaChiTiet { get; init; }
    public int MaPhieuKiem { get; init; }
    public string MaPhong { get; init; } = "";
    public string MaPhieuNhan { get; init; } = "";
    public string? HoTenKhach { get; init; }
    public string MaVatDung { get; init; } = "";
    public string TenVatDung { get; init; } = "";
    public string DonViTinh { get; init; } = "";
    public int SoLuongTruoc { get; init; }
    public int SoLuongSau { get; init; }
    public int SoLuongTinhPhi { get; init; }
    public decimal ThanhTien { get; init; }
    public string TrangThaiTinhPhi { get; init; } = "";
    public string NguoiKiem { get; init; } = "";
    public string? HoTenNguoiKiem { get; init; }
    public DateTime ThoiGianKiem { get; init; }
    public string? NguoiXuLy { get; init; }
    public DateTime? ThoiGianXuLy { get; init; }
    public string? LyDoBoQua { get; init; }

    public string KiemHienThi => $"{SoLuongTruoc} → {SoLuongSau} {DonViTinh}";
    public string NguoiKiemHienThi => HoTenNguoiKiem ?? NguoiKiem;
    public string TrangThaiHienThi => TrangThaiTinhPhi switch { "CHO_TINH_PHI" => "Chờ tính phí", "DA_TINH_PHI" => "Đã tính phí", "BO_QUA" => "Đã bỏ qua", _ => TrangThaiTinhPhi };
}

public sealed class HaoHutDto
{
    public string MaVatDung { get; init; } = "";
    public string TenVatDung { get; init; } = "";
    public string DonViTinh { get; init; } = "";
    public int TieuThu { get; init; }
    public int MatThieu { get; init; }
    public int HuHong { get; init; }
    public int SoLanKiem { get; init; }
}

// ===================== Đồ bỏ quên =====================

public sealed record YeuCauDoBoQuen(string? MaPhong, string? KhuVuc, DateTime? ThoiGianPhatHien, string Loai, string MoTa, string? NguoiNhat, string? ViTriLuuGiu);
public sealed record YeuCauSuaDoBoQuen(string? MaPhong, string? KhuVuc, string Loai, string MoTa, string? NguoiNhat);

public sealed class AnhDoBoQuenDto
{
    public int MaAnh { get; init; }
    public string TenFile { get; init; } = "";
    public long KichThuocByte { get; init; }
    public DateTime ThoiGian { get; init; }
}

public sealed class DoBoQuenDto
{
    public int MaDo { get; init; }
    public string? MaPhong { get; init; }
    public string? KhuVuc { get; init; }
    public DateTime ThoiGianPhatHien { get; init; }
    public string Loai { get; init; } = "";
    public string MoTa { get; init; } = "";
    public string NguoiNhat { get; init; } = "";
    public string NguoiGhiNhan { get; init; } = "";
    public string? ViTriLuuGiu { get; init; }
    public string TrangThai { get; init; } = "";
    public int SoNgayLuuGiu { get; init; }
    public bool DuDieuKienThanhLy { get; init; }
    public DateTime? ThoiGianTra { get; init; }
    public string? NguoiNhanLai { get; init; }
    public string? GiayToNguoiNhan { get; init; }
    public string? NguoiTra { get; init; }
    public string? GhiChuTra { get; init; }
    public DateTime? ThoiGianThanhLy { get; init; }
    public string? NguoiThanhLy { get; init; }
    public string? LyDoThanhLy { get; init; }
    public string? MaPhieuNhan { get; init; }
    public string? HoTenKhach { get; init; }
    public string? SoDTKhach { get; init; }
    public List<AnhDoBoQuenDto> Anhs { get; init; } = [];

    public string MaHienThi => $"DQ{MaDo:0000}";
    public string NoiPhatHienHienThi => string.IsNullOrEmpty(MaPhong) ? KhuVuc ?? "" : MaPhong;
    public string LoaiHienThi => TenLoaiDo(Loai);
    public string TrangThaiHienThi => TrangThai switch
    {
        "DA_TIM_THAY" => "Đã tìm thấy", "DANG_LUU_GIU" => "Đang lưu giữ", "DA_TRA_KHACH" => "Đã trả khách", "DA_THANH_LY" => "Đã thanh lý", _ => TrangThai
    };
    public bool DangCon => TrangThai is "DA_TIM_THAY" or "DANG_LUU_GIU";
    public string KhachGoiYHienThi => string.IsNullOrEmpty(HoTenKhach) ? "" : $"{HoTenKhach} - {SoDTKhach}";
    public string LuuGiuHienThi => TrangThai switch
    {
        "DA_TRA_KHACH" => $"Trả {NguoiNhanLai} lúc {ThoiGianTra:dd/MM HH:mm}",
        "DA_THANH_LY" => $"Thanh lý lúc {ThoiGianThanhLy:dd/MM HH:mm}: {LyDoThanhLy}",
        _ => string.IsNullOrEmpty(ViTriLuuGiu) ? "Chưa lưu kho" : $"{ViTriLuuGiu} · {SoNgayLuuGiu} ngày" + (DuDieuKienThanhLy ? " · đủ hạn thanh lý" : "")
    };

    public static string TenLoaiDo(string loai) => loai switch
    {
        "GIAY_TO" => "Giấy tờ", "DIEN_TU" => "Thiết bị điện tử", "VI_TIEN" => "Ví / tiền", "TRANG_SUC" => "Trang sức / phụ kiện", "QUAN_AO" => "Quần áo", "Y_TE" => "Thuốc / đồ y tế", "KHAC" => "Khác", _ => loai
    };
}

// ===================== Bảng điều hành buồng phòng (Quản lý) =====================

public sealed class PhongBuongPhongDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public int Tang { get; init; }
    public string TenLoaiPhong { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public string GiaiDoan { get; init; } = "";
    public DateTime? TuLuc { get; init; }
    public double? SoGioCho { get; init; }
    public string MucUuTien { get; init; } = "BINH_THUONG";
    public string? LyDoUuTien { get; init; }
    public string? NguoiDon { get; init; }
    public string? HoTenNguoiDon { get; init; }
    public DateTime? BatDauDonLuc { get; init; }
    public string? NguoiPhanCong { get; init; }
    public DateTime? GiaoLuc { get; init; }
    public string? NguoiDonXong { get; init; }
    public string? HoTenNguoiDonXong { get; init; }
    public DateTime? DonXongLuc { get; init; }
    public string? KhachSapDen { get; init; }
    public string? MaPhieuDatSapDen { get; init; }
    public DateOnly? NgayDonSapDen { get; init; }
    public int SuCoDangMo { get; init; }
    public string? LyDoKhoa { get; init; }
    public string? NguoiKhoa { get; init; }

    public string GiaiDoanHienThi => GiaiDoan switch
    {
        "BAN" => "Bẩn - chưa ai nhận", "BAN_CO_KHACH" => "Có khách, cần dọn - chưa ai nhận", "DA_GIAO" => "Đã giao - chưa bắt đầu", "DANG_DON" => "Đang dọn",
        "CHO_KIEM_TRA" => "Đã dọn - chờ kiểm tra", "SAN_SANG" => "Sẵn sàng (đã kiểm tra)", "CO_KHACH" => "Có khách", "BAO_TRI" => "Bảo trì / khóa (OOO)", _ => GiaiDoan
    };
    public string UuTienHienThi => MucUuTien switch { "KHAN" => "Khẩn", "CAO" => "Cao", _ => "Bình thường" };
    public string ChoHienThi => SoGioCho is null ? "" : SoGioCho < 1 ? "dưới 1 giờ" : SoGioCho < 48 ? $"{SoGioCho:0.#} giờ" : $"{SoGioCho / 24:0.#} ngày";
    public string NguoiDonHienThi => !string.IsNullOrEmpty(NguoiDon)
        ? (BatDauDonLuc is null ? $"{HoTenNguoiDon ?? NguoiDon} (được giao, chưa bắt đầu)" : $"{HoTenNguoiDon ?? NguoiDon} (từ {BatDauDonLuc:HH:mm})")
        : !string.IsNullOrEmpty(NguoiDonXong) ? $"{HoTenNguoiDonXong ?? NguoiDonXong} (đã dọn xong{(DonXongLuc is { } x ? $" lúc {x:HH:mm dd/MM}" : "")})"
        : GiaiDoan is "BAN" or "BAN_CO_KHACH" ? "Chưa ai nhận"
        : GiaiDoan is "CHO_KIEM_TRA" or "SAN_SANG" ? "Chưa ghi nhận người dọn" : "";
    /// <summary>Bộ lọc "Người dọn": tên người đang được giao/đang dọn, hoặc người đã dọn xong; rỗng nếu chưa ai nhận.</summary>
    public string NguoiDonLoc => !string.IsNullOrEmpty(NguoiDon) ? (HoTenNguoiDon ?? NguoiDon) : !string.IsNullOrEmpty(NguoiDonXong) ? (HoTenNguoiDonXong ?? NguoiDonXong) : "";
    public string HanhDongTiepTheo => GiaiDoan switch
    {
        "BAN" or "BAN_CO_KHACH" => "Chưa có người dọn → chọn nhân viên buồng phòng rồi bấm “Giao việc”.",
        "DA_GIAO" => "Đã giao, nhân viên chưa bắt đầu → chờ nhân viên bấm “Bắt đầu dọn” (có thể giao lại hoặc thu hồi).",
        "DANG_DON" => "Nhân viên đang dọn → chờ họ báo dọn xong (không giao lại được khi đang dọn).",
        "CHO_KIEM_TRA" => "Đã dọn xong → kiểm tra chất lượng: “Kiểm tra đạt → Sẵn sàng” hoặc “Không đạt → dọn lại” (bắt buộc ghi lý do).",
        "SAN_SANG" => "Phòng đã sẵn sàng đón khách.",
        "BAO_TRI" => "Phòng đang khóa bảo trì → mở lại ở mục “Phòng bảo trì”.",
        _ => ""
    };
    public string KhachSapDenHienThi => KhachSapDen is null ? "" : $"{KhachSapDen} · đón {NgayDonSapDen:dd/MM}" + (MaPhieuDatSapDen is null ? "" : $" · {MaPhieuDatSapDen}");
    public string GhiChuHienThi => string.Join(" · ", new[] { LyDoUuTien, SuCoDangMo > 0 ? $"{SuCoDangMo} sự cố chưa xử lý" : null }.Where(x => !string.IsNullOrEmpty(x)));
    public string GhiChuTongHop => string.Join("  ", new[] { KhachSapDenHienThi, GhiChuHienThi }.Where(x => !string.IsNullOrEmpty(x)));
    public bool CoTheGiao => GiaiDoan is "BAN" or "BAN_CO_KHACH" or "DA_GIAO";
}

public sealed class NhanVienBuongPhongDto
{
    public string TenDN { get; init; } = "";
    public string HoTen { get; init; } = "";
    public string VaiTro { get; init; } = "";
    public bool DangHoatDong { get; init; }
    public List<string> DangDon { get; init; } = [];
    public List<string> DaGiaoChuaBatDau { get; init; } = [];
    public int QuaGio { get; init; }
    public int DaXongHomNay { get; init; }
    public double? ThoiGianTrungBinhPhut { get; init; }

    public string TenHienThi => string.IsNullOrWhiteSpace(HoTen) ? TenDN : HoTen;
    public string VaiTroHienThi => VaiTro switch { "QUAN_LY" => "Quản lý", "BUONG_PHONG" => "Buồng phòng", "LE_TAN" => "Lễ tân", _ => VaiTro };
    public string DangDonHienThi => DangDon.Count == 0 ? "—" : string.Join(", ", DangDon);
    public string DaGiaoHienThi => DaGiaoChuaBatDau.Count == 0 ? "—" : string.Join(", ", DaGiaoChuaBatDau);
    public string TrangThaiHienThi => DangDon.Count > 0 ? (QuaGio > 0 ? $"Đang dọn - {QuaGio} phòng quá giờ" : "Đang dọn") : DaGiaoChuaBatDau.Count > 0 ? "Có việc chưa bắt đầu" : "Rảnh";
    public string TrungBinhHienThi => ThoiGianTrungBinhPhut is null ? "" : $"{ThoiGianTrungBinhPhut:0} phút";
}

public sealed class TongQuanBuongPhongDto
{
    public DateTime CapNhatLuc { get; init; }
    public int TongPhong { get; init; }
    public int VC { get; init; }
    public int VD { get; init; }
    public int VI { get; init; }
    public int OC { get; init; }
    public int OD { get; init; }
    public int OOO { get; init; }
    public int ChuaAiNhan { get; init; }
    public int DaGiaoChuaBatDau { get; init; }
    public int DangDon { get; init; }
    public int ChoKiemTra { get; init; }
    public int SanSang { get; init; }
    public int PhongKhan { get; init; }
    public int DaDonHomNay { get; init; }
    public int DaKiemTraHomNay { get; init; }
    public int SuCoDangMo { get; init; }
    public int SuCoCaoKhanCap { get; init; }
    public int MinibarChoTinhPhi { get; init; }
    public int DoBoQuenDangLuu { get; init; }
    public List<PhongBuongPhongDto> CanXuLy { get; init; } = [];
    public List<PhongBuongPhongDto> SapCheckIn { get; init; } = [];
    public List<PhongBuongPhongDto> PhongKhoa { get; init; } = [];
    public List<NhanVienBuongPhongDto> NhanVien { get; init; } = [];
}

public sealed class NhatKyBuongPhongDto
{
    public DateTime ThoiGian { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public string? HoTenNguoiThucHien { get; init; }
    public string HanhDong { get; init; } = "";
    public string? MaPhong { get; init; }
    public string? Truoc { get; init; }
    public string? Sau { get; init; }
    public string? LyDo { get; init; }
    public string? ChiTiet { get; init; }
    public string? ThayDoiChu { get; init; }
    public string? ChiTietDeDoc { get; init; }

    public string NguoiHienThi => string.IsNullOrEmpty(HoTenNguoiThucHien) ? NguoiThucHien : $"{HoTenNguoiThucHien} ({NguoiThucHien})";
    private static string TenTinhTrang(string? v) => v switch
    {
        "VC" => "Trống, đã dọn", "VD" => "Trống, chưa dọn", "VI" => "Trống, đã kiểm tra", "OC" => "Đang có khách", "OD" => "Có khách, cần dọn", "OOO" => "Ngừng phục vụ / bảo trì", _ => v ?? ""
    };
    public string ThayDoiHienThi => Truoc is not null ? $"{TenTinhTrang(Truoc)} → {TenTinhTrang(Sau)}" : ThayDoiChu ?? "";
    public string GhiChuHienThi => !string.IsNullOrWhiteSpace(LyDo) ? LyDo! : !string.IsNullOrWhiteSpace(ChiTietDeDoc) ? ChiTietDeDoc! : ChiTiet ?? "";
}
