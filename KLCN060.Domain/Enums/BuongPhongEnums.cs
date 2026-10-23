namespace KLCN060.Domain.Enums;

/// <summary>Loại sự cố trong phòng do buồng phòng/lễ tân báo cho quản lý.</summary>
public enum LoaiSuCo
{
    DIEU_HOA,
    DIEN_CHIEU_SANG,
    NUOC,
    TV_THIET_BI_DIEN_TU,
    NOI_THAT,
    THIEU_TRANG_THIET_BI,
    VE_SINH,
    KHAC
}

public enum MucDoSuCo
{
    THAP,
    TRUNG_BINH,
    CAO,
    KHAN_CAP
}

/// <summary>MOI -> DA_TIEP_NHAN -> DANG_XU_LY -> DA_XU_LY; hủy (DA_HUY) khi báo nhầm; mở lại (DA_XU_LY -> DANG_XU_LY) khi chưa khắc phục được.</summary>
public enum TrangThaiSuCo
{
    MOI,
    DA_TIEP_NHAN,
    DANG_XU_LY,
    DA_XU_LY,
    DA_HUY
}

/// <summary>Nhóm vật dụng trong phòng: khăn/ga/gối, đồ dùng miễn phí (amenities), nước uống, minibar, đồ khác.</summary>
public enum NhomVatDung
{
    KHAN_GA_GOI,
    AMENITIES,
    NUOC_UONG,
    MINIBAR,
    KHAC
}

/// <summary>Cách tính định mức đồ dùng của một phòng: cố định mỗi phòng, nhân theo số giường (ga, gối) hoặc nhân theo số người tiêu chuẩn (khăn, dép, amenities).</summary>
public enum CachTinhDinhMuc
{
    MOI_PHONG,
    MOI_GIUONG,
    MOI_NGUOI
}

/// <summary>Lý do hao hụt của phần chênh lệch (số lượng trước - sau) khi kiểm phòng.</summary>
public enum LyDoHaoHut
{
    TIEU_THU,
    MAT_THIEU,
    HU_HONG
}

/// <summary>Tình trạng tính phí của một dòng kiểm phòng có tiêu thụ minibar.</summary>
public enum TrangThaiTinhPhi
{
    KHONG_TINH_PHI,
    CHO_TINH_PHI,
    DA_TINH_PHI,
    BO_QUA
}

public enum LoaiDoBoQuen
{
    GIAY_TO,
    DIEN_TU,
    VI_TIEN,
    TRANG_SUC,
    QUAN_AO,
    Y_TE,
    KHAC
}

/// <summary>Found / Stored / Returned / Disposed.</summary>
public enum TrangThaiDoBoQuen
{
    DA_TIM_THAY,
    DANG_LUU_GIU,
    DA_TRA_KHACH,
    DA_THANH_LY
}

public enum TrangThaiPhieuDon
{
    DANG_DON,
    DA_XONG,
    DA_HUY
}
