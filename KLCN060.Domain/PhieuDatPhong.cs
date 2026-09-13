using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class PhieuDatPhong
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public decimal TienCoc { get; set; }
    public TrangThaiPhieuDat TrangThai { get; set; }
    public LoaiDatPhong LoaiDatPhong { get; set; }

    public Khach Khach { get; set; } = null!;
    public ICollection<ChiTietPhieuDat> ChiTietPhieuDats { get; set; } = new List<ChiTietPhieuDat>();
    public ICollection<ChiTietKhuyenMai> ChiTietKhuyenMais { get; set; } = new List<ChiTietKhuyenMai>();
    public PhieuNhanPhong? PhieuNhanPhong { get; set; }
}
