namespace KLCN060.Domain;

public class NhatKyBanGiaoCa
{
    public int MaBanGiao { get; set; }
    public string MaTaiKhoanGiao { get; set; } = null!;
    public string MaTaiKhoanNhan { get; set; } = null!;
    public DateTime ThoiGianBanGiao { get; set; }
    public decimal TongTienMatDauCa { get; set; }
    public decimal TongTienMatCuoiCa { get; set; }
    public int SoLuongPhieuTrongCa { get; set; }
    public string? GhiChu { get; set; }

    public TaiKhoan TaiKhoanGiao { get; set; } = null!;
    public TaiKhoan TaiKhoanNhan { get; set; } = null!;
}
