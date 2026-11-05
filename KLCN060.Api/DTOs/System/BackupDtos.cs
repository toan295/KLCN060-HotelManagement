namespace KLCN060.Api.DTOs.System;

public class BackupHistoryDto
{
    public int MaLichSu { get; set; }
    public string LoaiThaoTac { get; set; } = null!;
    public DateTime ThoiGianThucHien { get; set; }
    public string MaTaiKhoan { get; set; } = null!;

    /// <summary>Người thực hiện hiển thị "Họ tên (Mã NV)"; sao lưu tự động của hệ thống ghi rõ "Hệ thống (tự động)".</summary>
    public string? NguoiLam { get; set; }
    public string? DuongDanFile { get; set; }
    public string KetQua { get; set; } = null!;
    public string? GhiChu { get; set; }
    public string? TenCSDL { get; set; }
    public long? KichThuocBytes { get; set; }
    public int? ThoiLuongMs { get; set; }
    public string? NguonKichHoat { get; set; }
    public string? LyDo { get; set; }
    public int? MaBanNguon { get; set; }
    public bool? DaKiemTraHopLe { get; set; }
    public string? ThongBaoLoi { get; set; }
    /// <summary>File .bak còn tồn tại trên máy chạy API không (chỉ tính cho lần sao lưu thành công).</summary>
    public bool? FileTonTai { get; set; }
    /// <summary>Phục hồi: kết quả kiểm tra sau phục hồi (toàn vẹn CSDL, số bảng, migration).</summary>
    public string? KiemTraSauPhucHoi { get; set; }
}

public class BackupRequest
{
    /// <summary>Thư mục (đường dẫn tuyệt đối, trên máy chạy API) để lưu file .bak. Bỏ trống = dùng thư mục mặc định.</summary>
    public string? ThuMuc { get; set; }
}

public class RestoreFileRequest
{
    /// <summary>Đường dẫn tuyệt đối tới file .bak (trên máy chạy API) cần phục hồi.</summary>
    public string DuongDanFile { get; set; } = null!;

    /// <summary>Lý do phục hồi (bắt buộc khi phục hồi, ghi vào lịch sử).</summary>
    public string? LyDo { get; set; }
    /// <summary>Phải gõ lại đúng tên CSDL đang chạy để xác nhận ghi đè (chỉ dùng khi phục hồi).</summary>
    public string? XacNhanTenCSDL { get; set; }
    /// <summary>Chỉ khi không tạo được bản sao lưu an toàn: cho phép phục hồi tiếp (người dùng đã chấp nhận rủi ro).</summary>
    public bool BoQuaBanAnToan { get; set; }
}

public class RestoreRequest
{
    public string? LyDo { get; set; }
    public string? XacNhanTenCSDL { get; set; }
    public bool BoQuaBanAnToan { get; set; }
}

public class BackupSettingsDto
{
    public string ThuMucMacDinh { get; set; } = null!;
}

/// <summary>Kết quả xem trước phục hồi (không thay đổi gì): file có hợp lệ không, đúng CSDL không và dữ liệu nào sẽ mất.</summary>
public class RestorePreviewDto
{
    public string TenCSDL { get; set; } = null!;
    public string DuongDanFile { get; set; } = null!;
    public bool SanSang { get; set; }
    public string KetLuan { get; set; } = null!;
    public BackupVerifyResultDto KiemTra { get; set; } = null!;
    public DateTime? ThoiDiemBanSaoLuu { get; set; }
    /// <summary>Số thao tác nghiệp vụ phát sinh SAU thời điểm bản sao lưu - sẽ mất nếu phục hồi.</summary>
    public int SoThaoTacSeMat { get; set; }
    public int SoDatPhongMoiSeMat { get; set; }
    public int SoHoaDonMoiSeMat { get; set; }
    public List<string> CanhBao { get; set; } = [];
}

public class BackupVerifyResultDto
{
    /// <summary>true = file sao lưu đọc được và toàn vẹn (RESTORE VERIFYONLY ... WITH CHECKSUM thành công).</summary>
    public bool HopLe { get; set; }
    public string DuongDanFile { get; set; } = null!;
    public string ThongBao { get; set; } = null!;
    public DateTime KiemTraLuc { get; set; }
    /// <summary>Thông tin đọc từ chính file (RESTORE HEADERONLY): CSDL gốc, thời điểm sao lưu, dung lượng.</summary>
    public string? TenCSDLTrongFile { get; set; }
    public DateTime? ThoiDiemSaoLuuTrongFile { get; set; }
    public long? KichThuocBytes { get; set; }
    /// <summary>File thuộc đúng CSDL đang chạy (tên CSDL trong file = tên CSDL hiện tại).</summary>
    public bool? DungCSDL { get; set; }
}

