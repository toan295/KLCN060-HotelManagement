using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Media;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Ảnh phòng do quản lý tải lên. File được phục vụ công khai tại /media/rooms/{tenFile} (static files).</summary>
[ApiController]
[Route("api/v1/media/rooms")]
public class MediaController : ControllerBase
{
    private readonly IRoomImageService _roomImageService;

    public MediaController(IRoomImageService roomImageService)
    {
        _roomImageService = roomImageService;
    }

    /// <summary>Danh sách ảnh hiện có (công khai - Website dùng để biết loại phòng nào đã có ảnh).</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetAll()
        => Ok(ApiResponse<List<RoomImageDto>>.Ok(_roomImageService.GetAll()));

    /// <summary>Tải ảnh lên (multipart/form-data, field "file"); thay thế ảnh cũ của vị trí đó nếu có.</summary>
    [HttpPut("{slot}")]
    [HasPermission(Permissions.QuanLyPhong)]
    [Consumes("multipart/form-data")]
    // Giới hạn tầng truyền rộng hơn giới hạn nghiệp vụ (5 MB) để ảnh 5-20 MB nhận thông báo lỗi rõ ràng thay vì bị ngắt kết nối.
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(string slot, IFormFile? file)
        => Ok(ApiResponse<RoomImageDto>.Ok(await _roomImageService.SaveAsync(slot, file)));

    [HttpDelete("{slot}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Delete(string slot)
    {
        await _roomImageService.DeleteAsync(slot);
        return NoContent();
    }
}
