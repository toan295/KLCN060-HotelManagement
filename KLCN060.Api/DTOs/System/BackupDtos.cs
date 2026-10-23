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

/// <summary>Cài đặt sao lưu tự động (bật / tắt và chu kỳ) - Quản lý đặt ngay trên giao diện.</summary>
public class BackupScheduleDto
{
    public bool TuDongBat { get; set; }
    public int ChuKyGio { get; set; } = 24;

    /// <summary>Mỗi bao lâu dịch vụ nền kiểm tra hạn một lần (phút): sau khi bật, lần sao lưu đầu có thể chạy trong khoảng thời gian này.</summary>
    public int KiemTraMoiPhut { get; set; }
    public string? ThuMuc { get; set; }
}

public class BackupScheduleRequest
{
    public bool TuDongBat { get; set; }

    [global::System.ComponentModel.DataAnnotations.Range(1, 720, ErrorMessage = "Chu kỳ sao lưu từ 1 đến 720 giờ.")]
    public int ChuKyGio { get; set; } = 24;
}

public class BackupSettingsDto
{
    public string ThuMucMacDinh { get; set; } = null!;
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

/// <summary>Xem trước phục hồi: kiểm tra file + cho biết dữ liệu sẽ mất những gì, TRƯỚC khi người dùng xác nhận (không thay đổi gì).</summary>
public class RestorePreviewDto
{
    public string TenCSDL { get; set; } = null!;
    public string DuongDanFile { get; set; } = null!;
    public bool SanSang { get; set; }
    public string KetLuan { get; set; } = null!;
    public BackupVerifyResultDto KiemTra { get; set; } = null!;
    public DateTime? ThoiDiemBanSaoLuu { get; set; }
    /// <summary>Số thao tác nghiệp vụ (nhật ký hoạt động) phát sinh SAU thời điểm bản sao lưu - sẽ mất nếu phục hồi.</summary>
    public int SoThaoTacSeMat { get; set; }
    public int SoDatPhongMoiSeMat { get; set; }
    public int SoHoaDonMoiSeMat { get; set; }
    public List<string> CanhBao { get; set; } = [];
}

/// <summary>Đánh giá khả năng phục hồi: nếu CSDL hỏng ngay bây giờ thì dùng bản nào, mất dữ liệu đến thời điểm nào, bản đó đáng tin cậy không.</summary>
public class RecoveryAssessmentDto
{
    /// <summary>SAN_SANG (có bản đã kiểm tra, còn trẻ), RUI_RO (có bản nhưng cũ / chưa kiểm tra), KHONG_CO.</summary>
    public string MucDo { get; set; } = null!;
    public string KetLuan { get; set; } = null!;
    public int? MaBanDeXuat { get; set; }
    public DateTime? ThoiDiemBanDeXuat { get; set; }
    public string? DuongDanBanDeXuat { get; set; }
    public bool? BanDeXuatDaKiemTra { get; set; }
    public bool BanDeXuatConFile { get; set; }
    /// <summary>Nếu phục hồi bản đề xuất, dữ liệu sau thời điểm này sẽ mất.</summary>
    public int? SoGioCoTheMatDuLieu { get; set; }
    public int SoThaoTacCoTheMat { get; set; }
    public List<string> CoCheBaoVe { get; set; } = [];
    public List<string> CanhBao { get; set; } = [];
}

public class BackupStatusDto
{
    /// <summary>TOT, CANH_BAO (đã lâu chưa sao lưu / lần gần nhất thất bại) hoặc CHUA_SAO_LUU.</summary>
    public string TinhTrang { get; set; } = null!;
    public string TomTat { get; set; } = null!;
    public DateTime? LanSaoLuuThanhCongCuoi { get; set; }
    public int? SoNgayTuLanSaoLuuCuoi { get; set; }
    public string? DuongDanBanSaoLuuCuoi { get; set; }
    public int TongSoBanSaoLuuThanhCong { get; set; }
    public DateTime? LanPhucHoiCuoi { get; set; }
    public DateTime? LanKiemTraCuoi { get; set; }

    /// <summary>Kết quả lần kiểm tra gần nhất (null nếu chưa kiểm tra lần nào).</summary>
    public bool? LanKiemTraCuoiHopLe { get; set; }
    public bool LanSaoLuuCuoiThatBai { get; set; }

    /// <summary>Sao lưu định kỳ tự động (cấu hình "Backup:Schedule"): đang bật không, chu kỳ (giờ) và hạn dự kiến của lần tới.</summary>
    public bool TuDongBat { get; set; }
    public int? ChuKyTuDongGio { get; set; }
    public DateTime? HanTuDongKeTiep { get; set; }

    public string TenCSDL { get; set; } = null!;
    /// <summary>Cập nhật lúc nào (để màn hình biết dữ liệu có cũ không).</summary>
    public DateTime CapNhatLuc { get; set; }
    public DateTime? LanSaoLuuCuoi { get; set; }
    public string? KetQuaLanSaoLuuCuoi { get; set; }
    public string? LoiLanSaoLuuCuoi { get; set; }
    /// <summary>Bản thành công gần nhất: đã kiểm tra toàn vẹn chưa / kích thước / còn file không.</summary>
    public bool? BanCuoiDaKiemTra { get; set; }
    public long? BanCuoiKichThuocBytes { get; set; }
    public bool BanCuoiConFile { get; set; }
    public DateTime? LanTuDongThanhCongCuoi { get; set; }
    public DateTime? LanTuDongThatBaiCuoi { get; set; }
    /// <summary>Sao lưu tự động đã quá hạn: dịch vụ nền có thể không chạy.</summary>
    public bool TuDongQuaHan { get; set; }
    public string? ThuMucSaoLuu { get; set; }
    public long? DungLuongTrongBytes { get; set; }
    public long? TongDungLuongCacBanBytes { get; set; }
    public long? KichThuocCSDLBytes { get; set; }
    /// <summary>OK, THAP (dưới ~3 lần bản sao lưu gần nhất hoặc dưới 1 GB), KHONG_RO.</summary>
    public string MucDungLuong { get; set; } = "KHONG_RO";
    public string? GhiChuDungLuong { get; set; }
    public RecoveryAssessmentDto KhaNangPhucHoi { get; set; } = null!;
    public List<string> CanhBao { get; set; } = [];
}
