using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Invoices;

public class PaymentRequest
{
    public string HinhThucThanhToan { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTien { get; set; }

    public string? MaGiaoDich { get; set; }
}

public class InvoiceLineDto
{
    public string LoaiKhoanMuc { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public int SoLuong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThanhTien { get; set; }
}

public class PaymentDto
{
    public int MaThanhToan { get; set; }
    public string HinhThucThanhToan { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    public DateTime ThoiGianThanhToan { get; set; }
    public string MaTaiKhoanThuNgan { get; set; } = null!;
    public string? MaGiaoDich { get; set; }
}

public class InvoiceDto
{
    public string MaHD { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string MaNV { get; set; } = null!;
    public DateTime NgayLap { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDV { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDaCoc { get; set; }

    /// <summary>Số tiền khách phải thanh toán tại quầy (đã trừ khuyến mãi và tiền cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public string HinhThucThanhToan { get; set; } = null!;
    public string TrangThaiThanhToan { get; set; } = null!;
    public List<InvoiceLineDto> ChiTietHoaDon { get; set; } = new();
    public List<PaymentDto> ChiTietThanhToan { get; set; } = new();
}
