namespace KLCN060.Domain;

public class CoSoVatChat
{
    public string MaSo { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public string TinhTrang { get; set; } = null!;
    public string? MaPhong { get; set; }

    public Phong? Phong { get; set; }
}
