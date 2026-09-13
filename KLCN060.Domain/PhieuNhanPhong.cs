namespace KLCN060.Domain;

public class PhieuNhanPhong
{
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public DateTime NgayNhan { get; set; }

    public PhieuDatPhong? PhieuDatPhong { get; set; }
    public ICollection<ChiTietPhieuNhan> ChiTietPhieuNhans { get; set; } = new List<ChiTietPhieuNhan>();
    public ICollection<ChiTietSuDungDV> ChiTietSuDungDVs { get; set; } = new List<ChiTietSuDungDV>();
    public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
}
