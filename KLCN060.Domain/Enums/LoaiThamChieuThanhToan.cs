namespace KLCN060.Domain.Enums;

/// <summary>Giao dịch chuyển khoản trả cho khoản nào.</summary>
public enum LoaiThamChieuThanhToan
{
    /// <summary>Tiền đặt cọc của phiếu đặt phòng (MaThamChieu = MaPhieuDat).</summary>
    DAT_COC,
    /// <summary>Thanh toán hóa đơn khi trả phòng (MaThamChieu = MaHD).</summary>
    HOA_DON
}
