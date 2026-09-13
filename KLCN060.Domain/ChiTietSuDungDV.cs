namespace KLCN060.Domain;

public class ChiTietSuDungDV
{
    public int MaChiTietDV { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public DateTime NgaySuDung { get; set; }
    public decimal ThanhTien { get; set; }

    public PhieuNhanPhong PhieuNhanPhong { get; set; } = null!;
    public DichVu DichVu { get; set; } = null!;
}
