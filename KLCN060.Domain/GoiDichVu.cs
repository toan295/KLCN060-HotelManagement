namespace KLCN060.Domain;

/// <summary>
/// Gói dịch vụ (package): tập hợp vài dịch vụ bán chung với một giá gói (VD "Gói ăn sáng + giặt là"). Khi ghi gói cho một lượt lưu trú, hệ thống tạo
/// một dòng sử dụng cho từng dịch vụ trong gói, chia giá gói theo tỷ lệ giá lẻ - hóa đơn vẫn chỉ cộng các dòng sử dụng dịch vụ như trước.
/// </summary>
public class GoiDichVu
{
    public string MaGoi { get; set; } = null!;
    public string TenGoi { get; set; } = null!;
    public string? MoTa { get; set; }
    public decimal GiaGoi { get; set; }
    public bool HoatDong { get; set; } = true;
    public DateTime NgayTao { get; set; }
    public string? TaoBoi { get; set; }

    public ICollection<ChiTietGoiDichVu> ChiTietGois { get; set; } = new List<ChiTietGoiDichVu>();
}

/// <summary>Một dịch vụ (kèm số lượng) nằm trong gói.</summary>
public class ChiTietGoiDichVu
{
    public string MaGoi { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public int SoLuong { get; set; } = 1;

    public GoiDichVu Goi { get; set; } = null!;
    public DichVu DichVu { get; set; } = null!;
}
