using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Rates;

/// <remarks>Khai báo dạng record để có thể sao chép-sửa (with) khi dựng yêu cầu; hành vi JSON/binding không đổi.</remarks>
public record RateRuleRequest
{
    [StringLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TenQuyTac { get; set; } = null!;

    /// <summary>CUOI_TUAN, MUA_CAO_DIEM, LE_TET, DOANH_NGHIEP hoặc KHAC.</summary>
    public string LoaiQuyTac { get; set; } = null!;

    /// <summary>Loại phòng áp dụng; bỏ trống = mọi loại phòng.</summary>
    public string? MaLoai { get; set; }

    /// <summary>PHAN_TRAM (GiaTri là % tăng/giảm, VD 20 hoặc -10) hoặc GIA_CO_DINH (GiaTri là giá mỗi đêm).</summary>
    public string KieuDieuChinh { get; set; } = null!;
    public decimal GiaTri { get; set; }

    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }

    /// <summary>Thứ áp dụng theo đêm lưu trú, VD "5,6" = đêm thứ 6 và thứ 7 (0 = Chủ nhật ... 6 = Thứ bảy); bỏ trống = mọi ngày.</summary>
    public string? CacThuApDung { get; set; }

    /// <summary>CA_NHAN hoặc DOAN; bỏ trống = mọi loại đặt phòng.</summary>
    public string? LoaiDatPhongApDung { get; set; }
    public int UuTien { get; set; }
    public bool HoatDong { get; set; } = true;

    [StringLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string? GhiChu { get; set; }
}

public class RateRuleDto
{
    public int MaQuyTac { get; set; }
    public string TenQuyTac { get; set; } = null!;
    public string LoaiQuyTac { get; set; } = null!;
    public string? MaLoai { get; set; }
    public string? TenLoai { get; set; }
    public string KieuDieuChinh { get; set; } = null!;
    public decimal GiaTri { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public string? CacThuApDung { get; set; }
    public string? LoaiDatPhongApDung { get; set; }
    public int UuTien { get; set; }
    public bool HoatDong { get; set; }
    public string? GhiChu { get; set; }
    public DateTime NgayTao { get; set; }
    public string? TaoBoi { get; set; }

    /// <summary>TAT, CHUA_BAT_DAU, DANG_AP_DUNG hoặc DA_HET_HAN (theo cờ bật/tắt và khoảng ngày so với hôm nay).</summary>
    public string HieuLuc { get; set; } = "DANG_AP_DUNG";

    /// <summary>Số đêm / số phiếu đặt đã chốt giá bằng quy tắc này (giữ nguyên khi quy tắc đổi hoặc bị xóa).</summary>
    public int SoDemDaChot { get; set; }
    public int SoPhieuDaChot { get; set; }

    /// <summary>Cảnh báo: quy tắc khác đang bật có cùng phạm vi giao nhau và cùng độ ưu tiên (hệ thống sẽ lấy quy tắc tạo sau).</summary>
    public List<string> CanhBao { get; set; } = new();
}

public class RateHistoryEntryDto
{
    public DateTime ThoiGian { get; set; }
    public string NguoiThucHien { get; set; } = "";
    public string HanhDong { get; set; } = "";
    public string MoTaHanhDong { get; set; } = "";
    public string? DoiTuong { get; set; }
    public string? ChiTiet { get; set; }
}

/// <summary>Một quy tắc khớp với một đêm (kể cả quy tắc bị quy tắc ưu tiên cao hơn thay thế).</summary>
public class RateMatchedRuleDto
{
    public int MaQuyTac { get; set; }
    public string TenQuyTac { get; set; } = null!;
    public int UuTien { get; set; }
    public string DieuChinh { get; set; } = "";
    public bool DuocApDung { get; set; }
}

/// <summary>Giá chốt của một đêm trong phiếu đặt, kèm giá nếu tính lại theo bảng giá hiện hành.</summary>
public class BookingNightPriceDto
{
    public DateOnly Dem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaCoBan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal Gia { get; set; }
    public int? MaQuyTac { get; set; }
    public string? TenQuyTac { get; set; }
    public string? DieuChinh { get; set; }

    /// <summary>Giá của đêm này nếu tính theo bảng giá HIỆN HÀNH (để so sánh; giá chốt không bị đổi).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaHienHanh { get; set; }
}

public class BookingRoomPriceDto
{
    public string MaPhong { get; set; } = null!;
    public string TenPhong { get; set; } = null!;
    public string TenLoaiPhong { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaApDung { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal ThanhTien { get; set; }

    /// <summary>false = phiếu tạo trước khi hệ thống lưu giá từng đêm: giá chốt là đơn giá × số đêm.</summary>
    public bool CoGiaTungDem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTheoDem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongHienHanh { get; set; }
    public List<BookingNightPriceDto> TheoDem { get; set; } = new();
}

public class BookingPriceBreakdownDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;
    public DateOnly NgayDon { get; set; }
    public DateOnly NgayTra { get; set; }
    public int SoDem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTienPhong { get; set; }

    /// <summary>Quy tắc ghi nhận khi nào giá của phiếu được tính lại.</summary>
    public string QuyTacTinhLai { get; set; } = "";
    public List<BookingRoomPriceDto> Phong { get; set; } = new();
}

public class RateNightDto
{
    public DateOnly Dem { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal Gia { get; set; }

    /// <summary>Tên quy tắc được áp dụng cho đêm này (null = giá cơ bản của loại phòng).</summary>
    public string? QuyTac { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal GiaCoBan { get; set; }

    /// <summary>Cách điều chỉnh của quy tắc thắng, VD "+20%" hoặc "giá cố định 900.000đ" (null = giá cơ bản).</summary>
    public string? DieuChinh { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UuTien { get; set; }

    /// <summary>Mọi quy tắc đang bật khớp đêm này, quy tắc được áp dụng đứng đầu.</summary>
    public List<RateMatchedRuleDto> CacQuyTacKhop { get; set; } = new();
}

public class RateQuoteDto
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public string LoaiDatPhong { get; set; } = null!;

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaCoBan { get; set; }
    public int SoDem { get; set; }

    /// <summary>Đơn giá bình quân mỗi đêm (làm tròn 1.000đ khi có quy tắc giá) — chính là đơn giá ghi vào phiếu đặt/lượt lưu trú.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal DonGiaBinhQuan { get; set; }

    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongTien { get; set; }

    /// <summary>Tổng nếu mọi đêm tính giá cơ bản, để thấy phần chênh lệch do quy tắc.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))]
    public decimal TongCoBan { get; set; }
    public List<RateNightDto> TheoDem { get; set; } = new();
}
