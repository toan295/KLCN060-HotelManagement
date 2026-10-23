using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Invoices;

public class PaymentRequest
{
    public string HinhThucThanhToan { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTien { get; set; }

    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột ChiTietThanhToan.MaGiaoDich
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

/// <summary>Một dòng trong danh sách hóa đơn (kế toán/lễ tân tìm hóa đơn cần thu tiền hoặc đối chiếu).</summary>
public class InvoiceSummaryDto
{
    public string MaHD { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string? HoTenKhach { get; set; }
    public DateTime NgayLap { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public string TrangThaiThanhToan { get; set; } = null!;

    // Thông tin để Quản lý quét danh sách mà không phải mở từng hóa đơn
    public string? MaPhieuDat { get; set; }
    public string? DanhSachPhong { get; set; }
    public DateTime? LanThuCuoi { get; set; }
    public string? HinhThucThuCuoi { get; set; }
    public bool QuaHan { get; set; }
    public int SoNgayQuaHan { get; set; }
    public bool ThuThua { get; set; }
}

public class InvoiceListQuery
{
    /// <summary>CHUA_THANH_TOAN, THANH_TOAN_MOT_PHAN hoặc DA_THANH_TOAN; bỏ trống = tất cả.</summary>
    public string? TrangThai { get; set; }
    /// <summary>Tìm theo mã hóa đơn, mã phiếu nhận hoặc tên khách.</summary>
    [StringLength(200, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? Q { get; set; }
    /// <summary>Lọc theo ngày lập hóa đơn.</summary>
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    /// <summary>true = chỉ hóa đơn còn nợ quá hạn; false = chỉ hóa đơn chưa quá hạn.</summary>
    public bool? QuaHan { get; set; }
    /// <summary>moi (mặc định), con_no (còn nợ giảm dần), tong_tien (tổng tiền giảm dần), qua_han (quá hạn lâu nhất trước).</summary>
    public string? SapXep { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
