namespace KLCN060.Api.DTOs.Audit;

public class AuditLogQuery
{
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }

    /// <summary>Tên đăng nhập của người thực hiện (khớp chính xác).</summary>
    public string? TaiKhoan { get; set; }

    /// <summary>Nhóm nghiệp vụ: DAT_PHONG, THANH_TOAN, PHONG, HOA_DON, GIA, PHAN_QUYEN, TAI_KHOAN, DANG_NHAP, HE_THONG, BUONG_PHONG, CHI_PHI, DOI_SOAT.</summary>
    public string? Nhom { get; set; }

    /// <summary>Tìm chữ trong hành động, đối tượng tác động hoặc chi tiết.</summary>
    public string? Q { get; set; }

    /// <summary>Lọc theo kết quả: THANH_CONG, THAT_BAI, TU_CHOI.</summary>
    public string? KetQua { get; set; }

    /// <summary>NGUOI_DUNG (thao tác của người dùng) hoặc HE_THONG (hệ thống tự thực hiện).</summary>
    public string? Nguon { get; set; }

    /// <summary>Mã đối tượng tác động (mã phiếu đặt, hóa đơn, phòng, khách...): khớp chính xác hoặc nằm trong đường dẫn.</summary>
    public string? DoiTuong { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditChangeDto
{
    public string Truong { get; set; } = null!;
    public string? Truoc { get; set; }
    public string? Sau { get; set; }
}

public class AuditFieldDto
{
    public string Truong { get; set; } = null!;
    public string GiaTri { get; set; } = null!;
}

public class AuditLogDto
{
    public long MaNhatKy { get; set; }
    public DateTime ThoiGian { get; set; }
    public string TaiKhoan { get; set; } = null!;
    public string? HoTenNguoiThucHien { get; set; }

    /// <summary>Mã nhân viên (hoặc mã khách) của người thực hiện - để hiển thị "Họ tên (Mã NV)".</summary>
    public string? MaNguoiThucHien { get; set; }
    public string? VaiTro { get; set; }
    public string HanhDong { get; set; } = null!;

    /// <summary>Mô tả tiếng Việt của hành động (nếu là hành động đã biết).</summary>
    public string MoTaHanhDong { get; set; } = null!;
    public string Nhom { get; set; } = null!;
    public string? DoiTuongTacDong { get; set; }
    public string? ChiTiet { get; set; }

    /// <summary>THANH_CONG, THAT_BAI hoặc TU_CHOI (không đủ quyền).</summary>
    public string KetQua { get; set; } = "THANH_CONG";
    public string? MaLoi { get; set; }

    /// <summary>NGUOI_DUNG hoặc HE_THONG (dịch vụ nền / hệ thống tự động).</summary>
    public string NguonThucHien { get; set; } = "NGUOI_DUNG";

    /// <summary>Lý do do người thực hiện nhập (tách từ chi tiết).</summary>
    public string? LyDo { get; set; }

    /// <summary>Trước → Sau của từng trường thay đổi (tách từ chi tiết).</summary>
    public List<AuditChangeDto> ThayDoi { get; set; } = new();

    /// <summary>Các thông tin còn lại của thao tác (không phải thay đổi): ví dụ phòng, khách, số tiền.</summary>
    public List<AuditFieldDto> ThongTin { get; set; } = new();

    /// <summary>Mã các đối tượng khác nhắc tới trong dòng này (phiếu đặt, hóa đơn, phòng, khách, nhân viên...).</summary>
    public List<string> DoiTuongLienQuan { get; set; } = new();
}

public class AuditGroupDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
}

public class AuditRoomStatusDto
{
    public string MaPhong { get; set; } = null!;
    public string TuTinhTrang { get; set; } = null!;
    public string DenTinhTrang { get; set; } = null!;
    public DateTime ThoiGian { get; set; }
    public string? NguoiThucHien { get; set; }
    public string? LyDo { get; set; }
}

/// <summary>Mọi thứ liên quan tới một dòng nhật ký để điều tra ngay trên màn hình: chuỗi sự kiện của cùng đối tượng, lịch sử tình trạng phòng.</summary>
public class AuditRelatedDto
{
    public long MaNhatKy { get; set; }
    public string? DoiTuong { get; set; }

    /// <summary>Các dòng nhật ký của cùng đối tượng, theo thứ tự thời gian (cũ → mới); dòng đang xem được đánh dấu.</summary>
    public List<AuditLogDto> ChuoiSuKien { get; set; } = new();
    public List<AuditRoomStatusDto> LichSuTinhTrangPhong { get; set; } = new();
    public List<string> DoiTuongLienQuan { get; set; } = new();
}

/// <summary>Kết quả kiểm tra toàn vẹn nhật ký: dòng có chữ ký đúng / sai / chưa ký, và các khoảng trống trong dãy mã (dòng bị xóa).</summary>
public class AuditIntegrityDto
{
    public DateTime KiemTraLuc { get; set; }
    public int TongSoDong { get; set; }
    public int ChuKyHopLe { get; set; }
    public int ChuKySai { get; set; }
    public int ChuaCoChuKy { get; set; }
    public int SoKhoangTrongMa { get; set; }
    public bool ToanVen { get; set; }
    public string KetLuan { get; set; } = null!;
    public List<long> MaDongSai { get; set; } = new();
}
