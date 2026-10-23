using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Mỗi lần tình trạng phòng thay đổi (dù do lễ tân, buồng phòng, quản lý hay do nhận/trả/đổi phòng) được ghi một dòng.</summary>
public class LichSuTinhTrangPhong
{
    public long MaLichSu { get; set; }
    public string MaPhong { get; set; } = null!;
    public TinhTrangPhong TuTinhTrang { get; set; }
    public TinhTrangPhong DenTinhTrang { get; set; }
    public DateTime ThoiGian { get; set; }

    /// <summary>Tài khoản thực hiện (null khi không xác định được, VD tiến trình nền).</summary>
    public string? MaTaiKhoan { get; set; }
    public string? LyDo { get; set; }

    public Phong Phong { get; set; } = null!;
}
