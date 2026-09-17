namespace KLCN060.Web.Models.Home;

/// <summary>Khớp với RoomTypeDto bên KLCN060.Api (GET /room-types, GET /room-types/{id}).</summary>
public class RoomTypeViewModel
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }
    public decimal DonGia { get; set; }
    public decimal PhuThu { get; set; }
}
