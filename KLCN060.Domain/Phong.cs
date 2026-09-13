using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class Phong
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public TinhTrangPhong TinhTrang { get; set; }
    public string MaLoai { get; set; } = null!;

    public LoaiPhong LoaiPhong { get; set; } = null!;
    public ICollection<CoSoVatChat> CoSoVatChats { get; set; } = new List<CoSoVatChat>();
    public ICollection<ChiTietPhieuDat> ChiTietPhieuDats { get; set; } = new List<ChiTietPhieuDat>();
    public ICollection<ChiTietPhieuNhan> ChiTietPhieuNhans { get; set; } = new List<ChiTietPhieuNhan>();
    public ICollection<PhieuDoiPhong> PhieuDoiPhongMois { get; set; } = new List<PhieuDoiPhong>();
}
