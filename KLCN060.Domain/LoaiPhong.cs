namespace KLCN060.Domain;

public class LoaiPhong
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }
    public decimal DonGia { get; set; }
    public decimal PhuThu { get; set; }

    public ICollection<Phong> Phongs { get; set; } = new List<Phong>();
}
