using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Guests;

public class GuestRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.HoTen
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.CCCD
    public string? CCCD { get; set; }
    public string? Email { get; set; }
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.DiaChi
    public string? DiaChi { get; set; }
}

/// <summary>Khách tự sửa hồ sơ: KHÔNG có CCCD (chỉ lễ tân nhập lúc check-in).</summary>
public class UpdateProfileRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.HoTen
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? Email { get; set; }
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.DiaChi
    public string? DiaChi { get; set; }

    /// <summary>Gom các field lạ để từ chối rõ ràng nếu client cố gửi CCCD (thay vì âm thầm bỏ qua).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? TruongKhac { get; set; }
}

public class GuestDto
{
    public string MaKhach { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    // Tổng hợp để nhân viên nhận diện khách ngay trên danh sách (chỉ có ở kết quả tìm kiếm / hồ sơ chi tiết)
    public int SoLuotO { get; set; }
    public DateOnly? LanOCuoi { get; set; }
    public bool DangO { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongChiTieu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    /// <summary>CA_NHAN hoặc DOANH_NGHIEP (hiển thị là "Đoàn": khách đã từng đặt theo đoàn / tên có chữ Công ty).</summary>
    public string LoaiKhach { get; set; } = "CA_NHAN";
}

/// <summary>Khách khác trong hệ thống có thể là cùng một người với hồ sơ đang xem / sắp tạo.</summary>
public class GuestDuplicateDto
{
    public string MaKhach { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    /// <summary>CHAC_CHAN (trùng CCCD / SĐT - không được tạo trùng) hoặc NGHI_NGO (trùng họ tên / email - nên kiểm tra).</summary>
    public string Muc { get; set; } = null!;
    public List<string> LyDo { get; set; } = new();
}

public class GuestProfileChangeDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? ChiTiet { get; set; }
}

public class GuestStayHistoryDto
{
    /// <summary>Null với lượt ở của khách vãng lai (nhận phòng trực tiếp, không qua phiếu đặt).</summary>
    public string? MaPhieuDat { get; set; }

    /// <summary>Mã phiếu nhận phòng của lượt ở vãng lai (null với phiếu đặt trước).</summary>
    public string? MaPhieuNhan { get; set; }

    /// <summary>DAT_TRUOC hoặc VANG_LAI.</summary>
    public string LoaiLuuTru { get; set; } = "DAT_TRUOC";

    /// <summary>Các phòng đã ở (chỉ có với lượt vãng lai).</summary>
    public string? Phongs { get; set; }
    public string TrangThai { get; set; } = null!;
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTien { get; set; }

    // Phòng - check-in/check-out thực tế - cọc - thanh toán của lượt ở
    public string? DanhSachPhong { get; set; }
    public DateTime? NgayNhanThucTe { get; set; }
    public DateOnly? NgayTraThucTe { get; set; }
    public string? NguonDat { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienCoc { get; set; }
    public bool DaThuCoc { get; set; }
    public string? MaHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
}

public class GuestDetailDto : GuestDto
{
    public List<GuestStayHistoryDto> LichSuLuuTru { get; set; } = new();
    public List<GuestDuplicateDto> NghiTrung { get; set; } = new();
    public List<GuestProfileChangeDto> LichSuHoSo { get; set; } = new();
    public int SoLuotHuy { get; set; }
    public int SoLuotKhongDen { get; set; }
}
