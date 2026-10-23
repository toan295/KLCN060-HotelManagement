using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Các lời gọi API dành cho lễ tân: bảng theo dõi ca trực, khách đi cùng, gia hạn, hủy dòng dịch vụ, tìm phòng trống theo ngày.
public sealed partial class ApiClient
{
    // ---------------- Bảng theo dõi ca trực ----------------
    public Task<TongQuanCaTrucDto> GetTongQuanCaTrucAsync() => GetAsync<TongQuanCaTrucDto>("api/v1/frontdesk/overview");
    public Task<List<KhachDenDto>> GetKhachDenAsync(DateOnly ngay) => GetAsync<List<KhachDenDto>>($"api/v1/frontdesk/arrivals?ngay={ngay:yyyy-MM-dd}");
    public Task<List<KhachDiDto>> GetKhachDiAsync(DateOnly ngay) => GetAsync<List<KhachDiDto>>($"api/v1/frontdesk/departures?ngay={ngay:yyyy-MM-dd}");
    public Task<List<KhachDiDto>> GetKhachDangOAsync() => GetAsync<List<KhachDiDto>>("api/v1/frontdesk/in-house");
    public Task<BaoCaoKhongDenDto> GetKhongDenAsync() => GetAsync<BaoCaoKhongDenDto>("api/v1/frontdesk/no-shows");
    public Task<List<PhongTrongDto>> GetPhongTrongTheoNgayAsync(DateOnly nhan, DateOnly tra, string? maLoai = null)
        => GetAsync<List<PhongTrongDto>>($"api/v1/frontdesk/vacant-rooms?nhan={nhan:yyyy-MM-dd}&tra={tra:yyyy-MM-dd}" + (string.IsNullOrEmpty(maLoai) ? "" : "&maLoai=" + Uri.EscapeDataString(maLoai)));

    // ---------------- Khách đi cùng ----------------
    public Task<List<KhachDiCungDto>> GetKhachDiCungAsync(string maPhieuDat) => GetAsync<List<KhachDiCungDto>>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/guests");
    public Task<KhachDiCungDto> ThemKhachDiCungAsync(string maPhieuDat, YeuCauKhachDiCung x) => PostAsync<YeuCauKhachDiCung, KhachDiCungDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/guests", x);
    public Task XoaKhachDiCungAsync(string maPhieuDat, int maKhachDiCung) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/guests/{maKhachDiCung}");

    // ---------------- Lưu trú ----------------
    public Task<KetQuaGiaHanDto> GiaHanLuuTruAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi, string? lyDo)
        => PostAsync<object, KetQuaGiaHanDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/extend", new { ngayTraMoi, lyDo });
    public Task<XemTruocDoiPhongDto> XemTruocDoiPhongAsync(string maPhieuNhan, string maPhong, string? maPhongMoi)
        => GetAsync<XemTruocDoiPhongDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/change-room-preview?maPhongMoi={Uri.EscapeDataString(maPhongMoi ?? "")}");
    public Task<XemTruocGiaHanDto> XemTruocGiaHanAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi)
        => GetAsync<XemTruocGiaHanDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/extend-preview?ngayTraMoi={ngayTraMoi:yyyy-MM-dd}");
    public Task<List<NhatKyDto>> GetLichSuLuuTruAsync(string maPhieuNhan, string maPhong)
        => GetAsync<List<NhatKyDto>>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/history");
    public Task HuyDongDichVuAsync(string maPhieuNhan, string maPhong, int maChiTietDV, string lyDo)
        => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/services/{maChiTietDV}?lyDo={Uri.EscapeDataString(lyDo)}");
}

public sealed record YeuCauKhachDiCung(string HoTen, string? CCCD, string? MaPhong);
public sealed record YeuCauDoiPhongDat(string MaPhongCu, string? MaPhongMoi, string? MaLoaiMoi);

public sealed class KhachDiCungDto
{
    public int MaKhachDiCung { get; init; }
    public string HoTen { get; init; } = "";
    public string? CCCD { get; init; }
    public string? MaPhong { get; init; }
    public DateTime NgayThem { get; init; }
    public string? NguoiThem { get; init; }
    public string PhongHienThi => string.IsNullOrEmpty(MaPhong) ? "chưa xếp" : MaPhong;
    public string CccdHienThi => string.IsNullOrEmpty(CCCD) ? "" : CCCD.Length <= 4 ? "****" : new string('*', CCCD.Length - 4) + CCCD[^4..];
}

public sealed class KetQuaGiaHanDto
{
    public DateOnly? NgayTraDuKienCu { get; init; }
    public DateOnly NgayTraDuKienMoi { get; init; }
    public int SoDemThem { get; init; }
    public decimal DonGia { get; init; }
    public decimal TienThemDuKien { get; init; }
}

public sealed class ViecCanXuLyDto
{
    public string Loai { get; init; } = "";
    public string MucDo { get; init; } = "";
    public string MaThamChieu { get; init; } = "";
    public string MoTa { get; init; } = "";
    public string ManHinhDich { get; init; } = "";
    public string HanhDong { get; init; } = "";
    public string? MaPhong { get; init; }
    public string? MaPhieuNhan { get; init; }
    public string MucDoHienThi => MucDo switch { "NGHIEM_TRONG" => "Nghiêm trọng", "CANH_BAO" => "Cảnh báo", _ => "Thông tin" };
    public string LoaiHienThi => Loai switch
    {
        "CHO_COC" => "Chờ cọc",
        "QUA_HAN_KHONG_DEN" => "Quá ngày đón",
        "TRA_PHONG_QUA_HAN" => "Quá hạn trả",
        "PHONG_CHUA_SAN_SANG" => "Phòng chưa sẵn sàng",
        "HOA_DON_CHUA_THU" => "Hóa đơn chưa thu",
        "HOA_DON_CON_NUA" => "Còn hóa đơn nợ",
        "PHONG_BAO_TRI" => "Phòng bảo trì",
        "SU_CO_MO" => "Sự cố",
        "MINIBAR_CHO_TINH_PHI" => "Minibar chờ tính phí",
        "DO_BO_QUEN" => "Đồ bỏ quên",
        _ => Loai
    };
}

public sealed class TongQuanCaTrucDto
{
    public DateOnly Ngay { get; init; }
    public DateTime CapNhatLuc { get; init; }
    public int TongSoPhong { get; init; }
    public int PhongTrong { get; init; }
    public int PhongTrongBanDuoc { get; init; }
    public int PhongGiuChoKhachDen { get; init; }
    public int PhongDangCoKhach { get; init; }
    public int PhongChoDon { get; init; }
    public int PhongCoKhachCanDon { get; init; }
    public int PhongDangDon { get; init; }
    public int PhongBaoTri { get; init; }
    public int BookingDenHomNay { get; init; }
    public int BookingChoNhanPhong { get; init; }
    public int PhongSapCheckIn { get; init; }
    public int BookingDaNhanHomNay { get; init; }
    public int PhongSapCheckOut { get; init; }
    public int PhongDaTraHomNay { get; init; }
    public int PhongQuaHanTra { get; init; }
    public int KhachDangLuuTru { get; init; }
    public int DatCocChoThu { get; init; }
    public decimal TongTienCocChoThu { get; init; }
    public int HoaDonConNo { get; init; }
    public decimal TongConPhaiThu { get; init; }
    public int KhongDenChoXuLy { get; init; }
    public int SuCoDangMo { get; init; }
    public int MinibarChoTinhPhi { get; init; }
    public int DoBoQuenDangLuu { get; init; }
    public int BookingCanXuLy { get; init; }
    public int ViecKhacCanXuLy { get; init; }
    public List<ViecCanXuLyDto> CanXuLy { get; init; } = [];
}

public sealed class PhongKhachDenDto
{
    public string MaPhong { get; init; } = "";
    public string MaLoai { get; init; } = "";
    public string TinhTrang { get; init; } = "";
    public bool DaNhan { get; init; }
    public bool SanSang { get; init; }
}

public sealed class KhachDenDto
{
    public string MaPhieuDat { get; init; } = "";
    public string HoTenKhach { get; init; } = "";
    public string SoDTKhach { get; init; } = "";
    public string TrangThai { get; init; } = "";
    public string LoaiDatPhong { get; init; } = "";
    public DateOnly NgayDonDuKien { get; init; }
    public DateOnly NgayTraDuKien { get; init; }
    public decimal TienCoc { get; init; }
    public bool DaCoc { get; init; }
    public int SoKhachDiCung { get; init; }
    public bool CoTheNhanNgay { get; init; }
    public List<PhongKhachDenDto> Phongs { get; init; } = [];
    /// <summary>Ngày + giờ trả phòng dự kiến của phiếu (giờ trả phòng chuẩn 12:00).</summary>
    public string NgayTraHienThi => $"{NgayTraDuKien:dd/MM/yyyy} 12:00";
    public string TrangThaiHienThi => TrangThai switch
    {
        "CHO_XAC_NHAN" => "Chờ xác nhận cọc", "DA_XAC_NHAN" => "Đã cọc", "DA_NHAN_PHONG" => "Đã nhận phòng", "DA_TRA_PHONG" => "Đã trả phòng", _ => TrangThai
    };
    public string PhongHienThi => string.Join(", ", Phongs.Select(p => p.DaNhan ? $"{p.MaPhong} (đã nhận)" : p.SanSang ? p.MaPhong : $"{p.MaPhong} (chưa sẵn sàng)"));
    /// <summary>Có đi cùng ai không: luôn có chữ (không để trống) để lễ tân biết đã kiểm tra.</summary>
    public string KhachDiCungHienThi => SoKhachDiCung == 0 ? "Một mình" : $"+{SoKhachDiCung} người";
    /// <summary>Phiếu đã sẵn sàng nhận phòng chưa, và nếu chưa thì vướng ở đâu (cọc / phòng nào chưa dọn).</summary>
    public string SanSangHienThi => TrangThai switch
    {
        "CHO_XAC_NHAN" => "Chưa thu cọc",
        "DA_NHAN_PHONG" or "DA_TRA_PHONG" => "Đã nhận phòng",
        "DA_XAC_NHAN" when CoTheNhanNgay => "Nhận được ngay",
        "DA_XAC_NHAN" => Phongs.Where(p => !p.DaNhan && !p.SanSang).Select(p => $"{p.MaPhong} {Converters.TrangThaiPhongConverter.MoTa(p.TinhTrang).ToLowerInvariant()}").ToList() is { Count: > 0 } chua
            ? "Chờ phòng: " + string.Join(", ", chua) : "Chờ xử lý",
        _ => "-"
    };
}

public sealed class KhachDiDto
{
    public string MaPhieuNhan { get; init; } = "";
    public string MaPhong { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? HoTenKhach { get; init; }
    public string? SoDTKhach { get; init; }
    public DateOnly NgayNhan { get; init; }
    public DateOnly? NgayTraDuKien { get; init; }
    public DateOnly? NgayTra { get; init; }
    public int SoNguoi { get; init; }
    public decimal DonGia { get; init; }
    public string TrangThai { get; init; } = "";
    public bool QuaHan { get; init; }
    public bool DangO => TrangThai == "DANG_O";
    public string TrangThaiHienThi => TrangThai == "DA_TRA_PHONG" ? "Đã trả" : QuaHan ? "Quá hạn trả" : "Đang ở";
    /// <summary>Thời gian check-out cụ thể: ngày trả + giờ trả phòng chuẩn của khách sạn (khách đã trả thì ghi "đã trả"); khách lưu trú luôn có ngày trả.</summary>
    public string NgayTraHienThi => NgayTra is { } t ? $"{t:dd/MM/yyyy} (đã trả)" : NgayTraDuKien is { } d ? $"{d:dd/MM/yyyy} {GioTraPhongChuan}" : "chưa có ngày trả";
    public const string GioTraPhongChuan = "12:00";
}

public sealed class PhongTrongDto
{
    public string MaPhong { get; init; } = "";
    public string TenPhong { get; init; } = "";
    public int Tang { get; init; }
    public string MaLoai { get; init; } = "";
    public string TenLoai { get; init; } = "";
    public decimal DonGia { get; init; }
    public string TinhTrang { get; init; } = "";
    public bool NhanNgayDuoc { get; init; }
    public string TinhTrangHienThi => Converters.TrangThaiPhongConverter.MoTa(TinhTrang);
    public string MoTa => $"{TenPhong} - {TenLoai} ({TinhTrangHienThi})";
}

public sealed class BaoCaoKhongDenDto
{
    public DateOnly TuNgay { get; init; }
    public DateOnly DenNgay { get; init; }
    public List<PhieuDatTomTatDto> DaDanhDau { get; init; } = [];
    public List<PhieuDatTomTatDto> ChoXuLy { get; init; } = [];
}

public sealed class XemTruocDoiPhongDto
{
    public string MaPhieuNhan { get; init; } = "";
    public string? MaPhieuDat { get; init; }
    public string? HoTenKhach { get; init; }
    public string MaPhongCu { get; init; } = "";
    public string TenLoaiCu { get; init; } = "";
    public decimal DonGiaCu { get; init; }
    public int SoDemConLai { get; init; }
    public int SoNguoi { get; init; }
    public string? MaPhongMoi { get; init; }
    public string? TenLoaiMoi { get; init; }
    public decimal DonGiaBangGia { get; init; }
    public decimal DonGiaApDung { get; init; }
    public string? LoaiDoi { get; init; }
    public decimal ChenhLechMoiDem { get; init; }
    public decimal PhuThuNguoiCuMoiDem { get; init; }
    public decimal PhuThuNguoiMoiMoiDem { get; init; }
    public decimal TacDongTong { get; init; }
    public bool CoTheDoi { get; init; }
    public string? MaLoi { get; init; }
    public string? LyDoKhongDoi { get; init; }
    public string? HuongDan { get; init; }
    public List<string> CanhBao { get; init; } = [];

    public string LoaiDoiHienThi => LoaiDoi switch
    {
        "NANG_HANG" => "NÂNG HẠNG - CÓ TÍNH TIỀN",
        "HA_HANG" => "HẠ HẠNG - GIẢM TIỀN PHÒNG",
        "NGANG_HANG" => "ĐỔI NGANG HẠNG - KHÔNG ĐỔI GIÁ",
        _ => ""
    };
    public string TuDenHienThi => MaPhongMoi is null ? "" : $"{MaPhongCu} ({TenLoaiCu}, {DonGiaCu:N0}đ/đêm)  →  {MaPhongMoi} ({TenLoaiMoi}, {DonGiaApDung:N0}đ/đêm)";
    public string TienHienThi => MaPhongMoi is null || !CoTheDoi ? "" :
        $"Chênh lệch {(ChenhLechMoiDem >= 0 ? "+" : "")}{ChenhLechMoiDem:N0}đ/đêm × {SoDemConLai} đêm còn lại" +
        (PhuThuNguoiMoiMoiDem != PhuThuNguoiCuMoiDem ? $" · phụ thu người ở thêm {PhuThuNguoiCuMoiDem:N0} → {PhuThuNguoiMoiMoiDem:N0}đ/đêm" : "") +
        $"  =>  tổng {(TacDongTong >= 0 ? "+" : "")}{TacDongTong:N0}đ";
}
public sealed class XemTruocGiaHanDto
{
    public string? HoTenKhach { get; init; }
    public DateOnly? NgayTraDuKienCu { get; init; }
    public DateOnly NgayTraMoi { get; init; }
    public int SoDemThem { get; init; }
    public decimal DonGia { get; init; }
    public decimal TienThemDuKien { get; init; }
    public bool CoTheGiaHan { get; init; }
    public string? LyDoKhongGiaHan { get; init; }
    public string? MaPhieuDatKeTiep { get; init; }
    public DateOnly? NgayToiDaGiaHan { get; init; }
    public string TomTat => CoTheGiaHan
        ? $"Gia hạn {SoDemThem} đêm: {NgayTraDuKienCu:dd/MM/yyyy} → {NgayTraMoi:dd/MM/yyyy} × {DonGia:N0}đ/đêm = +{TienThemDuKien:N0}đ" + (NgayToiDaGiaHan is { } m ? $" (phòng có phiếu {MaPhieuDatKeTiep} từ {m:dd/MM/yyyy}: tối đa tới ngày đó)" : "")
        : (LyDoKhongGiaHan ?? "");
}
