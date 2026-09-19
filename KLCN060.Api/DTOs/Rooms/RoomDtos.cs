namespace KLCN060.Api.DTOs.Rooms;

public class RoomRequest
{
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string MaLoai { get; set; } = null!;
    public string? TinhTrang { get; set; }
}

public class RoomStatusRequest
{
    public string TinhTrang { get; set; } = null!;
    public string? GhiChu { get; set; }
}

public class RoomDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TinhTrang { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;
}
