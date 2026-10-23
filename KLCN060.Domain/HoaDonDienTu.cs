namespace KLCN060.Domain;

/// <summary>Trạng thái hóa đơn điện tử (HĐĐT) của một hóa đơn thanh toán.</summary>
public enum TrangThaiHoaDonDienTu
{
    /// <summary>Đã lưu thông tin người mua nhưng chưa gửi được lên nhà cung cấp HĐĐT / cơ quan thuế (chưa kết nối hoặc lỗi tạm thời).</summary>
    CHO_PHAT_HANH,
    /// <summary>Nhà cung cấp đã nhận, đang chờ cơ quan thuế cấp mã.</summary>
    DANG_XU_LY,
    /// <summary>Đã phát hành và được cơ quan thuế cấp mã - có giá trị pháp lý.</summary>
    DA_PHAT_HANH,
    /// <summary>Nhà cung cấp / cơ quan thuế từ chối (sai MST, sai dữ liệu...) - sửa thông tin rồi phát hành lại.</summary>
    LOI
}

/// <summary>
/// Hóa đơn điện tử theo Nghị định 123/2020/NĐ-CP, Thông tư 78/2021/TT-BTC, gắn 1-1 với một <see cref="HoaDon"/>.
/// Số hóa đơn, ký hiệu và mã của cơ quan thuế do nhà cung cấp HĐĐT cấp; hệ thống chỉ lưu lại để tra cứu - không tự đặt số.
/// </summary>
public class HoaDonDienTu
{
    public int MaHDDT { get; set; }
    public string MaHD { get; set; } = null!;
    public TrangThaiHoaDonDienTu TrangThai { get; set; } = TrangThaiHoaDonDienTu.CHO_PHAT_HANH;

    // Người mua
    public string TenNguoiMua { get; set; } = null!;
    public string? TenDonVi { get; set; }
    public string? MaSoThue { get; set; }
    public string? DiaChi { get; set; }
    public string? Email { get; set; }

    // Tiền (VND). Giá trên hóa đơn thanh toán đã bao gồm VAT: TienChuaThue = TongThanhToan / (1 + ThueSuat).
    public decimal ThueSuat { get; set; }
    public decimal TienChuaThue { get; set; }
    public decimal TienThue { get; set; }
    public decimal TongThanhToan { get; set; }

    // Do nhà cung cấp / cơ quan thuế cấp
    public string? NhaCungCap { get; set; }
    public string? MauSo { get; set; }
    public string? KyHieu { get; set; }
    public string? SoHoaDon { get; set; }
    public string? MaCQT { get; set; }
    public DateTime? NgayPhatHanh { get; set; }
    public string? MaTraCuu { get; set; }
    public string? LienKetTraCuu { get; set; }
    public string? MaThamChieuNhaCungCap { get; set; }

    public string? ThongBaoLoi { get; set; }
    public int SoLanThu { get; set; }
    public string NguoiYeuCau { get; set; } = null!;
    public DateTime NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    public HoaDon HoaDon { get; set; } = null!;
}
