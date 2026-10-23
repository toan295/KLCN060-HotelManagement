namespace KLCN060.Web.Models.Payment;

/// <summary>Khớp với TransferDto bên KLCN060.Api - một yêu cầu thanh toán chuyển khoản (kèm mã QR).</summary>
public class TransferViewModel
{
    public string MaGiaoDich { get; set; } = null!;
    public string LoaiThamChieu { get; set; } = null!;
    public string MaThamChieu { get; set; } = null!;
    public decimal SoTien { get; set; }
    public string NoiDungChuyenKhoan { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayTao { get; set; }
    public DateTime HanThanhToan { get; set; }
    public DateTime? NgayThanhToan { get; set; }
    public string? MaGiaoDichNganHang { get; set; }
    public int SoGiayConLai { get; set; }
    public BankAccountViewModel NganHang { get; set; } = new();
    public bool CheDoThuNghiem { get; set; }
    public bool DungTaiKhoanMau { get; set; }
    public string? QrPayload { get; set; }
    public string? QrPngBase64 { get; set; }
}

public class BankAccountViewModel
{
    public string TenNganHang { get; set; } = "";
    public string SoTaiKhoan { get; set; } = "";
    public string TenChuTaiKhoan { get; set; } = "";
}
