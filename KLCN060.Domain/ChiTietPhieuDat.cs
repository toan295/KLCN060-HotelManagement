namespace KLCN060.Domain;

/// <summary>Khóa chính kép (MaPhieuDat, MaPhong) — "BookingRoom" trước khi nhận phòng.</summary>
public class ChiTietPhieuDat
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public decimal DonGiaApDung { get; set; }
    public decimal ThanhTien { get; set; }

    public PhieuDatPhong PhieuDatPhong { get; set; } = null!;
    public Phong Phong { get; set; } = null!;
}
