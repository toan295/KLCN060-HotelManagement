using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Reports;

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
