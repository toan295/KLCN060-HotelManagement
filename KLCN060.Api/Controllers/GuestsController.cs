using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Guests;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/guests")]
[Authorize(Roles = "LE_TAN,QUAN_LY")]
public class GuestsController : ControllerBase
{
    private readonly IGuestService _guestService;

    public GuestsController(IGuestService guestService)
    {
        _guestService = guestService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _guestService.SearchAsync(q, page, pageSize);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<GuestDto>>.Ok(items, meta));
    }

    [HttpGet("{maKhach}")]
    public async Task<IActionResult> GetById(string maKhach)
    {
        var result = await _guestService.GetDetailAsync(maKhach);
        return Ok(ApiResponse<GuestDetailDto>.Ok(result));
    }

    /// <summary>Tạo khách vãng lai để lễ tân đặt phòng/nhận phòng hộ (mở rộng của 6.8, không đổi schema).</summary>
    [HttpPost]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> Create([FromBody] GuestRequest request)
    {
        var result = await _guestService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<GuestDto>.Ok(result));
    }

    [HttpPut("{maKhach}")]
    public async Task<IActionResult> Update(string maKhach, [FromBody] GuestRequest request)
    {
        var result = await _guestService.UpdateAsync(maKhach, request);
        return Ok(ApiResponse<GuestDto>.Ok(result));
    }
}
