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
