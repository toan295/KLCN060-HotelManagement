using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>Một lần kiểm đồ dùng / minibar của một phòng.</summary>
public class PhieuKiemPhong
{
    public int MaPhieuKiem { get; set; }
    public string MaPhong { get; set; } = null!;

    /// <summary>Lượt lưu trú đang ở tại thời điểm kiểm (null = phòng không có khách).</summary>
    public string? MaPhieuNhan { get; set; }
    public string NguoiKiem { get; set; } = null!;
    public DateTime ThoiGian { get; set; }
    public string? GhiChu { get; set; }

    public Phong Phong { get; set; } = null!;
    public ICollection<ChiTietKiemPhong> ChiTiets { get; set; } = new List<ChiTietKiemPhong>();
}

public class ChiTietKiemPhong
{
    public int MaChiTiet { get; set; }
    public int MaPhieuKiem { get; set; }
    public string MaVatDung { get; set; } = null!;

    /// <summary>Số lượng ghi nhận trước khi kiểm (mặc định: định mức, hoặc số còn lại + bổ sung của lần kiểm trước trong cùng lượt ở).</summary>
    public int SoLuongTruoc { get; set; }

    /// <summary>Số lượng còn lại khi kiểm.</summary>
    public int SoLuongSau { get; set; }

    /// <summary>Số lượng đã bổ sung lại sau khi kiểm (để lần kiểm sau bắt đầu từ đúng số này).</summary>
    public int SoLuongBoSung { get; set; }
    public LyDoHaoHut? LyDo { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>Số lượng bị tính phí (chỉ khi vật dụng gắn dịch vụ và lý do là tiêu thụ).</summary>
    public int SoLuongTinhPhi { get; set; }
    public decimal ThanhTien { get; set; }
    public TrangThaiTinhPhi TrangThaiTinhPhi { get; set; }

    /// <summary>Dòng sử dụng dịch vụ đã tạo khi lễ tân tính phí.</summary>
    public int? MaChiTietDV { get; set; }
    public string? NguoiXuLy { get; set; }
    public DateTime? ThoiGianXuLy { get; set; }
    public string? LyDoBoQua { get; set; }

    public PhieuKiemPhong PhieuKiemPhong { get; set; } = null!;
    public VatDung VatDung { get; set; } = null!;
}
