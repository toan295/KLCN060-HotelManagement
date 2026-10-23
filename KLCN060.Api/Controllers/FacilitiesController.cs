using KLCN060.Api.Authorization;
using KLCN060.Domain;
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
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> GetByRoom(string roomId)
    {
        var result = await _facilityService.GetByRoomAsync(roomId);
        return Ok(ApiResponse<List<FacilityDto>>.Ok(result));
    }

    [HttpPost("api/v1/rooms/{roomId}/facilities")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> CreateForRoom(string roomId, [FromBody] FacilityRequest request)
    {
        var result = await _facilityService.CreateForRoomAsync(roomId, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<FacilityDto>.Ok(result));
    }

    /// <summary>Tổng quan cho quản lý: số thiết bị theo tình trạng, phòng có thiết bị hỏng, thiết bị hỏng quá hạn, kho; kèm toàn bộ danh sách thiết bị.</summary>
    [HttpGet("api/v1/facilities/overview")]
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> Overview()
        => Ok(ApiResponse<FacilityOverviewDto>.Ok(await _facilityService.GetOverviewAsync()));

    [HttpGet("api/v1/facilities/{id}/history")]
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> History(string id)
        => Ok(ApiResponse<List<FacilityHistoryEntryDto>>.Ok(await _facilityService.GetHistoryAsync(id)));

    /// <summary>Thêm thiết bị vào phòng (<c>maPhong</c>) hoặc vào kho/dùng chung (bỏ trống).</summary>
    [HttpPost("api/v1/facilities")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Create([FromBody] FacilityRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<FacilityDto>.Ok(await _facilityService.CreateAsync(request)));

    [HttpPost("api/v1/facilities/{id}/transfer")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Transfer(string id, [FromBody] FacilityTransferRequest request)
        => Ok(ApiResponse<FacilityDto>.Ok(await _facilityService.TransferAsync(id, request)));

    [HttpPut("api/v1/facilities/{id}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Update(string id, [FromBody] FacilityRequest request)
    {
        var result = await _facilityService.UpdateAsync(id, request);
        return Ok(ApiResponse<FacilityDto>.Ok(result));
    }

    [HttpDelete("api/v1/facilities/{id}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Delete(string id, [FromQuery] string? lyDo = null)
    {
        await _facilityService.DeleteAsync(id, lyDo);
        return NoContent();
    }
}
