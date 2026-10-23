using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class LichSuSaoLuu
{
    public int MaLichSu { get; set; }
    public LoaiThaoTacSaoLuu LoaiThaoTac { get; set; }
    public DateTime ThoiGianThucHien { get; set; }
    public string MaTaiKhoan { get; set; } = null!;
    public string? DuongDanFile { get; set; }
    public KetQuaSaoLuu KetQua { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>Tên CSDL được sao lưu / phục hồi (để biết thao tác diễn ra trên database nào).</summary>
    public string? TenCSDL { get; set; }
    /// <summary>Dung lượng file .bak (byte) sau khi sao lưu.</summary>
    public long? KichThuocBytes { get; set; }
    public int? ThoiLuongMs { get; set; }
    /// <summary>THU_CONG, TU_DONG, AN_TOAN (bản chụp tự động ngay trước khi phục hồi).</summary>
    public string? NguonKichHoat { get; set; }
    /// <summary>Lý do do người thực hiện nhập (bắt buộc với phục hồi).</summary>
    public string? LyDo { get; set; }
    /// <summary>Phục hồi / kiểm tra: mã lần sao lưu được dùng làm nguồn (nếu có trong lịch sử).</summary>
    public int? MaBanNguon { get; set; }
    /// <summary>Sao lưu: kết quả kiểm tra toàn vẹn (VERIFYONLY ... CHECKSUM) chạy ngay sau khi sao lưu; null = chưa kiểm tra.</summary>
    public bool? DaKiemTraHopLe { get; set; }
    /// <summary>Thông báo lỗi đầy đủ khi thất bại.</summary>
    public string? ThongBaoLoi { get; set; }

    public TaiKhoan TaiKhoan { get; set; } = null!;
}
