namespace KLCN060.Domain;

public class Quyen
{
    public int MaQuyen { get; set; }
    public string TenQuyen { get; set; } = null!;
    public string? NhomChucNang { get; set; }

    public ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = new List<VaiTroQuyen>();
}
