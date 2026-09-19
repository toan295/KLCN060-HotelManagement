using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICheckInService _checkInService;

    public BookingsController(IBookingService bookingService, ICheckInService checkInService)
    {
        _bookingService = bookingService;
        _checkInService = checkInService;
    }

    [HttpPost]
    [Authorize(Roles = "KHACH_HANG,LE_TAN")]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        var result = await _bookingService.CreateAsync(request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BookingDto>.Ok(result));
    }

    [HttpGet]
    [Authorize(Roles = "LE_TAN,QUAN_LY")]
    public async Task<IActionResult> Search([FromQuery] BookingListQuery query)
    {
        var (items, total) = await _bookingService.SearchAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<BookingSummaryDto>>.Ok(items, meta));
    }

    [HttpGet("me")]
    [Authorize(Roles = "KHACH_HANG")]
    public async Task<IActionResult> GetMine()
    {
        var result = await _bookingService.GetMineAsync(CurrentUser.From(User).MaKhach);
        return Ok(ApiResponse<List<BookingSummaryDto>>.Ok(result));
    }

    [HttpGet("{maPhieuDat}")]
    [Authorize(Roles = "KHACH_HANG,LE_TAN,QUAN_LY")]
    public async Task<IActionResult> GetById(string maPhieuDat)
    {
        var result = await _bookingService.GetByIdAsync(maPhieuDat, CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    [HttpPost("{maPhieuDat}/confirm-deposit")]
    [Authorize(Roles = "KHACH_HANG,LE_TAN")]
    public async Task<IActionResult> ConfirmDeposit(string maPhieuDat)
    {
        var result = await _bookingService.ConfirmDepositAsync(maPhieuDat, CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    [HttpPost("{maPhieuDat}/cancel")]
    [Authorize(Roles = "KHACH_HANG,LE_TAN")]
    public async Task<IActionResult> Cancel(string maPhieuDat, [FromBody] CancelBookingRequest? request)
    {
        var result = await _bookingService.CancelAsync(maPhieuDat, request ?? new CancelBookingRequest(), CurrentUser.From(User));
        return Ok(ApiResponse<CancelBookingResultDto>.Ok(result));
    }

    [HttpPost("{maPhieuDat}/check-in")]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> CheckIn(string maPhieuDat, [FromBody] BookingCheckInRequest request)
    {
        var result = await _checkInService.CheckInBookingAsync(maPhieuDat, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CheckInResultDto>.Ok(result));
    }
}
