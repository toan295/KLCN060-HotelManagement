using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Reports;

// ----- Bàn giao ca -----

public class ShiftHandoverRequest
{
    public string MaTaiKhoanNhan { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienMatCuoiCa { get; set; }

    /// <summary>Chỉ dùng cho lần bàn giao đầu tiên (chưa có lần trước để kế thừa); mặc định 0.</summary>
    [JsonConverter(typeof(NullableVndMoneyJsonConverter))]
    public decimal? TongTienMatDauCa { get; set; }

    public string? GhiChu { get; set; }
}

public class ShiftHandoverDto
{
    public int MaBanGiao { get; set; }
    public string MaTaiKhoanGiao { get; set; } = null!;
    public string MaTaiKhoanNhan { get; set; } = null!;
    public DateTime ThoiGianBanGiao { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienMatDauCa { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienMatCuoiCa { get; set; }
    public int SoLuongPhieuTrongCa { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>Đầu ca + tổng tiền mặt đã thu trong ca (chỉ có khi vừa tạo bản ghi, để đối chiếu).</summary>
    [JsonConverter(typeof(NullableVndMoneyJsonConverter))] public decimal? TienMatDuKien { get; set; }

    /// <summary>Cuối ca thực tế trừ tiền mặt dự kiến (âm = thiếu, dương = thừa).</summary>
    [JsonConverter(typeof(NullableVndMoneyJsonConverter))] public decimal? ChenhLech { get; set; }
}

// ----- Báo cáo -----

public class RevenueDayDto
{
    public DateOnly Ngay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }
}

public class RevenueReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongCong { get; set; }
    public List<RevenueDayDto> TheoNgay { get; set; } = new();
}

public class OccupancyDayDto
{
    public DateOnly Ngay { get; set; }
    public int SoPhongCoKhach { get; set; }
    public decimal TyLePhanTram { get; set; }
}

public class OccupancyReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int TongSoPhong { get; set; }
    public decimal TyLeTrungBinhPhanTram { get; set; }
    public List<OccupancyDayDto> TheoNgay { get; set; } = new();
}

public class GuestReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int KhachMoi { get; set; }
    public int KhachQuayLai { get; set; }
    public int TongKhach { get; set; }
}
