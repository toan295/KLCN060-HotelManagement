using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Invoices;

/// <summary>Thông tin người mua để lập hóa đơn điện tử. Khách là doanh nghiệp thì bắt buộc tên đơn vị + mã số thuế.</summary>
public class IssueEInvoiceRequest
{
    [Required(ErrorMessage = "Thiếu tên người mua.")]
    [StringLength(200, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TenNguoiMua { get; set; } = null!;

    [StringLength(300, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? TenDonVi { get; set; }

    [StringLength(14, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? MaSoThue { get; set; }

    [StringLength(300, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? DiaChi { get; set; }

    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? Email { get; set; }
}

public class EInvoiceDto
{
    public string MaHD { get; set; } = null!;

    /// <summary>CHO_PHAT_HANH, DANG_XU_LY, DA_PHAT_HANH hoặc LOI.</summary>
    public string TrangThai { get; set; } = null!;
    public string TenNguoiMua { get; set; } = null!;
    public string? TenDonVi { get; set; }
    public string? MaSoThue { get; set; }
    public string? DiaChi { get; set; }
    public string? Email { get; set; }

    public decimal ThueSuat { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienChuaThue { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienThue { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongThanhToan { get; set; }

    public string? NhaCungCap { get; set; }
    public string? MauSo { get; set; }
    public string? KyHieu { get; set; }
    public string? SoHoaDon { get; set; }
    public string? MaCQT { get; set; }
    public DateTime? NgayPhatHanh { get; set; }
    public string? MaTraCuu { get; set; }
    public string? LienKetTraCuu { get; set; }
    public string? ThongBaoLoi { get; set; }
    public int SoLanThu { get; set; }
    public string NguoiYeuCau { get; set; } = null!;
    public DateTime NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    /// <summary>true khi hệ thống đã kết nối nhà cung cấp HĐĐT (nếu false, hóa đơn chỉ nằm ở trạng thái chờ phát hành).</summary>
    public bool DaKetNoiNhaCungCap { get; set; }
}
