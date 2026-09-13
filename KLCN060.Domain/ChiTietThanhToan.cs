using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class ChiTietThanhToan
{
    public int MaThanhToan { get; set; }
    public string MaHD { get; set; } = null!;
    public HinhThucThanhToan HinhThucThanhToan { get; set; }
    public decimal SoTien { get; set; }
    public DateTime ThoiGianThanhToan { get; set; }
    public string MaTaiKhoanThuNgan { get; set; } = null!;
    public string? MaGiaoDich { get; set; }

    public HoaDon HoaDon { get; set; } = null!;
    public NhanVien NhanVienThuNgan { get; set; } = null!;
}
