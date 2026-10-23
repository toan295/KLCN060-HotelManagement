namespace KLCN060.Domain.Enums;

/// <summary>Kết quả đối soát một thông báo "tiền về" từ ngân hàng với các giao dịch đang chờ.</summary>
public enum KetQuaDoiSoatNganHang
{
    /// <summary>Khớp mã, đúng số tiền, còn hạn: đã ghi nhận thanh toán.</summary>
    KHOP,
    /// <summary>Khớp mã nhưng số tiền khác số cần thanh toán: không tự ghi nhận, chờ xử lý thủ công.</summary>
    SAI_SO_TIEN,
    /// <summary>Nội dung chuyển khoản không chứa mã giao dịch nào của hệ thống.</summary>
    KHONG_TIM_THAY_GIAO_DICH,
    /// <summary>Tiền về sau khi giao dịch đã hết hạn / bị hủy.</summary>
    GIAO_DICH_KHONG_CON_HIEU_LUC,
    /// <summary>Giao dịch đã được thanh toán trước đó (khách chuyển 2 lần).</summary>
    GIAO_DICH_DA_THANH_TOAN,
    /// <summary>Tiền chuyển vào tài khoản khác, không phải tài khoản nhận của khách sạn.</summary>
    SAI_TAI_KHOAN_NHAN,
    /// <summary>Khớp giao dịch nhưng nghiệp vụ không còn áp dụng được (VD hóa đơn đã được thu đủ bằng tiền mặt).</summary>
    KHONG_AP_DUNG_DUOC
}
