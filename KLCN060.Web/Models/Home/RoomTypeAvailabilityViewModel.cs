namespace KLCN060.Web.Models.Home;

/// <summary>Khớp với RoomTypeAvailabilityDto bên KLCN060.Api (GET /room-types/availability?checkin=&checkout=&guests=).</summary>
public class RoomTypeAvailabilityViewModel
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }
    public decimal DonGia { get; set; }
    public decimal PhuThu { get; set; }
    public int SoPhongTrong { get; set; }
}
