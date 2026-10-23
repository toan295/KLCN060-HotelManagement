using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>
/// Việc dọn phòng: buồng phòng "nhận dọn" một phòng đang bẩn (VD/OD) và khi phòng được xác nhận sạch thì việc này xong.
/// Phòng bẩn tự nó là việc cần làm (suy ra từ tình trạng phòng); bảng này chỉ ghi ai đang dọn và ai đã dọn, bao lâu.
/// </summary>
public class PhieuDonPhong
{
    public int MaPhieuDon { get; set; }
    public string MaPhong { get; set; } = null!;

    /// <summary>Tên đăng nhập người dọn.</summary>
    public string NguoiDon { get; set; } = null!;
    public TinhTrangPhong TinhTrangBanDau { get; set; }

    /// <summary>
    /// Giờ bắt đầu dọn. Null + phiếu đang mở (DANG_DON) = Quản lý đã GIAO việc nhưng người dọn chưa bắt đầu; null + phiếu DA_XONG = phòng được xác nhận sạch mà không qua bước "nhận dọn".
    /// </summary>
    public DateTime? ThoiGianBatDau { get; set; }

    /// <summary>Tên đăng nhập người giao việc (Quản lý); null = nhân viên tự nhận dọn.</summary>
    public string? NguoiPhanCong { get; set; }
    public DateTime? ThoiGianPhanCong { get; set; }
    public DateTime? ThoiGianXong { get; set; }
    public TrangThaiPhieuDon TrangThai { get; set; }

    public Phong Phong { get; set; } = null!;
}
