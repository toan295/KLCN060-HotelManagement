namespace KLCN060.Domain;

/// <summary>Khóa chính kép (MaPhieuDat, MaKM).</summary>
public class ChiTietKhuyenMai
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKM { get; set; } = null!;
    public decimal TongKhuyenMai { get; set; }

    public PhieuDatPhong PhieuDatPhong { get; set; } = null!;
    public KhuyenMai KhuyenMai { get; set; } = null!;
}
