using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Bookings;

public class BookingRoomRequest
{
    public string MaLoai { get; set; } = null!;
    public int SoLuong { get; set; } = 1;
}

public class CreateBookingRequest
{
    public string LoaiDatPhong { get; set; } = null!;

    /// <summary>KH: bị bỏ qua (lấy từ JWT). LT: bắt buộc.</summary>
    public string? MaKhach { get; set; }

    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public List<BookingRoomRequest> PhongCanDat { get; set; } = new();
    /// <summary>Một khuyến mãi (giữ để tương thích); muốn áp dụng nhiều khuyến mãi dùng <see cref="MaKhuyenMais"/>.</summary>
    public string? MaKhuyenMai { get; set; }

    /// <summary>Các khuyến mãi áp dụng cùng lúc (tối đa <see cref="BookingService.SoKhuyenMaiToiDa"/>); mỗi khuyến mãi tính trên tiền phòng gốc, tổng giảm không vượt tiền phòng.</summary>
    public List<string>? MaKhuyenMais { get; set; }
}

public class HeldRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = "";

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaApDung { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
}

public class BookingDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;

    /// <summary>WEBSITE (khách tự đặt) hoặc QUAY_LE_TAN (lễ tân đặt hộ).</summary>
    public string NguonDat { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienDuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongKhuyenMai { get; set; }

    /// <summary>Thời điểm phiếu bị hủy / bị đánh dấu không đến, người thực hiện, lý do và số cọc đã hoàn (chỉ có khi phiếu ở trạng thái DA_HUY hoặc KHONG_DEN).</summary>
    public DateTime? NgayHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? HuyBoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTienHoanCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCocGiuLai { get; set; }

    public List<HeldRoomDto> PhongDaGiu { get; set; } = new();

    /// <summary>Người ở cùng đã khai báo (ngoài khách đại diện).</summary>
    public List<CompanionDto> KhachDiCung { get; set; } = new();
}

public class CompanionDto
{
    public int MaKhachDiCung { get; set; }
    public string HoTen { get; set; } = null!;
    public string? CCCD { get; set; }
    public string? MaPhong { get; set; }
    public DateTime NgayThem { get; set; }
    public string? NguoiThem { get; set; }
}

public class AddCompanionRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string HoTen { get; set; } = null!;

    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? CCCD { get; set; }

    /// <summary>Phòng khách này ở (phải là phòng thuộc phiếu); bỏ trống nếu chưa xếp.</summary>
    public string? MaPhong { get; set; }
}

public class BookingSummaryDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string SoDTKhach { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string NguonDat { get; set; } = null!;
    public DateTime NgayDat { get; set; }
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public int SoPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienDuKien { get; set; }

    public DateTime? NgayHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? HuyBoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTienHoanCoc { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCocGiuLai { get; set; }

    /// <summary>true = đã qua ngày đón dự kiến mà khách chưa nhận phòng (phiếu vẫn đang giữ phòng) - ứng viên để đánh dấu "không đến".</summary>
    public bool QuaHanChuaNhan { get; set; }

    /// <summary>Đã nhận tiền cọc (có ngày nhận cọc).</summary>
    public bool DaThuCoc { get; set; }
}

public class BookingListQuery
{
    public string? TrangThai { get; set; }

    /// <summary>Lọc theo kênh đặt: WEBSITE hoặc QUAY_LE_TAN.</summary>
    public string? Nguon { get; set; }

    /// <summary>Lọc theo loại đặt phòng: CA_NHAN hoặc DOAN (theo dõi đặt phòng đoàn).</summary>
    public string? LoaiDatPhong { get; set; }

    /// <summary>true = chỉ các phiếu đã qua ngày đón dự kiến mà khách chưa nhận phòng (chờ xử lý "không đến").</summary>
    public bool? QuaHanChuaNhan { get; set; }

    /// <summary>Cột ngày dùng cho TuNgay/DenNgay: NGAY_DON (mặc định, ngày đón dự kiến), NGAY_DAT (ngày tạo phiếu) hoặc NGAY_HUY (ngày hủy/không đến).</summary>
    public string? LocTheoNgay { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    [StringLength(200, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// ----- Check-in -----

public class CheckInGuestInfo
{
    public string MaPhong { get; set; } = null!;
    [StringLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")] // khớp cột Khach.CCCD
    public string? CCCD { get; set; }
    public int SoNguoi { get; set; }
}

public class BookingCheckInRequest
{
    /// <summary>Nhận phòng SỚM (trước ngày đón dự kiến): phải bật cờ này, ghi lý do, và phòng phải trống từ hôm nay; ngày đón của phiếu được dời về hôm nay và giá tính lại.</summary>
    public bool NhanSom { get; set; }

    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }

    public List<string> PhongNhan { get; set; } = new();
    public List<CheckInGuestInfo> Khach { get; set; } = new();
}

public class WalkInCheckInRequest
{
    /// <summary>Khách lưu trú (tạo trước bằng POST /guests nếu chưa có) — nơi lưu CCCD.</summary>
    public string MaKhach { get; set; } = null!;

    /// <summary>Ngày trả dự kiến (BẮT BUỘC, phải sau hôm nay): khách lưu trú phải có ngày trả cụ thể; dùng để biết phòng nào sắp check-out và kiểm tra khi gia hạn.</summary>
    public DateOnly? NgayTraDuKien { get; set; }

    /// <summary>Mỗi phần tử = 1 phòng cụ thể được nhận.</summary>
    public List<CheckInGuestInfo> Khach { get; set; } = new();
}

public class CheckedInRoomDto
{
    public string MaPhong { get; set; } = null!;
    public int SoNguoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGia { get; set; }

    public string TinhTrangPhong { get; set; } = null!;
}

public class CheckInResultDto
{
    public string MaPhieuNhan { get; set; } = null!;
    public string? MaPhieuDat { get; set; }
    public string? TrangThaiPhieuDat { get; set; }
    public DateTime NgayNhan { get; set; }
    /// <summary>Tài khoản đã thực hiện nhận phòng (cũng có trong nhật ký NHAN_PHONG).</summary>
    public string? NguoiThucHien { get; set; }
    public List<CheckedInRoomDto> PhongDaNhan { get; set; } = new();
}

/// <summary>Thông tin lễ tân cần nhìn TRƯỚC khi bấm nhận phòng theo một phiếu đặt (một lần gọi, không phải mở màn khác để tra).</summary>
public class CheckInInfoDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public string MaKhach { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string SoDTKhach { get; set; } = null!;
    /// <summary>Số giấy tờ đã có trong hồ sơ (đã che, chỉ 4 số cuối); null = chưa có, phải nhập khi nhận.</summary>
    public string? CccdDaCo { get; set; }
    public bool ThieuCccd { get; set; }
    public string LoaiDatPhong { get; set; } = null!;
    public DateOnly NgayDonDuKien { get; set; }
    public DateOnly NgayTraDuKien { get; set; }
    public int SoDem { get; set; }
    public DateOnly HomNay { get; set; }
    /// <summary>DUNG_NGAY / SOM (trước ngày đón) / TRE (quá ngày đón) / QUA_HAN (đã quá ngày trả dự kiến).</summary>
    public string ThoiDiem { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienDuKien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienCoc { get; set; }
    public bool DaThuCoc { get; set; }
    /// <summary>Phần còn lại phải thu khi trả phòng (tổng dự kiến − cọc đã thu), chỉ mang tính tham khảo.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConLaiDuKien { get; set; }
    public int SoKhachDiCung { get; set; }
    public string? MaPhieuNhan { get; set; }
    /// <summary>Phiếu ở trạng thái cho phép nhận phòng và còn ít nhất một phòng nhận được.</summary>
    public bool NhanDuoc { get; set; }
    public string? LyDoKhongNhanDuoc { get; set; }
    public List<string> CanhBao { get; set; } = new();
    public List<CheckInRoomInfoDto> Phong { get; set; } = new();
}

public class CheckInRoomInfoDto
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public string TinhTrang { get; set; } = null!;
    public bool DaNhan { get; set; }
    public bool NhanDuoc { get; set; }
    public string? LyDoKhongNhanDuoc { get; set; }
    public int SucChuaTieuChuan { get; set; }
    public int SucChuaToiDa { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DonGiaApDung { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThanhTien { get; set; }
}

// ----- Hủy đặt phòng -----

public class CancelBookingRequest
{
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]   // khớp cột PhieuDatPhong.LyDoHuy
    public string? LyDo { get; set; }
}

/// <summary>Sửa phiếu đặt phòng trong trường hợp đặc biệt (chỉ Quản lý): đổi ngày đón / ngày trả, bắt buộc ghi lý do để nhật ký truy vết.</summary>
public class DoiPhongDatRequest
{
    /// <summary>Phòng đang giữ trong phiếu muốn đổi.</summary>
    public string MaPhongCu { get; set; } = null!;

    /// <summary>Chỉ định phòng mới (đổi phòng)...</summary>
    public string? MaPhongMoi { get; set; }

    /// <summary>...hoặc chỉ định loại phòng mới (đổi loại phòng - hệ thống chọn phòng trống đầu tiên của loại đó). Đúng một trong hai.</summary>
    public string? MaLoaiMoi { get; set; }
}

/// <summary>Kết quả "xem trước" một thay đổi phiếu đặt phòng (chưa lưu gì): giá cũ/mới từng phòng, chênh lệch, tiền cọc, khuyến mãi.</summary>
public class BookingChangePreviewDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateOnly NgayDonCu { get; set; }
    public DateOnly NgayTraCu { get; set; }
    public DateOnly NgayDonMoi { get; set; }
    public DateOnly NgayTraMoi { get; set; }
    public int SoDemCu { get; set; }
    public int SoDemMoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongCu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongMoi { get; set; }

    /// <summary>Tổng mới trừ tổng cũ (dương = khách phải trả thêm, âm = giảm).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ChenhLech { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal KhuyenMaiCu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal KhuyenMaiMoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCocCu { get; set; }

    /// <summary>Tiền cọc sau thay đổi: phiếu chờ xác nhận cọc thì tính lại 50% tổng mới; phiếu đã thu cọc thì giữ nguyên số đã thu.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCocMoi { get; set; }

    public bool DaThuCoc { get; set; }

    /// <summary>Đã thu cọc: 50% tổng mới trừ cọc đã thu (dương = nên thu thêm, âm = cọc đã thu dư so với 50%; hệ thống KHÔNG tự thu thêm/hoàn).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal CocLechSoVoiChuan { get; set; }

    public bool HuyMaQrChoCoc { get; set; }
    public List<BookingChangeRoomDto> Phongs { get; set; } = new();
}

public class BookingChangeRoomDto
{
    public string MaPhong { get; set; } = null!;
    public string MaLoai { get; set; } = null!;

    /// <summary>Phòng cũ được thay bằng phòng này (null = phòng giữ nguyên).</summary>
    public string? DoiTuPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaCu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaMoi { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTienCu { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTienMoi { get; set; }
}

public class UpdateBookingRequest
{
    /// <summary>Ngày đón mới; bỏ trống = giữ nguyên.</summary>
    public DateOnly? NgayDonDuKien { get; set; }

    /// <summary>Ngày trả mới; bỏ trống = giữ nguyên.</summary>
    public DateOnly? NgayTraDuKien { get; set; }

    /// <summary>Đổi phòng / đổi loại phòng cho từng phòng đang giữ (chỉ khi phiếu chưa nhận phòng).</summary>
    public List<DoiPhongDatRequest>? DoiPhong { get; set; }

    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class NoShowRequest
{
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? LyDo { get; set; }
}

public class CancelBookingResultDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public bool HoanCoc { get; set; }

    /// <summary>Số ngày từ hôm nay tới ngày đón dự kiến, dùng để đối chiếu mốc 30 ngày.</summary>
    public int SoNgayConLai { get; set; }

    /// <summary>Số tiền cọc thực tế cần hoàn (0 nếu không hoàn hoặc phiếu chưa xác nhận đặt cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal SoTienHoan { get; set; }
}

/// <summary>Một dòng của bảng báo giá: loại phòng, số phòng muốn đặt, số phòng còn trống trong khoảng ngày và tiền.</summary>
public class BookingQuoteLineDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoLuong { get; set; }
    public int SoPhongTrong { get; set; }
    public bool DuPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaBinhQuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTienMotPhong { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }
}

public class BookingQuotePromotionDto
{
    public string MaKM { get; set; } = null!;
    public string TenKM { get; set; } = null!;
    public bool DuDieuKien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienGiam { get; set; }

    /// <summary>Lý do khuyến mãi không áp dụng được (null nếu đủ điều kiện).</summary>
    public string? LyDoKhongDat { get; set; }
}

/// <summary>Báo giá đặt phòng (KHÔNG lưu, KHÔNG giữ phòng): phòng còn trống, tiền phòng, khuyến mãi và tiền cọc đúng như khi tạo thật.</summary>
public class BookingQuoteDto
{
    public int SoDem { get; set; }
    public bool DuPhong { get; set; }
    public List<BookingQuoteLineDto> Dong { get; set; } = new();
    /// <summary>Khuyến mãi đầu tiên (giữ để tương thích); danh sách đầy đủ ở <see cref="KhuyenMais"/>.</summary>
    public BookingQuotePromotionDto? KhuyenMai { get; set; }
    public List<BookingQuotePromotionDto> KhuyenMais { get; set; } = new();

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTien { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongKhuyenMai { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongSauKhuyenMai { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TienCoc { get; set; }
}
