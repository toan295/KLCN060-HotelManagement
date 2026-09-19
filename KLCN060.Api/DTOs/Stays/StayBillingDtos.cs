using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Stays;

public class ChangeRoomRequest
{
    public string MaPhongMoi { get; set; } = null!;
    public string? GhiChu { get; set; }
}

public class ChangeRoomResultDto
{
    public string MaPhieuDoi { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhongCu { get; set; } = null!;
    public string MaPhongMoi { get; set; } = null!;
    public DateTime NgayDoi { get; set; }

    /// <summary>Chênh lệch đơn giá/đêm (phòng mới trừ phòng cũ), có thể âm.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ChenhLechGia { get; set; }

    public string TinhTrangPhongCu { get; set; } = null!;
    public string TinhTrangPhongMoi { get; set; } = null!;
}

public class CheckOutRequest
{
    /// <summary>Lễ tân tick khi khách trả phòng trễ giờ và cần tính phụ thu 1 đêm.</summary>
    public bool ApDungPhuThuTreGio { get; set; }
}

public class FolioSummaryDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDichVu { get; set; }

    /// <summary>Tiền phòng + phụ thu + dịch vụ (chưa trừ khuyến mãi và tiền cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongCong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongKhuyenMai { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDaCoc { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConPhaiThu { get; set; }
}

public class FolioLineDto
{
    public string LoaiKhoanMuc { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public int SoLuong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThanhTien { get; set; }
}

public class FolioDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public FolioSummaryDto TongHopFolio { get; set; } = null!;
    public List<FolioLineDto> ChiTiet { get; set; } = new();
}

public class CheckOutResultDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string TinhTrangPhong { get; set; } = null!;

    /// <summary>Null khi phiếu nhận còn phòng khác đang ở — hóa đơn chỉ được lập khi trả phòng cuối cùng.</summary>
    public string? MaHoaDon { get; set; }

    public FolioSummaryDto TongHopFolio { get; set; } = null!;
}

public class StaySummaryDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string MaPhong { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoNguoi { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    public string TrangThai { get; set; } = null!;
}
