using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Nhận phòng cho khách vãng lai (walk-in), không có phiếu đặt trước.</summary>
[ApiController]
[Route("api/v1/check-ins")]
[Authorize(Roles = "LE_TAN")]
public class CheckInsController : ControllerBase
{
    private readonly ICheckInService _checkInService;

    public CheckInsController(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    [HttpPost]
    public async Task<IActionResult> WalkIn([FromBody] WalkInCheckInRequest request)
    {
        var result = await _checkInService.WalkInAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CheckInResultDto>.Ok(result));
    }
}
