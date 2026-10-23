namespace KLCN060.Domain;

/// <summary>Người ở cùng (ngoài khách đại diện đặt phòng) đã khai báo với một phiếu đặt phòng; lễ tân thêm/xóa trước và trong lúc lưu trú (khai báo lưu trú).</summary>
public class KhachDiCung
{
    public int MaKhachDiCung { get; set; }
    public string MaPhieuDat { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string? CCCD { get; set; }

    /// <summary>Phòng khách này ở (không bắt buộc; phải là phòng thuộc phiếu).</summary>
    public string? MaPhong { get; set; }
    public DateTime NgayThem { get; set; }
    public string? NguoiThem { get; set; }

    public PhieuDatPhong PhieuDatPhong { get; set; } = null!;
}
