using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>
/// Nhật ký MỌI thông báo "tiền về" ngân hàng gửi sang (kể cả thông báo không khớp giao dịch nào) - phục vụ đối soát
/// và chống xử lý trùng: MaGiaoDichNganHang là duy nhất, ngân hàng gửi lại cùng một thông báo thì không ghi nhận tiền 2 lần.
/// </summary>
public class ThongBaoNganHang
{
    public int MaThongBao { get; set; }
    public string MaGiaoDichNganHang { get; set; } = null!;
    public string SoTaiKhoanNhan { get; set; } = null!;
    public decimal SoTien { get; set; }
    public string NoiDung { get; set; } = null!;
    public DateTime ThoiGianNganHang { get; set; }
    public DateTime ThoiGianNhan { get; set; }
    public KetQuaDoiSoatNganHang KetQua { get; set; }
    public string? MaGiaoDich { get; set; }
    public string? GhiChu { get; set; }
    /// <summary>KHONG_CAN khi đã khớp; CAN_XU_LY khi bất thường (kế toán phải xử lý); DA_XU_LY sau khi kế toán xử lý.</summary>
    public TrangThaiXuLyDoiSoat TrangThaiXuLy { get; set; } = TrangThaiXuLyDoiSoat.KHONG_CAN;
    public string? NguoiXuLy { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public string? HuongXuLy { get; set; }
    public string? GhiChuXuLy { get; set; }

    public GiaoDichChuyenKhoan? GiaoDich { get; set; }
}
