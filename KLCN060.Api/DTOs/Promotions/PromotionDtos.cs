namespace KLCN060.Api.DTOs.Promotions;

public class PromotionRequest
{
    public string TenKM { get; set; } = null!;
    public decimal? PhanTramKM { get; set; }
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public string? DieuKien { get; set; }
    public string LoaiKM { get; set; } = null!;
    public decimal? GiaTri { get; set; }
    public int? SoLuongGioiHan { get; set; }
}

public class PromotionDto
{
    public string MaKM { get; set; } = null!;
    public string TenKM { get; set; } = null!;
    public decimal? PhanTramKM { get; set; }
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public string? DieuKien { get; set; }
    public string LoaiKM { get; set; } = null!;
    public decimal? GiaTri { get; set; }
    public int? SoLuongGioiHan { get; set; }
    public int SoLuongDaSuDung { get; set; }
}
