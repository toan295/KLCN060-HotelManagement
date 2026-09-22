namespace KLCN060.Web.Models.Booking;

/// <summary>Khớp với CancelBookingResultDto bên KLCN060.Api - kết quả hủy đặt phòng (W10).</summary>
public class CancelResultViewModel
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public bool HoanCoc { get; set; }
    public bool GhiDeChinhSach { get; set; }
    public int SoNgayConLai { get; set; }
    public decimal SoTienHoan { get; set; }
}
