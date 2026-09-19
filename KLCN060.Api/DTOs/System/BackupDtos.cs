namespace KLCN060.Api.DTOs.System;

public class BackupHistoryDto
{
    public int MaLichSu { get; set; }
    public string LoaiThaoTac { get; set; } = null!;
    public DateTime ThoiGianThucHien { get; set; }
    public string MaTaiKhoan { get; set; } = null!;
    public string? DuongDanFile { get; set; }
    public string KetQua { get; set; } = null!;
    public string? GhiChu { get; set; }
}
