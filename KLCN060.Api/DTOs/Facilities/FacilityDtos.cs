using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Facilities;

public class FacilityRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột CoSoVatChat.Ten
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
    [StringLength(30, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột CoSoVatChat.TinhTrang
    public string TinhTrang { get; set; } = null!;

    /// <summary>Lý do đổi tình trạng (bắt buộc khi chuyển sang NGUNG_SU_DUNG / DA_THANH_LY).</summary>
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }

    /// <summary>Chỉ dùng cho <c>POST /facilities</c>: phòng chứa thiết bị; bỏ trống = thiết bị trong kho / dùng chung.</summary>
    [StringLength(10, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? MaPhong { get; set; }
}

public class FacilityTransferRequest
{
    /// <summary>Phòng đích; bỏ trống = chuyển về kho / dùng chung.</summary>
    [StringLength(10, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? MaPhongMoi { get; set; }

    /// <summary>Số lượng chuyển; bỏ trống = chuyển toàn bộ dòng. Chuyển một phần sẽ tách dòng.</summary>
    public int? SoLuong { get; set; }

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string LyDo { get; set; } = null!;
}

public class FacilityDto
{
    public string MaSo { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; }
    public string TinhTrang { get; set; } = null!;
    public string? MaPhong { get; set; }
}

/// <summary>Một thiết bị trong màn Quản lý cơ sở vật chất: kèm vị trí (phòng/tầng), tình trạng (từ khi nào, vì sao) và người cập nhật gần nhất.</summary>
public class FacilityManageItemDto
{
    public string MaSo { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; }
    public string TinhTrang { get; set; } = null!;
    public string? MaPhong { get; set; }
    public string? TenPhong { get; set; }
    public int? Tang { get; set; }
    public string? GhiChu { get; set; }
    public DateTime? TinhTrangTuLuc { get; set; }
    public string? LyDoTinhTrang { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? CapNhatLuc { get; set; }
    public string? NguoiCapNhat { get; set; }
    /// <summary>Số ngày thiết bị ở tình trạng hỏng (chỉ khi hỏng và biết thời điểm bắt đầu).</summary>
    public int? SoNgayHong { get; set; }
    public bool CanXuLy { get; set; }
    public string? GoiY { get; set; }
    public bool CoTheXoa { get; set; }
    public string? LyDoKhongXoa { get; set; }
}

public class FacilityStatusCountDto
{
    public string TinhTrang { get; set; } = null!;
    public int SoDong { get; set; }
    public int SoLuong { get; set; }
}

public class FacilityRoomIssueDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TinhTrangPhong { get; set; } = null!;
    public int SoThietBiHong { get; set; }
    public int SuCoDangMo { get; set; }
    public string ThietBi { get; set; } = "";
}

public class FacilitySummaryDto
{
    public int TongDong { get; set; }
    public int TongSoLuong { get; set; }
    public int SoLuongDangDung { get; set; }
    public int SoLuongHong { get; set; }
    public int SoLuongTrongKho { get; set; }
    public int SoPhongCoThietBiHong { get; set; }
    public int SoPhongChuaKhaiBao { get; set; }
    public int HongQuaHan { get; set; }
    public int HongQuaHanNgay { get; set; }
    public List<FacilityStatusCountDto> TheoTinhTrang { get; set; } = new();
}

public class FacilityOverviewDto
{
    public DateTime CapNhatLuc { get; set; }
    public FacilitySummaryDto TongHop { get; set; } = new();
    public List<FacilityRoomIssueDto> PhongCanChuY { get; set; } = new();
    public List<string> PhongChuaKhaiBao { get; set; } = new();
    public List<FacilityManageItemDto> ThietBis { get; set; } = new();
}

public class FacilityHistoryEntryDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string MoTaHanhDong { get; set; } = "";
    public string? ChiTiet { get; set; }
}
