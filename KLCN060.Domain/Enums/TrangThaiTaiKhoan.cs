namespace KLCN060.Domain.Enums;

public enum TrangThaiTaiKhoan
{
    HOAT_DONG,
    BI_KHOA,

    /// <summary>Nhân viên đã bị xóa (chỉ xóa được khi tài khoản đang khóa): không đăng nhập được và ẩn khỏi danh sách; hồ sơ + lịch sử thao tác vẫn được giữ để truy vết.</summary>
    DA_XOA
}
