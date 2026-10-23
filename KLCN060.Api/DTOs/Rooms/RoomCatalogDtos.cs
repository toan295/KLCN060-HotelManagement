using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Rooms;

/// <summary>Một phòng trong danh mục: thông tin cố định + tình trạng hiện tại + mức độ đã phát sinh dữ liệu (quyết định xóa được không).</summary>
public class RoomCatalogRowDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SucChua { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThu { get; set; }
    public string TinhTrang { get; set; } = null!;
    public DateTime? TinhTrangTuLuc { get; set; }
    public string? LyDoTinhTrang { get; set; }
    public int SoDatPhongTuongLai { get; set; }
    public int SoLuotLuuTru { get; set; }
    public int SoCoSoVatChat { get; set; }
    public bool CoTheXoa { get; set; }
    public string? LyDoKhongXoa { get; set; }
    /// <summary>Có thể đổi loại phòng không (không nếu đang có khách hoặc có đặt phòng sắp tới).</summary>
    public bool CoTheDoiLoai { get; set; }
    public string? LyDoKhongDoiLoai { get; set; }
    /// <summary>Các tình trạng người dùng được chuyển sang từ tình trạng hiện tại (theo sơ đồ chuyển trạng thái).</summary>
    public List<string> TinhTrangChoPhep { get; set; } = new();
}

public class RoomCatalogCountDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoPhong { get; set; }
}

public class RoomCatalogDto
{
    public DateTime CapNhatLuc { get; set; }
    public int TongPhong { get; set; }
    public int SoTang { get; set; }
    public int SoLoaiPhong { get; set; }
    public int TongSucChua { get; set; }
    public int SoPhongBaoTri { get; set; }
    public int SoPhongCoKhach { get; set; }
    public int SoPhongKhaDung { get; set; }
    public List<RoomCatalogCountDto> TheoTang { get; set; } = new();
    public List<RoomCatalogCountDto> TheoLoai { get; set; } = new();
    public List<RoomCatalogCountDto> TheoTinhTrang { get; set; } = new();
    /// <summary>Tổng số phòng khớp bộ lọc (Phong chỉ chứa trang đang xem).</summary>
    public int TongKhopLoc { get; set; }
    public List<RoomCatalogRowDto> Phong { get; set; } = new();
}

public class RoomCatalogQuery
{
    public string? Q { get; set; }
    public int? Tang { get; set; }
    public string? MaLoai { get; set; }
    public string? TinhTrang { get; set; }
    /// <summary>ma (mặc định), ten, tang, loai, tinh_trang.</summary>
    public string? SapXep { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

/// <summary>Tạo nhiều phòng cùng lúc (VD cả tầng 5: 501..510). Tất cả cùng thành công hoặc không phòng nào được tạo.</summary>
public class BulkRoomRequest
{
    [Required] public List<string> TenPhongs { get; set; } = new();
    public int Tang { get; set; }
    [Required] public string MaLoai { get; set; } = null!;
    /// <summary>VC (mặc định), VD, VI hoặc OOO.</summary>
    public string? TinhTrang { get; set; }
}

public class BulkRoomResultDto
{
    public int SoPhongDaTao { get; set; }
    public List<string> MaPhongs { get; set; } = new();
}
