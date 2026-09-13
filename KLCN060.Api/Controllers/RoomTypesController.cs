using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.RoomTypes;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/room-types")]
public class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var result = await _roomTypeService.GetAllAsync();
        return Ok(ApiResponse<List<RoomTypeDto>>.Ok(result));
    }

    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability([FromQuery] DateOnly checkin, [FromQuery] DateOnly checkout, [FromQuery] int guests = 1)
    {
        var result = await _roomTypeService.GetAvailabilityAsync(checkin, checkout, guests);
        return Ok(ApiResponse<List<RoomTypeAvailabilityDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _roomTypeService.GetByIdAsync(id);
        return Ok(ApiResponse<RoomTypeDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Create([FromBody] RoomTypeRequest request)
    {
        var result = await _roomTypeService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RoomTypeDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Update(string id, [FromBody] RoomTypeRequest request)
    {
        var result = await _roomTypeService.UpdateAsync(id, request);
        return Ok(ApiResponse<RoomTypeDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Delete(string id)
    {
        await _roomTypeService.DeleteAsync(id);
        return NoContent();
    }
}
