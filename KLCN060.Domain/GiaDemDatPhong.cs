namespace KLCN060.Domain;

/// <summary>
/// Giá CHỐT của từng đêm trong một phòng của phiếu đặt (snapshot lúc đặt / sửa ngày / nhận sớm): giá cơ bản của loại phòng và giá cuối.
/// Tổng các đêm = <see cref="ChiTietPhieuDat.ThanhTien"/> (chính xác từng đồng, không dùng đơn giá bình quân). Đổi đơn giá loại phòng sau đó KHÔNG làm đổi dòng này;
/// giá chỉ được tính lại khi phiếu đổi ngày ở, đổi phòng hoặc nhận phòng sớm hơn dự kiến.
/// </summary>
public class GiaDemDatPhong
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public DateOnly Dem { get; set; }

    public decimal GiaCoBan { get; set; }
    public decimal Gia { get; set; }


    public ChiTietPhieuDat ChiTietPhieuDat { get; set; } = null!;
}
