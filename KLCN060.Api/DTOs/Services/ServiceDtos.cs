using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Services;

public class ServiceRequest
{
    public string TenDV { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaDV { get; set; }

    public string DonViTinh { get; set; } = null!;
}

public class ServiceDto
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaDV { get; set; }

    public string DonViTinh { get; set; } = null!;
}
