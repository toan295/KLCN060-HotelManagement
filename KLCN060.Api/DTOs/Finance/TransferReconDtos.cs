using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Finance;

/// <summary>Một nhóm ngoại lệ tiền (thiếu tiền, thừa tiền, sai nội dung...) trong kỳ đối soát.</summary>
public class TransferExceptionGroupDto
{
    public string Loai { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; }
    public int SoChuaXuLy { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTienChuaXuLy { get; set; }
}

/// <summary>Bức tranh tiền chuyển khoản: đầu màn hình Đối soát chuyển khoản.</summary>
public class TransferReconOverviewDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public DateTime CapNhatLuc { get; set; }
    /// <summary>Thời điểm thông báo tiền về gần nhất hệ thống nhận được từ ngân hàng (null nếu chưa có).</summary>
    public DateTime? ThongBaoMoiNhatLuc { get; set; }

    /// <summary>Tổng tiền ngân hàng báo đã về trong kỳ (mọi thông báo, kể cả bất thường).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienNganHangNhan { get; set; }
    /// <summary>Phần đã khớp và được ghi nhận vào cọc / hóa đơn.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienDaKhop { get; set; }
    public int TongThongBao { get; set; }
    public int SoKhop { get; set; }
    public int SoChuaKhop { get; set; }
    public int SoChuaXuLy { get; set; }
    public int SoDaXuLy { get; set; }
    /// <summary>Giá trị các khoản bất thường đang chờ xử lý (tiền đã về nhưng hệ thống chưa ghi nhận).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTienCanXuLy { get; set; }
    /// <summary>Tổng chênh lệch tuyệt đối của các khoản bất thường trong kỳ (xem công thức ở từng dòng).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongChenhLech { get; set; }
    public int SoChuaXuLyQuaHan { get; set; }
    public int NgayQuaHan { get; set; }

    public int SoYeuCauChoThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienYeuCauChoThanhToan { get; set; }
    public int SoYeuCauHetHan { get; set; }
    public List<TransferExceptionGroupDto> NhomNgoaiLe { get; set; } = new();
}

public class TransferReconRowDto
{
    public int MaThongBao { get; set; }
    public DateTime ThoiGianNganHang { get; set; }
    public DateTime ThoiGianNhan { get; set; }
    public string MaGiaoDichNganHang { get; set; } = null!;
    public string SoTaiKhoanNhan { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTienNganHang { get; set; }
    public string NoiDung { get; set; } = null!;

    public string KetQua { get; set; } = null!;
    /// <summary>KHOP, THIEU_TIEN, THUA_TIEN, SAI_SO_TIEN, SAI_NOI_DUNG, CHUA_XAC_DINH, HET_HAN, DA_HUY, THANH_TOAN_NHIEU_LAN, SAI_TAI_KHOAN, KHONG_AP_DUNG.</summary>
    public string Loai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public string LyDo { get; set; } = null!;
    public string GoiYXuLy { get; set; } = "";

    // Yêu cầu thanh toán / booking / hóa đơn tương ứng
    public string? MaGiaoDich { get; set; }
    public string? TrangThaiGiaoDich { get; set; }
    public string? LoaiThamChieu { get; set; }
    public string? MaThamChieu { get; set; }
    public string? MaPhieuDat { get; set; }
    public string? MaHoaDon { get; set; }
    public string? TenKhach { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal? SoTienPhaiThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTienDaGhiNhan { get; set; }
    /// <summary>Số tiền ngân hàng nhận trừ số tiền hệ thống ghi nhận: 0 nếu khớp; sai số tiền = tiền NH − phải thu (âm là thiếu); trường hợp khác = toàn bộ tiền NH (hệ thống chưa ghi nhận).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ChenhLech { get; set; }

    // Xử lý
    public string TrangThaiXuLy { get; set; } = null!;
    public int SoNgayTonDong { get; set; }
    public bool QuaHanXuLy { get; set; }
    public string? NguoiXuLy { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public string? HuongXuLy { get; set; }
    public string? GhiChuXuLy { get; set; }
}

public class TransferReconHistoryDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = null!;
    public string HanhDong { get; set; } = null!;
    public string? ChiTiet { get; set; }
}

public class TransferReconDetailDto : TransferReconRowDto
{
    public DateTime? NgayTaoYeuCau { get; set; }
    public DateTime? HanThanhToan { get; set; }
    public string? NguoiTaoYeuCau { get; set; }
    /// <summary>Các thông báo khác của cùng một yêu cầu chuyển khoản (khách chuyển nhiều lần).</summary>
    public List<TransferReconRowDto> ThongBaoCungGiaoDich { get; set; } = new();
    public List<TransferReconHistoryDto> LichSu { get; set; } = new();
    /// <summary>Các hướng xử lý cho phép với khoản này (rỗng nếu đã khớp / đã xử lý).</summary>
    public List<string> HuongXuLyChoPhep { get; set; } = new();
}

public class TransferReconListQuery
{
    public string? Q { get; set; }
    /// <summary>KHOP, hoặc một loại ngoại lệ (THIEU_TIEN, THUA_TIEN, SAI_NOI_DUNG, CHUA_XAC_DINH, HET_HAN, DA_HUY, THANH_TOAN_NHIEU_LAN, SAI_TAI_KHOAN, KHONG_AP_DUNG).</summary>
    public string? Loai { get; set; }
    /// <summary>KHONG_CAN, CAN_XU_LY, DA_XU_LY.</summary>
    public string? TrangThaiXuLy { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
