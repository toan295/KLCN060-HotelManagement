namespace KLCN060.Web.Models.Home;

/// <summary>Khớp với ServiceDto bên KLCN060.Api (GET /services).</summary>
public class ServiceViewModel
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public decimal GiaDV { get; set; }
    public string DonViTinh { get; set; } = null!;
}
