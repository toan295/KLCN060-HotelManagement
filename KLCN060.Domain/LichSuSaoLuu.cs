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

    public TaiKhoan TaiKhoan { get; set; } = null!;
}
