namespace KLCN060.Web.Models.Booking;

/// <summary>Khớp với BookingDto bên KLCN060.Api - dùng cho W8 (xác nhận cọc) và W10 (chi tiết đơn).</summary>
public class BookingViewModel
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public decimal TienCoc { get; set; }
    public decimal TongTienDuKien { get; set; }
    public decimal TongKhuyenMai { get; set; }
    public List<HeldRoomViewModel> PhongDaGiu { get; set; } = new();
}

public class HeldRoomViewModel
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public decimal DonGiaApDung { get; set; }
    public decimal ThanhTien { get; set; }
}
