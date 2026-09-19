using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Bookings;

public class BookingRoomRequest
{
    public string MaLoai { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
}

public class CreateBookingRequest
{
    public string LoaiDatPhong { get; set; } = null!;

    /// <summary>KH: bị bỏ qua (lấy từ JWT). LT: bắt buộc.</summary>
    public string? MaKhach { get; set; }

    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public List<BookingRoomRequest> PhongCanDat { get; set; } = new();
    public string? MaKhuyenMai { get; set; }
}

public class HeldRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaApDung { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
}

public class BookingDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongKhuyenMai { get; set; }

    public List<HeldRoomDto> PhongDaGiu { get; set; } = new();
}

public class BookingSummaryDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string SoDTKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public int SoPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienDuKien { get; set; }
}

public class BookingListQuery
{
    public string? TrangThai { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ----- Check-in -----

public class CheckInGuestInfo
{
    public string MaPhong { get; set; } = null!;
    public string? CCCD { get; set; }
    public int SoNguoi { get; set; }
}

public class BookingCheckInRequest
{
    public List<string> PhongNhan { get; set; } = new();
    public List<CheckInGuestInfo> Khach { get; set; } = new();
}

public class WalkInCheckInRequest
{
    /// <summary>Khách lưu trú (tạo trước bằng POST /guests nếu chưa có) — nơi lưu CCCD.</summary>
    public string MaKhach { get; set; } = null!;

    /// <summary>Mỗi phần tử = 1 phòng cụ thể được nhận.</summary>
    public List<CheckInGuestInfo> Khach { get; set; } = new();
}

public class CheckedInRoomDto
{
    public string MaPhong { get; set; } = null!;
    public int SoNguoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    public string TinhTrangPhong { get; set; } = null!;
}

public class CheckInResultDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? TrangThaiPhieuDat { get; set; }
    public DateTime NgayNhan { get; set; }
    public List<CheckedInRoomDto> PhongDaNhan { get; set; } = new();
}

// ----- Hủy đặt phòng -----

public class CancelBookingRequest
{
    public string? LyDo { get; set; }

    /// <summary>Chỉ lễ tân: ghi đè chính sách hoàn cọc mặc định (mốc 30 ngày). Khi true phải có LyDo và HoanCoc.</summary>
    public bool GhiDeChinhSach { get; set; }

    /// <summary>Kết quả hoàn cọc mong muốn khi GhiDeChinhSach = true.</summary>
    public bool? HoanCoc { get; set; }
}

public class CancelBookingResultDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public bool HoanCoc { get; set; }
    public bool GhiDeChinhSach { get; set; }

    /// <summary>Số ngày từ hôm nay tới ngày đón dự kiến, dùng để đối chiếu mốc 30 ngày.</summary>
    public int SoNgayConLai { get; set; }

    /// <summary>Số tiền cọc thực tế cần hoàn (0 nếu không hoàn hoặc phiếu chưa xác nhận đặt cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTienHoan { get; set; }
}
