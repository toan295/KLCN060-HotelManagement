using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Báo cáo thống kê: doanh thu, công suất phòng, khách hàng, đặt phòng, công nợ và kết xuất báo cáo ra Excel / PDF.</summary>
[ApiController]
[Route("api/v1/reports")]
[HasPermission(Permissions.XemBaoCao)]
[ServiceFilter(typeof(KLCN060.Api.Middlewares.KhoangNgayBaoCaoFilter))]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IManagementReportService _management;
    private readonly KLCN060.Api.Services.Finance.IManagementReportExporter _exporter;

    public ReportsController(IReportService reportService, IManagementReportService management, KLCN060.Api.Services.Finance.IManagementReportExporter exporter)
    {
        _exporter = exporter;
        _reportService = reportService;
        _management = management;
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

    /// <summary>KPI của kỳ [from, to] so với kỳ liền trước cùng độ dài + chuỗi theo ngày/tháng để vẽ biểu đồ (nhomTheo = NGAY | THANG).</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? nhomTheo)
        => Ok(ApiResponse<OverviewReportDto>.Ok(await _management.GetOverviewAsync(from, to, nhomTheo)));

    /// <summary>Doanh thu theo ngày, tháng, loại phòng, nguồn đặt, dịch vụ và hình thức thanh toán.</summary>
    [HttpGet("revenue-breakdown")]
    public async Task<IActionResult> RevenueBreakdown([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<RevenueBreakdownDto>.Ok(await _management.GetRevenueBreakdownAsync(from, to)));

    /// <summary>Đặt phòng theo trạng thái, nguồn, loại (đoàn), tỷ lệ hủy / không đến, hoàn và giữ cọc.</summary>
    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(ApiResponse<BookingReportDto>.Ok(await _management.GetBookingReportAsync(from, to)));

    /// <summary>Công nợ: hóa đơn còn nợ phân theo tuổi nợ.</summary>
    [HttpGet("debts")]
    public async Task<IActionResult> Debts()
        => Ok(ApiResponse<DebtReportDto>.Ok(await _management.GetDebtsAsync()));

    /// <summary>Kết xuất báo cáo của kỳ [from, to] ra Excel (xlsx, nhiều trang tính) hoặc PDF.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? dinhDang)
    {
        var (noi, tenFile, loai) = await _exporter.XuatAsync(from, to, dinhDang, CurrentUser.From(User));
        return File(noi, loai, tenFile);
    }
}
