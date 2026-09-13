using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class ChiTietHoaDon
{
    public int MaChiTietHoaDon { get; set; }
    public string MaHD { get; set; } = null!;
    public LoaiKhoanMuc LoaiKhoanMuc { get; set; }
    public string MoTa { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public decimal DonGia { get; set; }
    public decimal ThanhTien { get; set; }

    public HoaDon HoaDon { get; set; } = null!;
}
