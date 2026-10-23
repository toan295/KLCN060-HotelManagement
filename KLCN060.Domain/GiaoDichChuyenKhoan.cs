using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>
/// Một yêu cầu thanh toán bằng chuyển khoản (giống "payment intent" của cổng thanh toán): hệ thống tạo mã + nội dung
/// chuyển khoản duy nhất, khách chuyển tiền theo đúng nội dung đó, ngân hàng báo "tiền về" thì hệ thống đối soát theo nội dung.
/// </summary>
public class GiaoDichChuyenKhoan
{
    public string MaGiaoDich { get; set; } = null!;
    public LoaiThamChieuThanhToan LoaiThamChieu { get; set; }
    public string MaThamChieu { get; set; } = null!;
    public decimal SoTien { get; set; }

    /// <summary>Nội dung khách phải nhập khi chuyển khoản; là "khóa đối soát" giữa ngân hàng và hệ thống.</summary>
    public string NoiDungChuyenKhoan { get; set; } = null!;

    public TrangThaiGiaoDichChuyenKhoan TrangThai { get; set; } = TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN;
    public DateTime NgayTao { get; set; }
    public DateTime HanThanhToan { get; set; }
    public DateTime? NgayThanhToan { get; set; }

    /// <summary>Mã giao dịch do ngân hàng cấp khi tiền về (chỉ có khi THANH_CONG).</summary>
    public string? MaGiaoDichNganHang { get; set; }

    /// <summary>Tài khoản tạo yêu cầu (khách hàng hoặc nhân viên).</summary>
    public string MaTaiKhoanTao { get; set; } = null!;

    /// <summary>Nhân viên sẽ đứng tên thu ngân khi ghi nhận thanh toán hóa đơn (chỉ với HOA_DON).</summary>
    public string? MaNVThuNgan { get; set; }

    public TaiKhoan TaiKhoanTao { get; set; } = null!;
    public NhanVien? NhanVienThuNgan { get; set; }
    public ICollection<ThongBaoNganHang> ThongBaoNganHangs { get; set; } = new List<ThongBaoNganHang>();
}
