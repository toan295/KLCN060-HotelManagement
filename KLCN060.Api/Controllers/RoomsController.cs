using KLCN060.Api.Authorization;
using KLCN060.Domain;
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
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _roomService.GetAllAsync();
        return Ok(ApiResponse<List<RoomDto>>.Ok(result));
    }

    /// <summary>Danh mục phòng cho Quản lý: thống kê theo tầng / loại / tình trạng, bộ lọc, và mức độ phát sinh dữ liệu của từng phòng (xóa / đổi loại được không).</summary>
    [HttpGet("catalog")]
    [HasPermission(Permissions.XemPhong, Permissions.QuanLyPhong)]
    public async Task<IActionResult> Catalog([FromQuery] RoomCatalogQuery query)
        => Ok(ApiResponse<RoomCatalogDto>.Ok(await _roomService.GetCatalogAsync(query)));

    /// <summary>Tạo nhiều phòng một lần (tất cả hoặc không phòng nào).</summary>
    [HttpPost("bulk")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> CreateBulk([FromBody] BulkRoomRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<BulkRoomResultDto>.Ok(await _roomService.CreateBulkAsync(request)));

    /// <summary>Lịch sử tình trạng của một phòng (ai đổi, từ gì sang gì, lúc nào, vì sao) - kể cả các lần đổi do nhận/trả/đổi phòng.</summary>
    [HttpGet("{id}/history")]
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> History(string id)
        => Ok(ApiResponse<List<RoomStatusHistoryDto>>.Ok(await _roomService.GetHistoryAsync(id)));

    /// <summary>Chi tiết một phòng: tình trạng + lý do + ai đổi, khách đang ở, đặt phòng sắp tới, sự cố mở, người đang dọn, việc tiếp theo.</summary>
    [HttpGet("{id}/detail")]
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> Detail(string id)
        => Ok(ApiResponse<RoomDetailDto>.Ok(await _roomService.GetDetailAsync(id)));

    [HttpPost]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Create([FromBody] RoomRequest request)
    {
        var result = await _roomService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<RoomDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Update(string id, [FromBody] RoomRequest request)
    {
        var result = await _roomService.UpdateAsync(id, request, CurrentUser.From(User).TenDN);
        return Ok(ApiResponse<RoomDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Delete(string id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    [HasPermission(Permissions.DoiTinhTrangPhong)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] RoomStatusRequest request)
    {
        var result = await _roomService.UpdateStatusAsync(id, request.TinhTrang, request.GhiChu, CurrentUser.From(User).TenDN, request.TinhTrangCu);
        return Ok(ApiResponse<RoomDto>.Ok(result));
    }
}
