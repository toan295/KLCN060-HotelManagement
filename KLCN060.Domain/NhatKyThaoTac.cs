namespace KLCN060.Domain;

/// <summary>Audit trail.</summary>
public class NhatKyThaoTac
{
    public long MaNhatKy { get; set; }
    public string MaTaiKhoan { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? DoiTuongTacDong { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? DiaChiIP { get; set; }
    public string? ChiTiet { get; set; }

    public TaiKhoan TaiKhoan { get; set; } = null!;
}
