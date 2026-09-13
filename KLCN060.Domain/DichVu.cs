namespace KLCN060.Domain;

public class DichVu
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public decimal GiaDV { get; set; }
    public string DonViTinh { get; set; } = null!;

    public ICollection<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; } = new List<ChiTietSuDungDV>();
}
