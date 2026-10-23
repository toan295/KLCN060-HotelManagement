using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Sự cố trong phòng (điều hòa hỏng, bóng đèn cháy, rò nước...) do buồng phòng/lễ tân báo, Quản lý tiếp nhận và xử lý.</summary>
public class SuCoPhong
{
    public int MaSuCo { get; set; }
    public string MaPhong { get; set; } = null!;
    public LoaiSuCo Loai { get; set; }
    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }
    public MucDoSuCo MucDo { get; set; }
    public TrangThaiSuCo TrangThai { get; set; }

    /// <summary>Tên đăng nhập người báo.</summary>
    public string NguoiBao { get; set; } = null!;
    public DateTime ThoiGianBao { get; set; }

    public string? NguoiXuLy { get; set; }
    public DateTime? ThoiGianTiepNhan { get; set; }
    public DateTime? ThoiGianXong { get; set; }
    public string? GhiChuXuLy { get; set; }

    /// <summary>Tài khoản đóng sự cố (xử lý xong hoặc hủy); null khi sự cố còn mở. Có thể khác người tiếp nhận (<see cref="NguoiXuLy"/>).</summary>
    public string? NguoiDong { get; set; }

    public Phong Phong { get; set; } = null!;
}
