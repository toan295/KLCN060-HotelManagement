using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Facilities;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Authorize]
public class FacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilityService;

    public FacilitiesController(IFacilityService facilityService)
    {
        _facilityService = facilityService;
    }

    [HttpGet("api/v1/rooms/{roomId}/facilities")]
    [Authorize(Roles = "LE_TAN,BUONG_PHONG,QUAN_LY")]
    public async Task<IActionResult> GetByRoom(string roomId)
    {
        var result = await _facilityService.GetByRoomAsync(roomId);
        return Ok(ApiResponse<List<FacilityDto>>.Ok(result));
    }

    [HttpPost("api/v1/rooms/{roomId}/facilities")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> CreateForRoom(string roomId, [FromBody] FacilityRequest request)
    {
        var result = await _facilityService.CreateForRoomAsync(roomId, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FacilityDto>.Ok(result));
    }

    [HttpPut("api/v1/facilities/{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Update(string id, [FromBody] FacilityRequest request)
    {
        var result = await _facilityService.UpdateAsync(id, request);
        return Ok(ApiResponse<FacilityDto>.Ok(result));
    }

    [HttpDelete("api/v1/facilities/{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Delete(string id)
    {
        await _facilityService.DeleteAsync(id);
        return NoContent();
    }
}
