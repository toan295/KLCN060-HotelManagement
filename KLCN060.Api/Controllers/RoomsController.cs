using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Rooms;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet]
    [Authorize(Roles = "LE_TAN,BUONG_PHONG,QUAN_LY")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _roomService.GetAllAsync();
        return Ok(ApiResponse<List<RoomDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Create([FromBody] RoomRequest request)
    {
        var result = await _roomService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RoomDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Update(string id, [FromBody] RoomRequest request)
    {
        var result = await _roomService.UpdateAsync(id, request);
        return Ok(ApiResponse<RoomDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Delete(string id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "LE_TAN,BUONG_PHONG")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] RoomStatusRequest request)
    {
        var result = await _roomService.UpdateStatusAsync(id, request.TinhTrang, request.GhiChu, CurrentUser.From(User).TenDN);
        return Ok(ApiResponse<RoomDto>.Ok(result));
    }
}
