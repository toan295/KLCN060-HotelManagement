namespace KLCN060.Domain.Enums;

/// <summary>Nhóm doanh thu của một dịch vụ (báo cáo doanh thu: dịch vụ chung / minibar / giặt là / khác). Mặc định DICH_VU.</summary>
public enum LoaiDoanhThuDichVu
{
    DICH_VU,
    MINIBAR,
    GIAT_LA,
    KHAC
}

/// <summary>Tình trạng xử lý của một thông báo tiền về: KHONG_CAN (đã khớp), CAN_XU_LY (bất thường, chờ kế toán), DA_XU_LY.</summary>
public enum TrangThaiXuLyDoiSoat
{
    KHONG_CAN,
    CAN_XU_LY,
    DA_XU_LY
}

/// <summary>Cách kế toán xử lý một thông báo tiền về bất thường.</summary>
public enum HuongXuLyDoiSoat
{
    /// <summary>Đã xử lý bên ngoài hệ thống (ghi nhận tay vào hóa đơn, liên hệ khách...), bắt buộc ghi chú.</summary>
    DA_XU_LY_NGOAI,
    /// <summary>Không phải tiền của khách sạn / giao dịch thử, bỏ qua.</summary>
    BO_QUA
}
