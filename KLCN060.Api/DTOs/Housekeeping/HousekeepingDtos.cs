using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Housekeeping;

// ===================== Việc dọn phòng =====================

public class HousekeepingTaskDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public int Tang { get; set; }
    public string TenLoaiPhong { get; set; } = null!;

    /// <summary>VD (phòng trống chờ dọn) hoặc OD (phòng có khách cần dọn).</summary>
    public string TinhTrang { get; set; } = null!;
    public DateTime? TuLuc { get; set; }
    public double? SoGioCho { get; set; }

    /// <summary>KHAN = phòng đang giữ cho khách đến hôm nay; CAO = chờ dọn quá ngưỡng cảnh báo; BINH_THUONG.</summary>
    public string MucUuTien { get; set; } = "BINH_THUONG";
    public string? LyDoUuTien { get; set; }
    public bool KhachDenHomNay { get; set; }

    /// <summary>Tên đăng nhập / họ tên người đã nhận dọn phòng này (null = chưa ai nhận).</summary>
    public string? NguoiDon { get; set; }
    public string? HoTenNguoiDon { get; set; }
    public DateTime? BatDauDonLuc { get; set; }

    /// <summary>Quản lý giao việc (null = nhân viên tự nhận). BatDauDonLuc null mà NguoiDon có giá trị = đã giao, chưa bắt đầu.</summary>
    public string? NguoiPhanCong { get; set; }
    public DateTime? GiaoLuc { get; set; }

    /// <summary>Số sự cố chưa xử lý xong của phòng (để buồng phòng biết phòng còn hỏng hóc).</summary>
    public int SuCoDangMo { get; set; }
}

public class HousekeepingBoardDto
{
    public DateTime CapNhatLuc { get; set; }
    public int TongCanDon { get; set; }
    public int PhongTrongCanDon { get; set; }
    public int PhongCoKhachCanDon { get; set; }
    public int DangDon { get; set; }
    public int PhongKhan { get; set; }
    public int DaDonHomNay { get; set; }
    public int DaDonHomNayCuaToi { get; set; }

    // Tình hình toàn bộ phòng theo vòng đời buồng phòng (mỗi phòng đúng một nhóm): TongPhong = PhongSanSang + PhongCoKhach (OC) + TongCanDon + PhongBaoTri.
    public int TongPhong { get; set; }

    /// <summary>Phòng trống đã dọn (VC) hoặc đã kiểm tra (VI): nhận khách được ngay. VI là mức cao hơn do giám sát kiểm tra.</summary>
    public int PhongSanSang { get; set; }
    public int PhongDaKiemTra { get; set; }
    public int PhongCoKhach { get; set; }
    public int PhongBaoTri { get; set; }
    public List<HousekeepingTaskDto> Viec { get; set; } = new();
}

public class CleaningReportRowDto
{
    public string NguoiDon { get; set; } = null!;
    public string? HoTen { get; set; }
    public int SoPhongDaDon { get; set; }

    /// <summary>Số phòng có bấm "nhận dọn" (chỉ các phòng này mới tính được thời gian dọn).</summary>
    public int SoPhongCoNhanViec { get; set; }
    public double? ThoiGianTrungBinhPhut { get; set; }
}

public class CleaningReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int TongPhongDaDon { get; set; }
    public double? ThoiGianTrungBinhPhut { get; set; }
    public List<CleaningReportRowDto> Dong { get; set; } = new();
}

// ===================== Sự cố phòng =====================

public class IncidentDto
{
    public int MaSuCo { get; set; }
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public string TinhTrangPhong { get; set; } = null!;
    public string Loai { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string? MoTa { get; set; }
    public string MucDo { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string NguoiBao { get; set; } = null!;
    public string? HoTenNguoiBao { get; set; }
    public DateTime ThoiGianBao { get; set; }
    public string? NguoiXuLy { get; set; }
    public string? HoTenNguoiXuLy { get; set; }
    public DateTime? ThoiGianTiepNhan { get; set; }
    public DateTime? ThoiGianXong { get; set; }
    public string? GhiChuXuLy { get; set; }

    /// <summary>Ai đóng sự cố (xử lý xong hoặc hủy) và họ tên.</summary>
    public string? NguoiDong { get; set; }
    public string? HoTenNguoiDong { get; set; }

    /// <summary>Số giờ từ lúc báo tới lúc xong (hoặc tới bây giờ nếu chưa xong).</summary>
    public double SoGio { get; set; }
}

public class CreateIncidentRequest
{
    [Required(ErrorMessage = "Chưa chọn phòng.")]
    public string MaPhong { get; set; } = null!;

    /// <summary>DIEU_HOA, DIEN_CHIEU_SANG, NUOC, TV_THIET_BI_DIEN_TU, NOI_THAT, THIEU_TRANG_THIET_BI, VE_SINH, KHAC.</summary>
    [Required(ErrorMessage = "Chưa chọn loại sự cố.")]
    public string Loai { get; set; } = null!;

    [Required(ErrorMessage = "Chưa nhập tiêu đề sự cố.")]
    [StringLength(150, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TieuDe { get; set; } = null!;

    [StringLength(1000, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? MoTa { get; set; }

    /// <summary>THAP, TRUNG_BINH, CAO, KHAN_CAP.</summary>
    [Required(ErrorMessage = "Chưa chọn mức độ.")]
    public string MucDo { get; set; } = null!;
}

public class UpdateIncidentStatusRequest
{
    /// <summary>DA_TIEP_NHAN, DANG_XU_LY, DA_XU_LY, DA_HUY (hoặc DANG_XU_LY để mở lại sự cố đã xử lý).</summary>
    [Required(ErrorMessage = "Chưa chọn trạng thái.")]
    public string TrangThai { get; set; } = null!;

    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }
}

// ===================== Danh mục đồ dùng =====================

public class SupplyDto
{
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public string Nhom { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    /// <summary>Định mức theo đơn vị của <see cref="CachTinh"/> (mỗi phòng / mỗi giường / mỗi người).</summary>
    public int SoLuongChuan { get; set; }

    /// <summary>MOI_PHONG, MOI_GIUONG hoặc MOI_NGUOI.</summary>
    public string CachTinh { get; set; } = "MOI_PHONG";

    /// <summary>Mã các loại phòng có đồ dùng này; rỗng = mọi loại phòng.</summary>
    public List<string> MaLoaiPhongs { get; set; } = new();

    /// <summary>Định mức thực tế theo từng loại phòng được áp dụng (đã nhân theo giường/người): để quản lý thấy phòng 2 giường cần bao nhiêu.</summary>
    public List<SupplyRoomTypeQuantityDto> DinhMucTheoLoai { get; set; } = new();

    /// <summary>Dịch vụ dùng để tính phí khi khách tiêu thụ (null = chỉ theo dõi hao hụt, không tính phí).</summary>
    public string? MaDV { get; set; }
    public string? TenDV { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal? GiaTinhPhi { get; set; }
    public bool HoatDong { get; set; }
}

public class SupplyRoomTypeQuantityDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoGiuong { get; set; }
    public int SoNguoiTieuChuan { get; set; }
    public int DinhMuc { get; set; }
}

/// <summary>Loại phòng để chọn phạm vi áp dụng của đồ dùng.</summary>
public class SupplyRoomTypeDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public string? LoaiGiuong { get; set; }
    public int SoGiuong { get; set; }
    public int SoNguoiTieuChuan { get; set; }
}

public class SupplyRequest
{
    [Required(ErrorMessage = "Chưa nhập tên đồ dùng.")]
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TenVatDung { get; set; } = null!;

    /// <summary>KHAN_GA_GOI, AMENITIES, NUOC_UONG, MINIBAR, KHAC.</summary>
    [Required(ErrorMessage = "Chưa chọn nhóm.")]
    public string Nhom { get; set; } = null!;

    [Required(ErrorMessage = "Chưa nhập đơn vị tính.")]
    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string DonViTinh { get; set; } = null!;

    /// <summary>Định mức theo đơn vị của <see cref="CachTinh"/>.</summary>
    public int SoLuongChuan { get; set; }

    /// <summary>MOI_PHONG (mặc định), MOI_GIUONG (ga, gối) hoặc MOI_NGUOI (khăn, dép, amenities).</summary>
    public string? CachTinh { get; set; }

    /// <summary>Loại phòng áp dụng; để trống = mọi loại phòng.</summary>
    public List<string>? MaLoaiPhongs { get; set; }
    public string? MaDV { get; set; }
    public bool HoatDong { get; set; } = true;
}

// ===================== Kiểm phòng / minibar =====================

public class RoomCheckTemplateLineDto
{
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public string Nhom { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int SoLuongChuan { get; set; }

    /// <summary>Số lượng gợi ý điền vào "trước khi kiểm": số còn lại + bổ sung của lần kiểm trước trong cùng lượt ở, hoặc định mức.</summary>
    public int SoLuongTruoc { get; set; }
    public bool TinhPhi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal? GiaTinhPhi { get; set; }
}

public class RoomCheckTemplateDto
{
    public string MaPhong { get; set; } = null!;
    public string TinhTrangPhong { get; set; } = null!;

    /// <summary>Lượt lưu trú đang ở (null = phòng không có khách: tiêu thụ chỉ được ghi nhận, không tính phí được).</summary>
    public string? MaPhieuNhan { get; set; }
    public string? KetQuaKiemGanNhat { get; set; }
    public DateTime? KiemGanNhatLuc { get; set; }
    public List<RoomCheckTemplateLineDto> Dong { get; set; } = new();
}

public class RoomCheckLineRequest
{
    [Required(ErrorMessage = "Chưa chọn đồ dùng.")]
    public string MaVatDung { get; set; } = null!;

    /// <summary>Bỏ trống = lấy theo gợi ý của hệ thống (định mức hoặc kết quả kiểm trước).</summary>
    public int? SoLuongTruoc { get; set; }
    public int SoLuongSau { get; set; }
    public int SoLuongBoSung { get; set; }

    /// <summary>TIEU_THU, MAT_THIEU, HU_HONG. Bỏ trống = mặc định theo nhóm đồ dùng (minibar/nước/amenities: tiêu thụ; khăn/ga/gối: mất/thiếu; khác: hư hỏng).</summary>
    public string? LyDo { get; set; }

    [StringLength(200, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }
}

public class CreateRoomCheckRequest
{
    [Required(ErrorMessage = "Chưa chọn phòng.")]
    public string MaPhong { get; set; } = null!;

    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }

    [Required(ErrorMessage = "Chưa có dòng đồ dùng nào.")]
    public List<RoomCheckLineRequest> Dong { get; set; } = new();
}

public class RoomCheckLineDto
{
    public int MaChiTiet { get; set; }
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public string Nhom { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int SoLuongTruoc { get; set; }
    public int SoLuongSau { get; set; }
    public int SoLuongBoSung { get; set; }
    public int HaoHut { get; set; }
    public string? LyDo { get; set; }
    public string? GhiChu { get; set; }
    public int SoLuongTinhPhi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
    public string TrangThaiTinhPhi { get; set; } = null!;
    public string? LyDoBoQua { get; set; }
}

public class RoomCheckDto
{
    public int MaPhieuKiem { get; set; }
    public string MaPhong { get; set; } = null!;
    public string? MaPhieuNhan { get; set; }
    public string NguoiKiem { get; set; } = null!;
    public string? HoTenNguoiKiem { get; set; }
    public DateTime ThoiGian { get; set; }
    public string? GhiChu { get; set; }
    public List<RoomCheckLineDto> Dong { get; set; } = new();
}

/// <summary>Khoản tiêu thụ minibar chờ lễ tân tính phí cho khách.</summary>
public class MinibarChargeDto
{
    public int MaChiTiet { get; set; }
    public int MaPhieuKiem { get; set; }
    public string MaPhong { get; set; } = null!;
    public string MaPhieuNhan { get; set; } = null!;
    public string? HoTenKhach { get; set; }
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int SoLuongTruoc { get; set; }
    public int SoLuongSau { get; set; }
    public int SoLuongTinhPhi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
    public string TrangThaiTinhPhi { get; set; } = null!;
    public string NguoiKiem { get; set; } = null!;
    public string? HoTenNguoiKiem { get; set; }
    public DateTime ThoiGianKiem { get; set; }
    public string? NguoiXuLy { get; set; }
    public DateTime? ThoiGianXuLy { get; set; }
    public string? LyDoBoQua { get; set; }
}

public class WaiveMinibarChargeRequest
{
    [Required(ErrorMessage = "Cần nhập lý do bỏ qua khoản phí.")]
    [StringLength(300, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string LyDo { get; set; } = null!;
}

/// <summary>Tổng hợp hao hụt (thiếu / hỏng / tiêu thụ) theo đồ dùng trong một khoảng thời gian.</summary>
public class SupplyLossRowDto
{
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public string DonViTinh { get; set; } = null!;
    public int TieuThu { get; set; }
    public int MatThieu { get; set; }
    public int HuHong { get; set; }
    public int SoLanKiem { get; set; }
}

// ===================== Đồ bỏ quên =====================

public class LostFoundImageDto
{
    public int MaAnh { get; set; }
    public string TenFile { get; set; } = null!;
    public long KichThuocByte { get; set; }
    public DateTime ThoiGian { get; set; }
}

public class LostFoundDto
{
    public int MaDo { get; set; }
    public string? MaPhong { get; set; }
    public string? KhuVuc { get; set; }
    public DateTime ThoiGianPhatHien { get; set; }
    public string Loai { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public string NguoiNhat { get; set; } = null!;
    public string NguoiGhiNhan { get; set; } = null!;
    public string? ViTriLuuGiu { get; set; }
    public string TrangThai { get; set; } = null!;
    public int SoNgayLuuGiu { get; set; }

    /// <summary>Đủ số ngày lưu giữ tối thiểu để được thanh lý.</summary>
    public bool DuDieuKienThanhLy { get; set; }

    public DateTime? ThoiGianTra { get; set; }
    public string? NguoiNhanLai { get; set; }
    public string? GiayToNguoiNhan { get; set; }
    public string? NguoiTra { get; set; }
    public string? GhiChuTra { get; set; }
    public DateTime? ThoiGianThanhLy { get; set; }
    public string? NguoiThanhLy { get; set; }
    public string? LyDoThanhLy { get; set; }

    /// <summary>Khách ở phòng lúc phát hiện (lượt lưu trú gần nhất): chỉ trả về cho người có quyền xem khách hàng (lễ tân, quản lý), buồng phòng không thấy.</summary>
    public string? MaPhieuNhan { get; set; }
    public string? HoTenKhach { get; set; }
    public string? SoDTKhach { get; set; }

    public List<LostFoundImageDto> Anhs { get; set; } = new();
}

public class CreateLostFoundRequest
{
    /// <summary>Phòng phát hiện; bỏ trống thì bắt buộc nhập KhuVuc (nhặt ở sảnh, nhà hàng...).</summary>
    public string? MaPhong { get; set; }

    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? KhuVuc { get; set; }

    /// <summary>Bỏ trống = bây giờ.</summary>
    public DateTime? ThoiGianPhatHien { get; set; }

    /// <summary>GIAY_TO, DIEN_TU, VI_TIEN, TRANG_SUC, QUAN_AO, Y_TE, KHAC.</summary>
    [Required(ErrorMessage = "Chưa chọn loại đồ.")]
    public string Loai { get; set; } = null!;

    [Required(ErrorMessage = "Chưa nhập mô tả.")]
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MoTa { get; set; } = null!;

    /// <summary>Họ tên người nhặt; bỏ trống = người đang ghi nhận.</summary>
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? NguoiNhat { get; set; }

    /// <summary>Có nhập = chuyển thẳng sang "đang lưu giữ" tại vị trí này.</summary>
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? ViTriLuuGiu { get; set; }
}

public class UpdateLostFoundRequest
{
    public string? MaPhong { get; set; }

    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? KhuVuc { get; set; }

    [Required(ErrorMessage = "Chưa chọn loại đồ.")]
    public string Loai { get; set; } = null!;

    [Required(ErrorMessage = "Chưa nhập mô tả.")]
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MoTa { get; set; } = null!;

    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? NguoiNhat { get; set; }
}

public class StoreLostFoundRequest
{
    [Required(ErrorMessage = "Chưa nhập vị trí lưu giữ.")]
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string ViTriLuuGiu { get; set; } = null!;
}

public class ReturnLostFoundRequest
{
    [Required(ErrorMessage = "Chưa nhập tên người nhận lại.")]
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NguoiNhanLai { get; set; } = null!;

    /// <summary>Giấy tờ đối chiếu khi nhận (CCCD, hộ chiếu...) - bắt buộc để chắc đúng người nhận.</summary>
    [Required(ErrorMessage = "Chưa nhập giấy tờ đối chiếu của người nhận.")]
    [StringLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string GiayToNguoiNhan { get; set; } = null!;

    [StringLength(300, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }
}

public class DisposeLostFoundRequest
{
    [Required(ErrorMessage = "Cần nhập lý do thanh lý.")]
    [StringLength(300, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string LyDo { get; set; } = null!;
}
