using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Khóa chính kép (MaPhieuNhan, MaPhong) — "BookingRoom" sau khi khách thực sự có mặt.</summary>
public class ChiTietPhieuNhan
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }

    /// <summary>Ngày trả dự kiến của lượt ở (theo phiếu đặt hoặc khai lúc nhận phòng vãng lai; gia hạn thì đổi ngày này). Null = chưa biết.</summary>
    public DateOnly? NgayTraDuKien { get; set; }
    public int SoNguoi { get; set; }
    public decimal DonGia { get; set; }
    public decimal TienPhuThu { get; set; }

    /// <summary>Sức chứa tiêu chuẩn của loại phòng lúc nhận phòng (chốt để đổi loại phòng sau này không làm đổi hóa đơn đang ở). Null = lượt cũ, lấy theo loại phòng hiện hành.</summary>
    public int? SoNguoiTieuChuanChot { get; set; }
    /// <summary>Phụ thu mỗi người vượt tiêu chuẩn mỗi đêm lúc nhận phòng (chốt như trên).</summary>
    public decimal? PhuThuNguoiThemChot { get; set; }
    public TrangThaiChiTietPhieuNhan TrangThai { get; set; }

    public PhieuNhanPhong PhieuNhanPhong { get; set; } = null!;
    public Phong Phong { get; set; } = null!;
}
