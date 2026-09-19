using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Stays;

public class AddStayServiceRequest
{
    public string MaDV { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
}

public class StayServiceUsageDto
{
    public int MaChiTietDV { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int SoLuong { get; set; }
    public DateTime NgaySuDung { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
}
