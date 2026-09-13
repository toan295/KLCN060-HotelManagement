using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Bảng xác thực duy nhất cho toàn hệ thống (nhân viên và khách hàng).</summary>
public class TaiKhoan
{
    public string TenDN { get; set; } = null!;
    public string MatKhau { get; set; } = null!;
    public LoaiTaiKhoan LoaiTaiKhoan { get; set; }
    public string? MaNV { get; set; }
    public string? MaKhach { get; set; }
    public TrangThaiTaiKhoan TrangThai { get; set; } = TrangThaiTaiKhoan.HOAT_DONG;
    public int SoLanDangNhapSai { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    public NhanVien? NhanVien { get; set; }
    public Khach? Khach { get; set; }
    public ICollection<NhatKyThaoTac> NhatKyThaoTacs { get; set; } = new List<NhatKyThaoTac>();
    public ICollection<LichSuSaoLuu> LichSuSaoLuus { get; set; } = new List<LichSuSaoLuu>();
    public ICollection<NhatKyBanGiaoCa> NhatKyBanGiaoCaGiaos { get; set; } = new List<NhatKyBanGiaoCa>();
    public ICollection<NhatKyBanGiaoCa> NhatKyBanGiaoCaNhans { get; set; } = new List<NhatKyBanGiaoCa>();
}
