namespace KLCN060.Api.DTOs.Facilities;

public class FacilityRequest
{
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public string TinhTrang { get; set; } = null!;
}

public class FacilityDto
{
    public string MaSo { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; }
    public string TinhTrang { get; set; } = null!;
    public string? MaPhong { get; set; }
}
