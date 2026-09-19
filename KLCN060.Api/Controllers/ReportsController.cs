using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "KE_TOAN,QUAN_LY")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
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
}

[ApiController]
[Route("api/v1/shift-handovers")]
[Authorize(Roles = "LE_TAN,QUAN_LY")]
public class ShiftHandoversController : ControllerBase
{
    private readonly IShiftHandoverService _service;

    public ShiftHandoversController(IShiftHandoverService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> Create([FromBody] ShiftHandoverRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<ShiftHandoverDto>.Ok(await _service.CreateAsync(request, CurrentUser.From(User))));

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<List<ShiftHandoverDto>>.Ok(await _service.GetAsync(tuNgay, denNgay)));
}
