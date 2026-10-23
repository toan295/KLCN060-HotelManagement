using System.Text.Json.Serialization;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.DTOs.Reports;

// =====================================================================================================
// Bảng điều hành (Dashboard) của Quản lý
// =====================================================================================================

public class DashboardDto
{
    public DateTime ThoiDiem { get; set; }
    public DashboardPhongDto Phong { get; set; } = new();
    public DashboardHomNayDto HomNay { get; set; } = new();
    public DashboardDoanhThuDto DoanhThu { get; set; } = new();
    public DashboardCongNoDto CongNo { get; set; } = new();

    public DashboardCanhBaoDto CanhBao { get; set; } = new();
    public List<DashboardTangDto> TheoTang { get; set; } = new();

    /// <summary>Số liệu hôm qua để có ngữ cảnh (doanh thu, công suất, ADR).</summary>
    public DashboardHomQuaDto HomQua { get; set; } = new();

    /// <summary>Việc cần làm trong ngày: khách đến chưa nhận phòng, khách trả chưa trả phòng, phòng giữ chưa sẵn sàng.</summary>
    public DashboardViecHomNayDto ViecHomNay { get; set; } = new();

    /// <summary>Dự báo 7 ngày tới từ các phiếu đặt đang giữ phòng.</summary>
    public List<DashboardDuBaoDto> DuBao { get; set; } = new();

    /// <summary>Đối chiếu nhất quán giữa các số trên bảng: trạng thái phòng, công suất, ADR, RevPAR, doanh thu, công nợ.</summary>
    public DashboardDoiChieuDto DoiChieu { get; set; } = new();
}

public class DashboardHomQuaDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }
    public decimal CongSuatPhanTram { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal Adr { get; set; }
    public int SoDemPhongBan { get; set; }
}

public class DashboardViecDto
{
    public string Ma { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string ChiTiet { get; set; } = null!;

    /// <summary>true = có vấn đề (phòng chưa sẵn sàng, quá giờ...) cần chú ý.</summary>
    public bool CanChuY { get; set; }
    public string ManHinh { get; set; } = null!;
}

public class DashboardViecHomNayDto
{
    public List<DashboardViecDto> KhachDenChuaNhan { get; set; } = new();
    public int TongKhachDenChuaNhan { get; set; }
    public List<DashboardViecDto> KhachTraChuaTra { get; set; } = new();
    public int TongKhachTraChuaTra { get; set; }
}

public class DashboardDuBaoDto
{
    public DateOnly Ngay { get; set; }
    public int PhongDaGiu { get; set; }
    public int KhachDen { get; set; }
    public int KhachTra { get; set; }
    public decimal CongSuatDuKienPhanTram { get; set; }
}

public class DashboardDoiChieuDto
{
    public int TongSoPhep { get; set; }
    public int SoKhop { get; set; }
    public bool TatCaKhop => SoKhop == TongSoPhep;
    public List<ReportCheckDto> Phep { get; set; } = new();
}

public class DashboardPhongDto
{
    public int TongSoPhong { get; set; }

    /// <summary>Phòng đang có khách (OC + OD).</summary>
    public int DangCoKhach { get; set; }

    /// <summary>Phòng trống sẵn sàng bán (VC + VI).</summary>
    public int Trong { get; set; }

    /// <summary>Phòng cần/đang dọn: trống chưa dọn (VD) + có khách cần dọn (OD).</summary>
    public int DangDon { get; set; }

    /// <summary>Phòng bảo trì / ngừng phục vụ (OOO).</summary>
    public int BaoTri { get; set; }

    /// <summary>Số phòng theo từng tình trạng: VC, VD, VI, OC, OD, OOO.</summary>
    public Dictionary<string, int> TheoTinhTrang { get; set; } = new();

    /// <summary>Công suất tức thời = phòng có khách ÷ tổng số phòng.</summary>
    public decimal CongSuatPhanTram { get; set; }
}

public class DashboardHomNayDto
{
    /// <summary>Phiếu đặt đến hôm nay (dự kiến) và số phiếu đã nhận phòng.</summary>
    public int CheckInDuKien { get; set; }
    public int CheckInDaLam { get; set; }
    public int CheckOutDuKien { get; set; }
    public int CheckOutDaLam { get; set; }
    public int BookingMoi { get; set; }
    public int BookingHuy { get; set; }
}

public class DashboardDoanhThuDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal HomNay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThangNay { get; set; }

    /// <summary>Cùng kỳ (từ đầu tháng trước đến cùng ngày) của tháng trước để so sánh.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal CungKyThangTruoc { get; set; }
    public decimal? ThayDoiSoVoiThangTruocPhanTram { get; set; }

    /// <summary>Công suất, ADR, RevPAR của tháng này (từ đầu tháng đến hôm nay).</summary>
    public decimal CongSuatThangPhanTram { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal AdrThang { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal RevParThang { get; set; }
    public decimal CongSuatHomNayPhanTram { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal AdrHomNay { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal RevParHomNay { get; set; }
}

public class DashboardCongNoDto
{
    public int SoHoaDonConNo { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongConNo { get; set; }
    public int SoHoaDonQuaHan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongQuaHan { get; set; }
}

public class DashboardCanhBaoDto
{
    public int NghiemTrong { get; set; }
    public int CanhBao { get; set; }
    public int ThongTin { get; set; }
    public int Tong => NghiemTrong + CanhBao + ThongTin;
}

public class DashboardTangDto
{
    public int Tang { get; set; }
    public int TongSoPhong { get; set; }
    public int DangCoKhach { get; set; }
    public int Trong { get; set; }
    public int DangDon { get; set; }
    public int BaoTri { get; set; }
}

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

public class OperationsReportDto
{
    public DateOnly TuNgay { get; set; }
    public DateOnly DenNgay { get; set; }
    public FrontOfficeReportDto LeTan { get; set; } = new();
    public HousekeepingReportDto BuongPhong { get; set; } = new();
    public AccountingReportDto KeToan { get; set; } = new();
    public MaintenanceReportDto BaoTri { get; set; } = new();
}

public class FrontOfficeReportDto
{
    public int CheckIn { get; set; }
    public int CheckOut { get; set; }
    public int NoShow { get; set; }
    public int Huy { get; set; }
    public int KhachVangLai { get; set; }
    public int DatPhongMoi { get; set; }
    public decimal CongSuatTrungBinhPhanTram { get; set; }
}

public class HousekeepingReportDto
{
    /// <summary>Số lần phòng được dọn xong (từ VD sang VC/VI) trong kỳ.</summary>
    public int PhongDaDon { get; set; }
    public int PhongDangCho { get; set; }
    public int PhongCoVanDe { get; set; }
    public List<RoomIssueDto> DanhSachCanXuLy { get; set; } = new();
}

public class RoomIssueDto
{
    public string MaPhong { get; set; } = null!;
    public string TinhTrang { get; set; } = null!;
    public DateTime? TuLuc { get; set; }
    public double? SoGio { get; set; }
    public string? LyDo { get; set; }
}

public class AccountingReportDto
{
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DoanhThu { get; set; }

    /// <summary>Tiền thực thu trong kỳ (đã trừ điều chỉnh).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ThanhToan { get; set; }
    public int SoLanThanhToan { get; set; }

    /// <summary>Công nợ hiện tại (toàn bộ hóa đơn còn nợ, không phụ thuộc kỳ).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal CongNoHienTai { get; set; }
    public int SoHoaDonConNo { get; set; }

    /// <summary>Cọc đã hoàn cho khách trong kỳ (các phiếu hủy được hoàn cọc).</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal HoanTien { get; set; }
    public int SoLanHoanTien { get; set; }

    /// <summary>Cọc bị giữ lại do khách hủy muộn / không đến trong kỳ.</summary>
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal CocGiuLai { get; set; }
}

public class MaintenanceReportDto
{
    /// <summary>Số lần đưa phòng vào bảo trì (sang OOO) trong kỳ.</summary>
    public int SoSuCo { get; set; }
    public int DangXuLy { get; set; }

    /// <summary>Số sự cố đã xử lý xong (phòng ra khỏi OOO) trong kỳ và thời gian xử lý trung bình (giờ).</summary>
    public int DaXuLy { get; set; }
    public double? ThoiGianXuLyTrungBinhGio { get; set; }
    public List<RoomIssueDto> PhongDangBaoTri { get; set; } = new();
}

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
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal TongTien { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal DaThanhToan { get; set; }
    [JsonConverter(typeof(VndMoneyJsonConverter))] public decimal ConNo { get; set; }
    public string NhomTuoiNo { get; set; } = null!;
}

/// <summary>Công nợ gộp theo khách hoặc theo phiếu đặt phòng.</summary>
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

// =====================================================================================================
// Cảnh báo
// =====================================================================================================

public class AlertDto
{
    /// <summary>Mã quy tắc cảnh báo (VD BAO_TRI_QUA_LAU).</summary>
    public string Ma { get; set; } = null!;

    /// <summary>NGHIEM_TRONG, CANH_BAO hoặc THONG_TIN.</summary>
    public string MucDo { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string MoTa { get; set; } = null!;
    public int SoLuong { get; set; }

    /// <summary>Nhóm màn hình liên quan để điều hướng: PHONG, DAT_PHONG, HOA_DON, DOI_SOAT, SAO_LUU, NHAN_VIEN, LUU_TRU.</summary>
    public string ManHinh { get; set; } = null!;

    /// <summary>Tối đa 10 đối tượng cụ thể, mỗi dòng một câu ngắn.</summary>
    public List<string> ChiTiet { get; set; } = new();

    /// <summary>Ảnh hưởng nếu để nguyên (tiền, khách, khả năng bán phòng...).</summary>
    public string AnhHuong { get; set; } = "";

    /// <summary>Bộ phận / vai trò thường xử lý (khi chưa có người nhận cụ thể).</summary>
    public string NguoiXuLyMacDinh { get; set; } = "";

    /// <summary>Các bước xử lý gợi ý.</summary>
    public string CachXuLy { get; set; } = "";

    /// <summary>Nhãn nút điều hướng tới nơi xử lý (VD "Mở Buồng phòng để giao việc dọn").</summary>
    public string HanhDongDeXuat { get; set; } = "";

    /// <summary>Hạn xử lý: do người nhận đặt, hoặc mặc định theo mức độ / loại cảnh báo tính từ lúc phát hiện.</summary>
    public DateTime? HanXuLy { get; set; }
    public bool QuaHan { get; set; }

    /// <summary>Lúc hệ thống phát hiện cảnh báo (bắt đầu đợt hiện tại).</summary>
    public DateTime? PhatHienLuc { get; set; }

    /// <summary>CHUA_NHAN (chưa ai nhận xử lý) hoặc DANG_XU_LY (đã có người nhận).</summary>
    public string TrangThaiXuLy { get; set; } = "CHUA_NHAN";
    public string? NguoiNhan { get; set; }
    public string? HoTenNguoiNhan { get; set; }
    public DateTime? NhanLuc { get; set; }
    public string? GhiChuNhan { get; set; }
}

public class AlertResolvedDto
{
    public string Ma { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string MucDo { get; set; } = null!;
    public DateTime PhatHienLuc { get; set; }
    public DateTime DaXuLyLuc { get; set; }
    public double SoGioXuLy { get; set; }
    public string? NguoiNhan { get; set; }
    public string? HoTenNguoiNhan { get; set; }
}

public class AssignAlertRequest
{
    /// <summary>Tên đăng nhập người nhận xử lý; bỏ trống = chính người đang thao tác.</summary>
    public string? NguoiXuLy { get; set; }

    /// <summary>Hạn xử lý; bỏ trống = giữ hạn mặc định.</summary>
    public DateTime? HanXuLy { get; set; }
    public string? GhiChu { get; set; }
}

public class AlertReportDto
{
    public DateTime ThoiDiem { get; set; }
    public int NghiemTrong { get; set; }
    public int CanhBao { get; set; }
    public int ThongTin { get; set; }
    public List<AlertDto> CanhBaoDangCo { get; set; } = new();

    /// <summary>Cảnh báo đã hết trong 48 giờ qua (nguyên nhân đã được xử lý) - để trả lời "đã xong chưa".</summary>
    public List<AlertResolvedDto> DaXuLyGanDay { get; set; } = new();
    public int QuaHan { get; set; }
    public int ChuaNhan { get; set; }
}
