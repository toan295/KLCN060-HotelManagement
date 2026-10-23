namespace KLCN060.Web.Models.Home;

/// <summary>Khớp với ServiceDto bên KLCN060.Api (GET /services).</summary>
public class ServiceViewModel
{
    public string MaDV { get; set; } = null!;
    public string TenDV { get; set; } = null!;
    public decimal GiaDV { get; set; }
    public string DonViTinh { get; set; } = null!;
    public List<ServiceDetailViewModel> ChiTiets { get; set; } = new();
}

/// <summary>Khớp với ServiceDetailDto bên KLCN060.Api: một mục thông tin chi tiết của dịch vụ.</summary>
public class ServiceDetailViewModel
{
    public string MaCTDV { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string NoiDung { get; set; } = null!;
    public int ThuTu { get; set; }
}
