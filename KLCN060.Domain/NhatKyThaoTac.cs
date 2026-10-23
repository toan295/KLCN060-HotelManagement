namespace KLCN060.Domain;

/// <summary>Audit trail.</summary>
public class NhatKyThaoTac
{
    public long MaNhatKy { get; set; }
    public string MaTaiKhoan { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? DoiTuongTacDong { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? DiaChiIP { get; set; }
    public string? ChiTiet { get; set; }

    /// <summary>THANH_CONG, THAT_BAI (nghiệp vụ / hệ thống từ chối hoặc lỗi) hoặc TU_CHOI (không đủ quyền).</summary>
    public string KetQua { get; set; } = "THANH_CONG";
    /// <summary>Mã lỗi API khi KetQua khác THANH_CONG (VD KHONG_CO_QUYEN, PHONG_DA_CO_KHACH).</summary>
    public string? MaLoi { get; set; }
    /// <summary>Chữ ký HMAC-SHA256 của nội dung dòng nhật ký (khóa nằm ngoài CSDL): phát hiện dòng bị sửa trực tiếp trong CSDL.</summary>
    public string? ChuKy { get; set; }

    public TaiKhoan TaiKhoan { get; set; } = null!;
}
