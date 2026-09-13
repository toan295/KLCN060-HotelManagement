namespace KLCN060.Domain;

public class PhieuDoiPhong
{
    public string MaPhieuDoi { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhongCu { get; set; } = null!;
    public string MaPhongMoi { get; set; } = null!;
    public DateTime NgayDoi { get; set; }
    public decimal ChenhLechGia { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>FK kép (MaPhieuNhan, MaPhongCu) trỏ về khóa chính kép của ChiTietPhieuNhan.</summary>
    public ChiTietPhieuNhan ChiTietPhieuNhanCu { get; set; } = null!;
    public Phong PhongMoi { get; set; } = null!;
}
