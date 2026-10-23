using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Invoices;

/// <summary>Số liệu tổng hợp của TOÀN BỘ hóa đơn khớp bộ lọc (không chỉ trang đang xem) - đầu màn hình Hóa đơn.</summary>
public class InvoiceStatsDto
{
    public DateTime CapNhatLuc { get; set; }
    public int TongHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThu { get; set; }
    /// <summary>Tổng công nợ = tổng số còn nợ của các hóa đơn (hóa đơn thu thừa không bù trừ cho hóa đơn khác).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public int SoChuaThanhToan { get; set; }
    public int SoThanhToanMotPhan { get; set; }
    public int SoDaThanhToan { get; set; }
    public int SoQuaHan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNoQuaHan { get; set; }
    /// <summary>Số hóa đơn thu thừa (đã thu lớn hơn tổng tiền) - các hóa đơn duy nhất làm "Tổng tiền khác Đã thu + Còn nợ".</summary>
    public int SoThuThua { get; set; }
    public int NgayQuaHan { get; set; }
}

public class InvoiceRoomOverviewDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;
    public DateOnly NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoDem { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGia { get; set; }
    public string TrangThai { get; set; } = null!;
}

public class InvoicePaymentOverviewDto
{
    public int MaThanhToan { get; set; }
    public DateTime ThoiGian { get; set; }
    public string HinhThuc { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    public string NguoiThu { get; set; } = null!;
    public string? MaGiaoDich { get; set; }
    /// <summary>TIEN_MAT_QUAY hoặc CHUYEN_KHOAN_QR.</summary>
    public string Nguon { get; set; } = null!;
}

public class InvoiceHistoryOverviewDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? ChiTiet { get; set; }
}

/// <summary>Ai được làm gì với hóa đơn này (xem, thực hiện) và hiện có làm được không.</summary>
public class InvoiceActionDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public string AiThucHien { get; set; } = null!;
    public bool ChoPhep { get; set; }
    public string GiaiThich { get; set; } = "";
}

/// <summary>Toàn bộ thông tin để Quản lý hiểu và kiểm soát MỘT hóa đơn mà không phải mở module khác.</summary>
public class InvoiceOverviewDto
{
    public string MaHD { get; set; } = null!;
    public DateTime NgayLap { get; set; }
    public string NguoiLap { get; set; } = null!;

    // Của ai / booking nào / phòng nào
    public string? MaKhach { get; set; }
    public string? HoTenKhach { get; set; }
    public string? SoDTKhach { get; set; }
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? NguonDat { get; set; }
    public List<InvoiceRoomOverviewDto> Phong { get; set; } = new();

    // Cơ cấu tiền
    public List<InvoiceLineDto> CacKhoan { get; set; } = new();
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDV { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal PhuThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongGiamGia { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienDaCoc { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = null!;
    /// <summary>Kiểm tra công thức: Tổng các khoản trừ Cọc bằng Tổng phải thu, và Tổng phải thu bằng Đã thu cộng Còn nợ.</summary>
    public bool CongThucKhop { get; set; }
    public string CongThucGhiChu { get; set; } = "";

    // Hạn thanh toán
    public bool QuaHan { get; set; }
    public int SoNgayQuaHan { get; set; }
    public DateOnly? NgayTraCuoi { get; set; }
    public string HanThanhToan { get; set; } = "";

    public List<InvoicePaymentOverviewDto> ThanhToan { get; set; } = new();
    public List<InvoiceHistoryOverviewDto> LichSu { get; set; } = new();
    public List<InvoiceActionDto> ThaoTac { get; set; } = new();
    public List<string> CanhBao { get; set; } = new();
}
