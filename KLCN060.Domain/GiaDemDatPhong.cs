namespace KLCN060.Domain;

/// <summary>
/// Giá CHỐT của từng đêm trong một phòng của phiếu đặt (snapshot lúc đặt / sửa ngày / nhận sớm): giá cơ bản của loại phòng, quy tắc giá nào được áp dụng và giá cuối.
/// Tổng các đêm = <see cref="ChiTietPhieuDat.ThanhTien"/> (chính xác từng đồng, không dùng đơn giá bình quân). Đổi/bật/tắt/xóa quy tắc giá sau đó KHÔNG làm đổi dòng này;
/// giá chỉ được tính lại khi phiếu đổi ngày ở, đổi phòng hoặc nhận phòng sớm hơn dự kiến. Quy tắc lưu bằng mã + tên (không khóa ngoại) để xóa quy tắc không mất dấu vết.
/// </summary>
public class GiaDemDatPhong
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public DateOnly Dem { get; set; }

    public decimal GiaCoBan { get; set; }
    public decimal Gia { get; set; }

    public int? MaQuyTac { get; set; }
    public string? TenQuyTac { get; set; }
    /// <summary>Mô tả cách điều chỉnh đã áp dụng, VD "+20%" hoặc "giá cố định 900.000đ".</summary>
    public string? DieuChinh { get; set; }

    public ChiTietPhieuDat ChiTietPhieuDat { get; set; } = null!;
}
