namespace KLCN060.Domain;

public class VaiTro
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public string? MoTa { get; set; }

    public ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
    public ICollection<VaiTroQuyen> VaiTroQuyens { get; set; } = new List<VaiTroQuyen>();
}
