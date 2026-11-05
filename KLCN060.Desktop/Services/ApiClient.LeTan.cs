using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Các lời gọi API dành cho lễ tân: bảng theo dõi ca trực, khách đi cùng, gia hạn, hủy dòng dịch vụ, tìm phòng trống theo ngày.
public sealed partial class ApiClient
{
    // ---------------- Bảng theo dõi ca trực ----------------

    // ---------------- Khách đi cùng ----------------
    public Task<KhachDiCungDto> ThemKhachDiCungAsync(string maPhieuDat, YeuCauKhachDiCung x) => PostAsync<YeuCauKhachDiCung, KhachDiCungDto>($"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/guests", x);
    public Task XoaKhachDiCungAsync(string maPhieuDat, int maKhachDiCung) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/bookings/{Uri.EscapeDataString(maPhieuDat)}/guests/{maKhachDiCung}");

    // ---------------- Lưu trú ----------------
    public Task<KetQuaGiaHanDto> GiaHanLuuTruAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi, string? lyDo)
        => PostAsync<object, KetQuaGiaHanDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/extend", new { ngayTraMoi, lyDo });
    public Task<XemTruocDoiPhongDto> XemTruocDoiPhongAsync(string maPhieuNhan, string maPhong, string? maPhongMoi)
        => GetAsync<XemTruocDoiPhongDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/change-room-preview?maPhongMoi={Uri.EscapeDataString(maPhongMoi ?? "")}");
    public Task<XemTruocGiaHanDto> XemTruocGiaHanAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi)
        => GetAsync<XemTruocGiaHanDto>($"api/v1/stays/{Uri.EscapeDataString(maPhieuNhan)}/{Uri.EscapeDataString(maPhong)}/extend-preview?ngayTraMoi={ngayTraMoi:yyyy-MM-dd}");
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
