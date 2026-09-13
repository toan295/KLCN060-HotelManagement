namespace KLCN060.Domain;

/// <summary>Bảng N-N VaiTro_Quyen.</summary>
public class VaiTroQuyen
{
    public int MaVaiTro { get; set; }
    public int MaQuyen { get; set; }

    public VaiTro VaiTro { get; set; } = null!;
    public Quyen Quyen { get; set; } = null!;
}
