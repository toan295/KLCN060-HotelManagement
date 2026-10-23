namespace KLCN060.Domain;

/// <summary>
/// Một "đợt" cảnh báo điều hành (VD BAO_TRI_QUA_LAU): từ lúc hệ thống thấy cảnh báo xuất hiện, ai nhận xử lý, hạn xử lý, cho tới lúc cảnh báo biến mất (đã xử lý xong).
/// Cảnh báo vẫn được tính trực tiếp từ dữ liệu; bảng này chỉ lưu vòng đời để trả lời "ai xử lý, hạn khi nào, đã xong chưa".
/// </summary>
public class CanhBaoTheoDoi
{
    public int MaTheoDoi { get; set; }
    public string Ma { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string MucDo { get; set; } = null!;
    public int SoLuongLucPhatHien { get; set; }
    public int SoLuongHienTai { get; set; }
    public DateTime PhatHienLuc { get; set; }

    /// <summary>null = đang còn; có giá trị = cảnh báo đã hết (nguyên nhân đã được xử lý).</summary>
    public DateTime? DaXuLyLuc { get; set; }

    /// <summary>Tên đăng nhập người nhận xử lý (null = chưa ai nhận).</summary>
    public string? NguoiNhan { get; set; }
    public string? NguoiGiao { get; set; }
    public DateTime? NhanLuc { get; set; }
    public DateTime? HanXuLy { get; set; }
    public string? GhiChuNhan { get; set; }
}
