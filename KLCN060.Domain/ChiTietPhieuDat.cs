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

    /// <summary>Giá chốt từng đêm (rỗng với phiếu tạo trước khi có bảng giá từng đêm: khi đó giá chốt là DonGiaApDung × số đêm).</summary>
    public ICollection<GiaDemDatPhong> GiaTheoDems { get; set; } = new List<GiaDemDatPhong>();
}
