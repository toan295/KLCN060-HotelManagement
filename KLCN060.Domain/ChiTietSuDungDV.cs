namespace KLCN060.Domain;

public class ChiTietSuDungDV
{
    public int MaChiTietDV { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    public DateTime NgaySuDung { get; set; }
    public decimal ThanhTien { get; set; }

    /// <summary>Tên và đơn vị của dịch vụ TẠI THỜI ĐIỂM ghi: đổi tên/đơn vị dịch vụ sau này không làm đổi hóa đơn và lịch sử cũ (null = dữ liệu cũ, lấy theo danh mục).</summary>
    public string? TenDichVu { get; set; }
    public string? DonViTinh { get; set; }

    /// <summary>Gói dịch vụ mà dòng này thuộc về (null = dịch vụ lẻ).</summary>
    public string? MaGoi { get; set; }

    /// <summary>Phòng (lượt ở) mà dịch vụ được ghi cho: phiếu nhiều phòng phải biết dòng này của phòng nào (null = dữ liệu cũ trước khi có cột này).</summary>
    public string? MaPhong { get; set; }

    /// <summary>Tài khoản đã ghi dòng này (null = dữ liệu cũ).</summary>
    public string? NguoiGhi { get; set; }

    public PhieuNhanPhong PhieuNhanPhong { get; set; } = null!;
    public DichVu DichVu { get; set; } = null!;
    public GoiDichVu? Goi { get; set; }
}
