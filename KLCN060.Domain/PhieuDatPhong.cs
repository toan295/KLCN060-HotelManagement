using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

public class PhieuDatPhong
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public decimal TienCoc { get; set; }
    /// <summary>Thời điểm tiền cọc thực sự được nhận (lễ tân thu tiền mặt hoặc ngân hàng báo tiền về); null = chưa nhận cọc.</summary>
    public DateTime? NgayNhanCoc { get; set; }
    public HinhThucThanhToan? HinhThucCoc { get; set; }
    /// <summary>Tài khoản ghi nhận khoản cọc (nhân viên thu tiền mặt, hoặc tài khoản tạo mã QR).</summary>
    public string? NguoiNhanCoc { get; set; }
    public TrangThaiPhieuDat TrangThai { get; set; }
    public LoaiDatPhong LoaiDatPhong { get; set; }

    /// <summary>Kênh đặt phòng: khách tự đặt trên Website hay lễ tân đặt hộ tại quầy.</summary>
    public NguonDatPhong NguonDat { get; set; } = NguonDatPhong.QUAY_LE_TAN;

    /// <summary>Thời điểm phiếu bị hủy hoặc bị đánh dấu "không đến" (null nếu chưa).</summary>
    public DateTime? NgayHuy { get; set; }
    public string? LyDoHuy { get; set; }

    /// <summary>Tài khoản thực hiện việc hủy / đánh dấu không đến.</summary>
    public string? HuyBoi { get; set; }

    /// <summary>Số tiền cọc thực tế đã hoàn lại cho khách khi hủy (0 nếu không hoàn).</summary>
    public decimal SoTienHoanCoc { get; set; }

    /// <summary>Số tiền cọc khách đã đóng nhưng bị giữ lại khi hủy muộn / không đến (0 nếu không có cọc hoặc đã hoàn).</summary>
    public decimal TienCocGiuLai { get; set; }

    public Khach Khach { get; set; } = null!;
    public ICollection<ChiTietPhieuDat> ChiTietPhieuDats { get; set; } = new List<ChiTietPhieuDat>();
    public ICollection<ChiTietKhuyenMai> ChiTietKhuyenMais { get; set; } = new List<ChiTietKhuyenMai>();
    public PhieuNhanPhong? PhieuNhanPhong { get; set; }
    public ICollection<KhachDiCung> KhachDiCungs { get; set; } = new List<KhachDiCung>();
}
