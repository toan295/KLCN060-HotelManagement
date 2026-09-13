using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class KhuyenMai
{
    public string MaKM { get; set; } = null!;
    public string TenKM { get; set; } = null!;
    public decimal? PhanTramKM { get; set; }
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public string? DieuKien { get; set; }
    public LoaiKhuyenMai LoaiKM { get; set; }
    public decimal? GiaTri { get; set; }
    public int? SoLuongGioiHan { get; set; }
    public int SoLuongDaSuDung { get; set; }

    public ICollection<ChiTietKhuyenMai> ChiTietKhuyenMais { get; set; } = new List<ChiTietKhuyenMai>();
}
