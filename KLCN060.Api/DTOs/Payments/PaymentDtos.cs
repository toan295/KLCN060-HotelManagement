using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Payments;

public class CreateTransferRequest
{
    /// <summary>DAT_COC hoặc HOA_DON.</summary>
    public string LoaiThamChieu { get; set; } = null!;

    /// <summary>Mã phiếu đặt phòng (DAT_COC) hoặc mã hóa đơn (HOA_DON).</summary>
    [StringLength(15, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MaThamChieu { get; set; } = null!;

    /// <summary>Chỉ dùng cho HOA_DON để chuyển khoản một phần; bỏ trống = toàn bộ số còn nợ. DAT_COC luôn là đúng số cọc.</summary>
    [JsonConverter(typeof(NullableVndMoneyJsonConverter))]
    public decimal? SoTien { get; set; }
}

public class BankAccountDto
{
    public string TenNganHang { get; set; } = null!;
    public string SoTaiKhoan { get; set; } = null!;
    public string TenChuTaiKhoan { get; set; } = null!;
}

public class TransferDto
{
    public string MaGiaoDich { get; set; } = null!;
    public string LoaiThamChieu { get; set; } = null!;
    public string MaThamChieu { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    public string NoiDungChuyenKhoan { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime NgayTao { get; set; }
    public DateTime HanThanhToan { get; set; }
    public DateTime? NgayThanhToan { get; set; }
    public string? MaGiaoDichNganHang { get; set; }

    /// <summary>Số giây còn lại trước khi hết hạn (0 nếu không còn chờ thanh toán) - client dùng để chạy đồng hồ đếm ngược.</summary>
    public int SoGiayConLai { get; set; }

    public BankAccountDto NganHang { get; set; } = null!;

    /// <summary>true khi hệ thống đang ở chế độ thử nghiệm: khách có thể bấm "Tôi đã chuyển khoản" để giả lập tiền về.</summary>
    public bool CheDoThuNghiem { get; set; }

    /// <summary>true khi đang hiển thị tài khoản MẪU (chưa khai báo đủ tài khoản ngân hàng thật).</summary>
    public bool DungTaiKhoanMau { get; set; }

    /// <summary>Chuỗi dữ liệu trong mã QR (chuẩn VietQR/EMVCo). Chỉ có khi đang chờ thanh toán.</summary>
    public string? QrPayload { get; set; }

    /// <summary>Ảnh PNG của mã QR, mã hóa base64. Chỉ có khi đang chờ thanh toán.</summary>
    public string? QrPngBase64 { get; set; }
}

public class TransferListQuery
{
    public string? TrangThai { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BankNoticeDto
{
    public int MaThongBao { get; set; }
    public string MaGiaoDichNganHang { get; set; } = null!;
    public string SoTaiKhoanNhan { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    public string NoiDung { get; set; } = null!;
    public DateTime ThoiGianNganHang { get; set; }
    public DateTime ThoiGianNhan { get; set; }
    public string KetQua { get; set; } = null!;
    public string? MaGiaoDich { get; set; }
    public string? GhiChu { get; set; }

    /// <summary>KHONG_CAN (đã khớp), CAN_XU_LY (bất thường, chờ kế toán), DA_XU_LY.</summary>
    public string TrangThaiXuLy { get; set; } = "KHONG_CAN";
    public string? NguoiXuLy { get; set; }
    public DateTime? NgayXuLy { get; set; }

    /// <summary>DA_XU_LY_NGOAI hoặc BO_QUA (dữ liệu cũ có thể còn HOAN_TIEN).</summary>
    public string? HuongXuLy { get; set; }
    public string? GhiChuXuLy { get; set; }
}

/// <summary>Nội dung thông báo "tiền về" mà ngân hàng gửi tới webhook của hệ thống.</summary>
public class BankNotificationRequest
{
    [Required(ErrorMessage = "Thiếu maGiaoDichNganHang.")]
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MaGiaoDichNganHang { get; set; } = null!;

    [Required(ErrorMessage = "Thiếu soTaiKhoanNhan.")]
    [StringLength(30, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string SoTaiKhoanNhan { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTien { get; set; }

    [Required(ErrorMessage = "Thiếu noiDung.")]
    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NoiDung { get; set; } = null!;

    public DateTime ThoiGian { get; set; }
}

public class BankNotificationResultDto
{
    public string KetQua { get; set; } = null!;
    public string ThongBao { get; set; } = null!;
    public string? MaGiaoDich { get; set; }
    public string MaGiaoDichNganHang { get; set; } = null!;
    /// <summary>true nếu thông báo này đã được xử lý trước đó (ngân hàng gửi lại) - không ghi nhận tiền thêm lần nữa.</summary>
    public bool DaXuLyTruocDo { get; set; }
}

/// <summary>Nội dung webhook SePay (https://docs.sepay.vn/tich-hop-webhooks.html) khi tài khoản ngân hàng có biến động số dư.</summary>
public class SePayWebhookRequest
{
    public long Id { get; set; }
    public string? Gateway { get; set; }
    public string? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? SubAccount { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }

    /// <summary>"in" = tiền vào, "out" = tiền ra.</summary>
    public string? TransferType { get; set; }
    public string? Description { get; set; }
    public decimal TransferAmount { get; set; }
    public decimal Accumulated { get; set; }
    public string? ReferenceCode { get; set; }
}
