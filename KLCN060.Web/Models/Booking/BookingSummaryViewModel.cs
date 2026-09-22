namespace KLCN060.Web.Models.Booking;

/// <summary>Khớp với BookingSummaryDto bên KLCN060.Api - dùng cho W9 (lịch sử đặt phòng).</summary>
public class BookingSummaryViewModel
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
    public decimal TienCoc { get; set; }
    public decimal TongTienDuKien { get; set; }
}
