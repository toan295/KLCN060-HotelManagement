namespace KLCN060.Api.DTOs.Audit;

/// <summary>Bộ lọc xem nhật ký hệ thống (mọi trường đều tùy chọn; không truyền gì = trang đầu của toàn bộ nhật ký, mới nhất trước).</summary>
public class AuditLogQuery
{
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }

    /// <summary>Tên đăng nhập của người thực hiện (khớp chính xác).</summary>
    public string? TaiKhoan { get; set; }

    /// <summary>Tìm chữ trong hành động (mã và mô tả), đối tượng tác động hoặc chi tiết.</summary>
    public string? Q { get; set; }

    /// <summary>Lọc theo kết quả: THANH_CONG, THAT_BAI, TU_CHOI.</summary>
    public string? KetQua { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogDto
{
    public long MaNhatKy { get; set; }
    public DateTime ThoiGian { get; set; }
    public string TaiKhoan { get; set; } = null!;

    /// <summary>"Họ tên (Mã NV)" của người thực hiện; trống nếu tài khoản không gắn với nhân viên / khách.</summary>
    public string? NguoiThucHien { get; set; }
    public string HanhDong { get; set; } = null!;
    /// <summary>Mô tả tiếng Việt của hành động (không biết mã thì trả lại chính mã).</summary>
    public string MoTaHanhDong { get; set; } = null!;
    public string? DoiTuongTacDong { get; set; }
    public string? ChiTiet { get; set; }
    public string KetQua { get; set; } = null!;
    public string? MaLoi { get; set; }
    public string? DiaChiIP { get; set; }
}
