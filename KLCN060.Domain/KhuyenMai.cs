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

    /// <summary>true = tạm ngưng: không áp dụng cho đặt phòng mới (đặt phòng đã áp dụng giữ nguyên). Dùng thay cho xóa khi chương trình đã có lịch sử.</summary>
    public bool TamNgung { get; set; }
    public string? LyDoTamNgung { get; set; }
    /// <summary>Phạm vi: chỉ tính trên tiền phòng thuộc loại này (null = mọi loại phòng).</summary>
    public string? MaLoaiApDung { get; set; }
    /// <summary>Điều kiện: số đêm tối thiểu của đặt phòng (null = không yêu cầu).</summary>
    public int? SoDemToiThieu { get; set; }
    /// <summary>Điều kiện: tổng tiền phòng (thuộc phạm vi) tối thiểu (null = không yêu cầu).</summary>
    public decimal? TongTienToiThieu { get; set; }

    public ICollection<ChiTietKhuyenMai> ChiTietKhuyenMais { get; set; } = new List<ChiTietKhuyenMai>();
}
