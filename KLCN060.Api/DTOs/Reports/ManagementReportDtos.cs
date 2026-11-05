using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Reports;

// =====================================================================================================
// Bảng điều hành (Dashboard) của Quản lý
// =====================================================================================================

// =====================================================================================================
// Tổng quan theo kỳ + so sánh kỳ trước (Occupancy, ADR, RevPAR)
// =====================================================================================================

public class PeriodKpiDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int SoNgay { get; set; }
    public int TongSoPhong { get; set; }

    /// <summary>Doanh thu theo hóa đơn lập trong kỳ (tiền phải thu tại quầy + tiền đã cọc), chỉ hóa đơn đã thu được ít nhất một phần.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }

    /// <summary>Doanh thu phòng ghi nhận theo đêm ở (đơn giá áp dụng x số đêm), chưa trừ khuyến mãi.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuPhong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuDichVu { get; set; }

    /// <summary>Số đêm phòng đã bán (Rooms Sold) và số đêm phòng có thể bán (Available Room-nights = số phòng x số ngày).</summary>
    public int SoDemPhongBan { get; set; }
    public int SoDemPhongKhaDung { get; set; }

    /// <summary>Occupancy = đêm phòng bán ÷ đêm phòng khả dụng x 100.</summary>
    public decimal CongSuatPhanTram { get; set; }

    /// <summary>ADR = doanh thu phòng ÷ đêm phòng bán.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal Adr { get; set; }

    /// <summary>RevPAR = doanh thu phòng ÷ đêm phòng khả dụng (= ADR x Occupancy).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal RevPar { get; set; }

    public int SoDatPhongMoi { get; set; }
    public int SoHuy { get; set; }
    public int SoKhongDen { get; set; }
    public decimal TyLeHuyPhanTram { get; set; }
    public decimal TyLeKhongDenPhanTram { get; set; }
    public int SoCheckIn { get; set; }
    public int SoCheckOut { get; set; }
}

public class PeriodDeltaDto
{
    /// <summary>% thay đổi so với kỳ trước (null khi kỳ trước bằng 0).</summary>
    public decimal? DoanhThu { get; set; }
    public decimal? DoanhThuPhong { get; set; }
    public decimal? CongSuat { get; set; }
    public decimal? Adr { get; set; }
    public decimal? RevPar { get; set; }
    public decimal? SoDatPhongMoi { get; set; }
}

public class SeriesPointDto
{
    /// <summary>Ngày (hoặc ngày đầu tháng khi nhóm theo tháng).</summary>
    public DateOnly Ngay { get; set; }
    public string Nhan { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThuPhong { get; set; }
    public decimal CongSuatPhanTram { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal Adr { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal RevPar { get; set; }
    public int SoDatPhongMoi { get; set; }
}

public class OverviewReportDto
{
    /// <summary>Thời điểm máy chủ tính báo cáo (để biết số liệu mới đến đâu).</summary>
    public DateTime CapNhatLuc { get; set; }
    public PeriodKpiDto KyNay { get; set; } = new();
    public PeriodKpiDto KyTruoc { get; set; } = new();
    public PeriodDeltaDto ThayDoi { get; set; } = new();

    /// <summary>NGAY hoặc THANG.</summary>
    public string NhomTheo { get; set; } = "NGAY";
    public List<SeriesPointDto> ChuoiKyNay { get; set; } = new();
    public List<SeriesPointDto> ChuoiKyTruoc { get; set; } = new();
}

// =====================================================================================================
// Doanh thu chi tiết
// =====================================================================================================

public class RevenueSliceDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }

    /// <summary>Số lượng đi kèm (đêm phòng, lượt dịch vụ, số lần thanh toán... tùy nhóm).</summary>
    public int SoLuong { get; set; }
    public decimal TyTrongPhanTram { get; set; }
}

public class RevenueBreakdownDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongDoanhThu { get; set; }
    public List<RevenueDayDto> TheoNgay { get; set; } = new();
    public List<RevenueSliceDto> TheoThang { get; set; } = new();

    /// <summary>Doanh thu PHÒNG theo loại phòng (theo đêm ở).</summary>
    public List<RevenueSliceDto> TheoLoaiPhong { get; set; } = new();

    /// <summary>Doanh thu PHÒNG theo nguồn đặt: WEBSITE, QUAY_LE_TAN, VANG_LAI (không qua đặt phòng).</summary>
    public List<RevenueSliceDto> TheoNguonDat { get; set; } = new();

    /// <summary>Doanh thu DỊCH VỤ theo từng dịch vụ (theo ngày sử dụng).</summary>
    public List<RevenueSliceDto> TheoDichVu { get; set; } = new();

    /// <summary>Tiền thực thu theo hình thức (thời điểm thanh toán trong kỳ, đã trừ các lần điều chỉnh); chỉ gồm thanh toán hóa đơn khi trả phòng, chưa gồm tiền cọc.</summary>
    public List<RevenueSliceDto> TheoHinhThucThanhToan { get; set; } = new();

    /// <summary>Tiền thực thu theo hình thức GỒM cả tiền cọc đã nhận trong kỳ (số liệu kế toán dùng đối chiếu quỹ và ngân hàng).</summary>
    public List<RevenueSliceDto> TheoHinhThucGomCoc { get; set; } = new();

    /// <summary>Doanh thu theo loại: Phòng, Dịch vụ, Minibar, Giặt là, Khác (phụ thu + dịch vụ nhóm khác) và Giảm giá (số âm). Cộng lại bằng đúng TongDoanhThu.</summary>
    public List<RevenueSliceDto> TheoLoaiDoanhThu { get; set; } = new();

    /// <summary>Doanh thu theo tuần (thứ Hai - Chủ nhật), quý và năm của ngày lập hóa đơn.</summary>
    public List<RevenueSliceDto> TheoTuan { get; set; } = new();
    public List<RevenueSliceDto> TheoQuy { get; set; } = new();
    public List<RevenueSliceDto> TheoNam { get; set; } = new();

    /// <summary>Doanh thu PHÒNG theo từng phòng (theo đêm ở) và theo tầng.</summary>
    public List<RevenueSliceDto> TheoPhong { get; set; } = new();
    public List<RevenueSliceDto> TheoTang { get; set; } = new();
}

// =====================================================================================================
// Báo cáo vận hành các bộ phận
// =====================================================================================================

// =====================================================================================================
// Báo cáo đặt phòng (theo nguồn, trạng thái, hủy, không đến, đoàn)
// =====================================================================================================

public class BookingCountDto
{
    public string Ma { get; set; } = null!;
    public string Ten { get; set; } = null!;
    public int SoLuong { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal GiaTri { get; set; }
}

public class BookingReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public int TongSoPhieu { get; set; }
    public List<BookingCountDto> TheoTrangThai { get; set; } = new();
    public List<BookingCountDto> TheoNguon { get; set; } = new();
    public List<BookingCountDto> TheoLoai { get; set; } = new();
    public int SoPhieuDoan { get; set; }
    public int SoPhongDoan { get; set; }
    public decimal TyLeHuyPhanTram { get; set; }
    public decimal TyLeKhongDenPhanTram { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongCocHoan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongCocGiuLai { get; set; }

    /// <summary>Số phiếu đã quá ngày đón mà khách chưa nhận phòng (chờ đánh dấu không đến).</summary>
    public int ChoXuLyKhongDen { get; set; }
    public List<CancellationRowDto> HuyGanDay { get; set; } = new();
}

public class CancellationRowDto
{
    public string MaPhieuDat { get; set; } = null!;
    public string HoTenKhach { get; set; } = null!;
    public string TrangThai { get; set; } = null!;
    public DateTime? NgayHuy { get; set; }
    public string? LyDoHuy { get; set; }
    public string? HuyBoi { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TienCoc { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal SoTienHoanCoc { get; set; }
}

// =====================================================================================================
// Công nợ
// =====================================================================================================

public class DebtRowDto
{
    public string MaHD { get; set; } = null!;
    public string? HoTenKhach { get; set; }

    /// <summary>Phiếu đặt phòng của lượt ở (null với khách vãng lai không đặt trước) và mã khách: để kế toán theo dõi công nợ theo booking / theo khách.</summary>
    public string? MaPhieuDat { get; set; }
    public string? MaKhach { get; set; }
    public DateTime NgayLap { get; set; }
    public int SoNgayNo { get; set; }

    /// <summary>Quá hạn thanh toán theo quy tắc chung (còn nợ, khách đã trả phòng quá số ngày cấu hình) - cùng định nghĩa với tab Hóa đơn.</summary>
    public bool QuaHan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public string NhomTuoiNo { get; set; } = null!;
}

public class DebtGroupDto
{
    public string Ma { get; set; } = null!;
    public string? HoTenKhach { get; set; }
    public int SoHoaDon { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public int SoNgayNoLonNhat { get; set; }
}

public class DebtReportDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongConNo { get; set; }
    public int SoHoaDon { get; set; }
    public List<RevenueSliceDto> TheoNhomTuoiNo { get; set; } = new();
    public List<DebtRowDto> ChiTiet { get; set; } = new();

    /// <summary>Khách còn nợ (gộp các hóa đơn của cùng một khách), nợ nhiều nhất trước.</summary>
    public List<DebtGroupDto> TheoKhach { get; set; } = new();

    /// <summary>Phiếu đặt phòng còn công nợ (booking còn nợ), nợ nhiều nhất trước; không gồm lượt vãng lai không có phiếu đặt.</summary>
    public List<DebtGroupDto> TheoDatPhong { get; set; } = new();
}
