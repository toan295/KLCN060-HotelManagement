namespace KLCN060.Domain;

public class Khach
{
    public string MaKhach { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }

    public TaiKhoan? TaiKhoan { get; set; }
    public ICollection<PhieuDatPhong> PhieuDatPhongs { get; set; } = new List<PhieuDatPhong>();
}
