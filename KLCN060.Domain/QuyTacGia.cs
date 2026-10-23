using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>
/// Quy tắc điều chỉnh giá phòng theo đêm (giá cuối tuần, mùa cao điểm, lễ/tết, giá đoàn/doanh nghiệp...). Không có quy tắc nào khớp
/// thì đêm đó tính theo đơn giá cơ bản của loại phòng. Nhiều quy tắc cùng khớp: lấy quy tắc có <see cref="UuTien"/> cao nhất.
/// </summary>
public class QuyTacGia
{
    public int MaQuyTac { get; set; }
    public string TenQuyTac { get; set; } = null!;
    public LoaiQuyTacGia LoaiQuyTac { get; set; }

    /// <summary>Loại phòng áp dụng (null = mọi loại phòng).</summary>
    public string? MaLoai { get; set; }
    public KieuDieuChinhGia KieuDieuChinh { get; set; }
    public decimal GiaTri { get; set; }

    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }

    /// <summary>Các thứ trong tuần áp dụng, dạng "5,6" (0 = Chủ nhật ... 6 = Thứ bảy); null = mọi ngày. Tính theo đêm lưu trú (đêm bắt đầu vào ngày đó).</summary>
    public string? CacThuApDung { get; set; }

    /// <summary>Chỉ áp dụng cho loại đặt phòng này (null = mọi loại; DOAN dùng cho giá đoàn/doanh nghiệp).</summary>
    public LoaiDatPhong? LoaiDatPhongApDung { get; set; }
    public int UuTien { get; set; }
    public bool HoatDong { get; set; } = true;
    public string? GhiChu { get; set; }
    public DateTime NgayTao { get; set; }
    public string? TaoBoi { get; set; }

    public LoaiPhong? LoaiPhong { get; set; }
}
