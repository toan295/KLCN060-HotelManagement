using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.RoomTypes;

public class RoomTypeRequest
{
    [StringLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột LoaiPhong.TenLoai
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }

    /// <summary>Diện tích (m²), 5-1000; bỏ trống = chưa nhập.</summary>
    public decimal? DienTich { get; set; }
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] public string? LoaiGiuong { get; set; }
    /// <summary>Tiện nghi cách nhau bằng dấu phẩy.</summary>
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")] public string? TienNghi { get; set; }
    [StringLength(1000, ErrorMessage = "{0} tối đa {1} ký tự.")] public string? MoTa { get; set; }
}

public class RoomTypeDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }

    public decimal? DienTich { get; set; }
    public string? LoaiGiuong { get; set; }
    public string? TienNghi { get; set; }
    public string? MoTa { get; set; }
}

/// <summary>Loại phòng kèm số liệu vận hành cho màn quản lý danh mục: bao nhiêu phòng, phòng bảo trì, đặt phòng sắp tới, xóa được không.</summary>
public class RoomTypeCatalogDto : RoomTypeDto
{
    public int SoPhong { get; set; }
    public int SoPhongBaoTri { get; set; }
    public int SoPhongCoKhach { get; set; }
    public int SoDatPhongTuongLai { get; set; }
    public bool CoTheXoa { get; set; }
    public string? LyDoKhongXoa { get; set; }
    /// <summary>Số quy tắc giá đang gắn với loại phòng.</summary>
    public int SoQuyTacGia { get; set; }
}

public class RoomTypeAvailabilityDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }

    /// <summary>Đơn giá bình quân mỗi đêm áp dụng cho khoảng ngày đã chọn (đã tính quy tắc giá cuối tuần/cao điểm/lễ tết đang bật; bằng giá cơ bản nếu không có).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    /// <summary>Đơn giá cơ bản của loại phòng (chưa tính quy tắc giá).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaCoBan { get; set; }

    public int SoDem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal PhuThu { get; set; }

    public int SoPhongTrong { get; set; }
}
