namespace KLCN060.Domain;

public class NhanVien
{
    public string MaNV { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string ChucVu { get; set; } = null!;
    public int MaVaiTro { get; set; }

    public VaiTro VaiTro { get; set; } = null!;
    public TaiKhoan? TaiKhoan { get; set; }
    public ICollection<HoaDon> HoaDons { get; set; } = new List<HoaDon>();
    public ICollection<ChiTietThanhToan> ChiTietThanhToans { get; set; } = new List<ChiTietThanhToan>();
}
