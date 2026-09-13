using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Khóa chính kép (MaPhieuNhan, MaPhong) — "BookingRoom" sau khi khách thực sự có mặt.</summary>
public class ChiTietPhieuNhan
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoNguoi { get; set; }
    public decimal DonGia { get; set; }
    public decimal TienPhuThu { get; set; }
    public TrangThaiChiTietPhieuNhan TrangThai { get; set; }

    public PhieuNhanPhong PhieuNhanPhong { get; set; } = null!;
    public Phong Phong { get; set; } = null!;
}
