using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Rooms;

public class RoomRequest
{
    // MaPhong = "P" + TenPhong, cột MaPhong varchar(10) → TenPhong tối đa 9 ký tự (dù cột TenPhong là 10).
    [StringLength(9, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string MaLoai { get; set; } = null!;
    public string? TinhTrang { get; set; }

    /// <summary>Lý do đổi tình trạng - BẮT BUỘC khi chuyển phòng sang OOO (bảo trì / khóa phòng).</summary>
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }
}

public class RoomStatusRequest
{
    public string TinhTrang { get; set; } = null!;

    /// <summary>Lý do / ghi chú; bắt buộc khi chuyển sang OOO (bảo trì, khóa phòng).</summary>
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }

    /// <summary>
    /// Tình trạng phòng mà người thao tác đang THẤY trên màn hình (tuỳ chọn). Nếu phòng đã bị người khác đổi sang tình trạng khác trong lúc đó
    /// thì từ chối (409 XUNG_DOT_TINH_TRANG) thay vì ghi đè lên thay đổi của họ.
    /// </summary>
    [StringLength(5, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? TinhTrangCu { get; set; }
}

public class RoomStatusHistoryDto
{
    public long MaLichSu { get; set; }
    public string MaPhong { get; set; } = null!;
    public string TuTinhTrang { get; set; } = null!;
    public string DenTinhTrang { get; set; } = null!;
    public DateTime ThoiGian { get; set; }
    public string? MaTaiKhoan { get; set; }
    public string? HoTenNguoiThucHien { get; set; }
    public string? LyDo { get; set; }
}

public class RoomDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TinhTrang { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;

    /// <summary>Thời điểm phòng chuyển sang tình trạng hiện tại và lý do (theo lịch sử tình trạng phòng; null nếu chưa có ghi nhận).</summary>
    public DateTime? TinhTrangTuLuc { get; set; }
    public string? LyDoTinhTrang { get; set; }

    /// <summary>Khách đang lưu trú tại phòng (null nếu không có).</summary>
    public string? KhachHienTai { get; set; }

    /// <summary>Khách có phiếu đặt đến phòng này hôm nay (hoặc quá ngày đón mà chưa nhận) hoặc ngày mai.</summary>
    public string? KhachSapDen { get; set; }
    public DateOnly? NgayDonSapDen { get; set; }
    public string? MaPhieuDatSapDen { get; set; }

    /// <summary>Số sự cố chưa xử lý xong của phòng.</summary>
    public int SuCoDangMo { get; set; }

    /// <summary>Phòng cần được chú ý và vì sao (null = bình thường). MucCanhBao: NGUY_HIEM (ảnh hưởng khách sắp đến / bán phòng) hoặc CANH_BAO.</summary>
    public string? CanhBao { get; set; }
    public string? MucCanhBao { get; set; }
}

public class RoomStayDto
{
    public string HoTen { get; set; } = null!;
    public string? SoDT { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTraDuKien { get; set; }
    public int SoNguoi { get; set; }
}

public class RoomReservationDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string? SoDT { get; set; }
    public DateOnly NgayDon { get; set; }
    public DateOnly NgayTra { get; set; }
    public string TrangThai { get; set; } = null!;
    public decimal TienCoc { get; set; }
}

public class RoomIncidentBriefDto
{
    public int MaSuCo { get; set; }
    public string TieuDe { get; set; } = null!;
    public string MucDo { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
}

/// <summary>Mọi thứ cần biết về một phòng để quyết định ngay trên sơ đồ: tình trạng + vì sao + ai đổi, khách đang ở, đặt phòng sắp tới, sự cố, việc tiếp theo.</summary>
public class RoomDetailDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TenLoaiPhong { get; set; } = null!;
    public string TinhTrang { get; set; } = null!;
    public DateTime? TinhTrangTuLuc { get; set; }
    public string? LyDoTinhTrang { get; set; }
    public string? NguoiDoi { get; set; }
    public string? HoTenNguoiDoi { get; set; }
    public string? TinhTrangTruoc { get; set; }
    public RoomStayDto? KhachDangO { get; set; }
    public List<RoomReservationDto> DatPhongSapToi { get; set; } = new();
    public List<RoomIncidentBriefDto> SuCo { get; set; } = new();
    public string? NguoiDangDon { get; set; }
    public string? CanhBao { get; set; }
    public string? MucCanhBao { get; set; }

    /// <summary>Gợi ý việc tiếp theo cho phòng này (bằng tiếng Việt).</summary>
    public string ViecTiepTheo { get; set; } = "";
}
