using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class HoaDon
{
    public string MaHD { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string MaNV { get; set; } = null!;
    public DateTime NgayLap { get; set; }
    public decimal TienPhong { get; set; }
    public decimal TienDV { get; set; }
    public decimal PhuThu { get; set; }
    public decimal TienDaCoc { get; set; }
    public decimal TongTien { get; set; }
    public HinhThucThanhToan HinhThucThanhToan { get; set; }
    public TrangThaiThanhToan TrangThaiThanhToan { get; set; } = TrangThaiThanhToan.CHUA_THANH_TOAN;

    public PhieuNhanPhong PhieuNhanPhong { get; set; } = null!;
    public NhanVien NhanVien { get; set; } = null!;
    public ICollection<ChiTietHoaDon> ChiTietHoaDons { get; set; } = new List<ChiTietHoaDon>();
    public ICollection<ChiTietThanhToan> ChiTietThanhToans { get; set; } = new List<ChiTietThanhToan>();
}
