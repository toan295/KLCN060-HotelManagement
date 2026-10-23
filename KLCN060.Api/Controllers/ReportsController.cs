using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[HasPermission(Permissions.XemBaoCao)]
[ServiceFilter(typeof(KLCN060.Api.Middlewares.KhoangNgayBaoCaoFilter))]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IManagementReportService _management;
    private readonly IAlertService _alerts;
    private readonly KLCN060.Api.Services.Finance.IManagementReportExporter _exporter;

    public ReportsController(IReportService reportService, IManagementReportService management, IAlertService alerts, KLCN060.Api.Services.Finance.IManagementReportExporter exporter)
    {
        _exporter = exporter;
        _reportService = reportService;
        _management = management;
        _alerts = alerts;
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<RevenueReportDto>.Ok(await _reportService.RevenueAsync(from, to)));

    [HttpGet("occupancy")]
    public async Task<IActionResult> Occupancy([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<OccupancyReportDto>.Ok(await _reportService.OccupancyAsync(from, to)));

    [HttpGet("guests")]
    public async Task<IActionResult> Guests([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<GuestReportDto>.Ok(await _reportService.GuestsAsync(from, to)));

    /// <summary>Bảng điều hành: phòng theo tình trạng/tầng, check-in/out và booking hôm nay, doanh thu, công suất, ADR, RevPAR, công nợ, cảnh báo.</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var dto = await _management.GetDashboardAsync();
        // Danh sách khách cụ thể (họ tên, mã phiếu) chỉ dành cho người có quyền xem đặt phòng/khách; còn lại chỉ thấy số tổng hợp.
        var xemDatPhong = await CoQuyenAsync(Permissions.XemDatPhong, Permissions.XemKhachHang);
        if (!xemDatPhong) dto.ViecHomNay.KhachDenChuaNhan = new();
        if (!xemDatPhong && !await CoQuyenAsync(Permissions.XemLuuTru)) dto.ViecHomNay.KhachTraChuaTra = new();
        // Số cảnh báo trên bảng điều hành phải khớp danh sách cảnh báo mà người này được thấy.
        var canhBao = await _alerts.GetAlertsAsync();
        await LocCanhBaoTheoQuyenAsync(canhBao);
        dto.CanhBao = new DashboardCanhBaoDto { NghiemTrong = canhBao.NghiemTrong, CanhBao = canhBao.CanhBao, ThongTin = canhBao.ThongTin };
        return Ok(ApiResponse<DashboardDto>.Ok(dto));
    }

    private async Task<bool> CoQuyenAsync(params string[] quyen)
        => await HttpContext.RequestServices.GetRequiredService<IPermissionService>().CoQuyenAsync(CurrentUser.From(User).TenDN, quyen);

    /// <summary>KPI của kỳ [from, to] so với kỳ liền trước cùng độ dài + chuỗi theo ngày/tháng để vẽ biểu đồ (nhomTheo = NGAY | THANG).</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? nhomTheo)
        => Ok(ApiResponse<OverviewReportDto>.Ok(await _management.GetOverviewAsync(from, to, nhomTheo)));

    /// <summary>Doanh thu theo ngày, tháng, loại phòng, nguồn đặt, dịch vụ và hình thức thanh toán.</summary>
    [HttpGet("revenue-breakdown")]
    public async Task<IActionResult> RevenueBreakdown([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<RevenueBreakdownDto>.Ok(await _management.GetRevenueBreakdownAsync(from, to)));

    /// <summary>Báo cáo vận hành tổng hợp các bộ phận: lễ tân, buồng phòng, kế toán, bảo trì.</summary>
    [HttpGet("operations")]
    public async Task<IActionResult> Operations([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var dto = await _management.GetOperationsAsync(from, to);
        return Ok(ApiResponse<OperationsReportDto>.Ok(dto));
    }

    /// <summary>Đặt phòng theo trạng thái, nguồn, loại (đoàn), tỷ lệ hủy / không đến, hoàn và giữ cọc.</summary>
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<BookingReportDto>.Ok(await _management.GetBookingReportAsync(from, to)));

    /// <summary>Xuất báo cáo quản trị của kỳ [from, to] ra Excel (xlsx, nhiều trang tính) hoặc PDF: tổng quan, doanh thu, vận hành, đặt phòng, công nợ, vấn đề cần chú ý, đối chiếu. Việc xuất được ghi nhật ký.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? dinhDang)
    {
        var (noi, tenFile, loai) = await _exporter.XuatAsync(from, to, dinhDang, CurrentUser.From(User));
        return File(noi, loai, tenFile);
    }

    /// <summary>Từ điển chỉ số: định nghĩa, công thức, nguồn dữ liệu, quy ước trường hợp đặc biệt.</summary>
    [HttpGet("definitions")]
    public IActionResult Definitions() => Ok(ApiResponse<List<ReportDefinitionDto>>.Ok(_management.GetDefinitions()));

    /// <summary>Đối chiếu doanh thu hóa đơn với doanh thu phòng theo đêm + các phép kiểm tra tổng = chi tiết.</summary>
    [HttpGet("reconciliation")]
    public async Task<IActionResult> Reconciliation([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<ReportReconciliationDto>.Ok(await _management.GetReconciliationAsync(from, to)));

    /// <summary>Vấn đề cần chú ý (công suất thấp, booking chưa xác nhận, phòng chưa sẵn sàng, bảo trì quá hạn, hủy / không đến, công nợ quá hạn...) kèm gợi ý hành động.</summary>
    [HttpGet("insights")]
    public async Task<IActionResult> Insights([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<ReportInsightsDto>.Ok(await _management.GetInsightsAsync(from, to)));

    /// <summary>Truy vết: các dòng dữ liệu tạo nên một chỉ số (loai = HOA_DON | DEM_PHONG | DAT_PHONG | HUY_KHONG_DEN | CONG_NO; khoa tùy chọn, VD loai:LP01, nguon:WEBSITE, tang:2, ngay:2026-09-10).</summary>
    [HttpGet("drilldown")]
    public async Task<IActionResult> Drilldown([FromQuery] string loai, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? khoa)
        => Ok(ApiResponse<ReportDrillDto>.Ok(await _management.GetDrilldownAsync(loai, from, to, khoa)));

    /// <summary>Công nợ: hóa đơn còn nợ phân theo tuổi nợ.</summary>
    [HttpGet("debts")]
    public async Task<IActionResult> Debts()
        => Ok(ApiResponse<DebtReportDto>.Ok(await _management.GetDebtsAsync()));

    /// <summary>Cảnh báo đang có (phòng bảo trì lâu, chưa dọn, booking VIP, công nợ quá hạn, hoàn cọc lớn, tỷ lệ hủy tăng, overbooking, đối soát lỗi, sao lưu...).</summary>
    /// <summary>Giao một cảnh báo cho người xử lý và đặt hạn. Cần quyền duyệt nghiệp vụ (Quản lý).</summary>
    [HttpPost("alerts/{ma}/assign")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> AssignAlert(string ma, [FromBody] AssignAlertRequest request)
        => Ok(ApiResponse<AlertDto>.Ok(await _alerts.AssignAsync(ma, request ?? new AssignAlertRequest(), CurrentUser.From(User))));

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var dto = await _alerts.GetAlertsAsync();
        await LocCanhBaoTheoQuyenAsync(dto);
        return Ok(ApiResponse<AlertReportDto>.Ok(dto));
    }

    /// <summary>
    /// Cảnh báo chứa tên khách / tài khoản nhân viên: chỉ hiện cho người có quyền tương ứng (số liệu tổng hợp cập nhật theo danh sách còn lại).
    /// Đối soát chuyển khoản (tiền ngân hàng về, mã QR hết hạn) chỉ thuộc Kế toán: Quản lý không thấy các cảnh báo này.
    /// </summary>
    private async Task LocCanhBaoTheoQuyenAsync(AlertReportDto dto)
    {
        var xemDatPhong = await CoQuyenAsync(Permissions.XemDatPhong, Permissions.XemKhachHang);
        var xemNhanSu = await CoQuyenAsync(Permissions.QuanTriHeThong);
        var xemDoiSoat = await CoQuyenAsync(Permissions.XemDoiSoat);
        var an = dto.CanhBaoDangCo.Where(a => (a.ManHinh == "DAT_PHONG" && !xemDatPhong) || (a.ManHinh == "NHAN_VIEN" && !xemNhanSu)
            || (a.Ma is "DOI_SOAT_LOI" or "CHUYEN_KHOAN_HET_HAN" && !xemDoiSoat)).ToList();
        if (an.Count > 0)
        {
            foreach (var a in an) dto.CanhBaoDangCo.Remove(a);
            dto.NghiemTrong = dto.CanhBaoDangCo.Count(a => a.MucDo == "NGHIEM_TRONG");
            dto.CanhBao = dto.CanhBaoDangCo.Count(a => a.MucDo == "CANH_BAO");
            dto.ThongTin = dto.CanhBaoDangCo.Count(a => a.MucDo == "THONG_TIN");
        }
    }
}
