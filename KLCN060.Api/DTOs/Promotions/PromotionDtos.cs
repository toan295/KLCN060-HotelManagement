using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Promotions;

public class PromotionRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột KhuyenMai.TenKM
    public string TenKM { get; set; } = null!;
    public decimal? PhanTramKM { get; set; }
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột KhuyenMai.DieuKien
    public string? DieuKien { get; set; }
    public string LoaiKM { get; set; } = null!;
    public decimal? GiaTri { get; set; }
    public int? SoLuongGioiHan { get; set; }
    /// <summary>Phạm vi loại phòng (null = mọi loại phòng).</summary>
    [StringLength(10)] public string? MaLoaiApDung { get; set; }
    public int? SoDemToiThieu { get; set; }
    public decimal? TongTienToiThieu { get; set; }
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

    public bool TamNgung { get; set; }
    public string? LyDoTamNgung { get; set; }
    public string? MaLoaiApDung { get; set; }
    public string? TenLoaiApDung { get; set; }
    public int? SoDemToiThieu { get; set; }
    public decimal? TongTienToiThieu { get; set; }

    /// <summary>CHUA_BAT_DAU, DANG_AP_DUNG, TAM_NGUNG, DA_KET_THUC, HET_LUOT.</summary>
    public string TrangThai { get; set; } = "DANG_AP_DUNG";
    public int SoPhieuDaApDung { get; set; }
    /// <summary>Tổng tiền đã giảm trên các đặt phòng còn hiệu lực (không tính hủy / không đến).</summary>
    public decimal TongTienDaGiam { get; set; }
    public bool DaSuDung { get; set; }
    public bool CoTheXoa { get; set; }
    public string? LyDoKhoa { get; set; }
}

public class PromotionUsageDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDon { get; set; }
    public DateOnly NgayTra { get; set; }
    public string TrangThaiPhieu { get; set; } = null!;
    public decimal TongKhuyenMai { get; set; }
}

public class PromotionCheckItemDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public bool Dat { get; set; }
    public string ChiTiet { get; set; } = null!;
}

public class PromotionCheckDto
{
    public string MaKM { get; set; } = null!;
    public string TenKM { get; set; } = null!;
    public string MaPhieuDat { get; set; } = null!;
    public bool DaApDung { get; set; }
    public bool DuDieuKien { get; set; }
    public decimal TienGiamDuKien { get; set; }
    public decimal TienGiamThucTe { get; set; }
    public string KetLuan { get; set; } = null!;
    public List<PromotionCheckItemDto> DieuKien { get; set; } = new();
}

public class PausePromotionRequest
{
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}
