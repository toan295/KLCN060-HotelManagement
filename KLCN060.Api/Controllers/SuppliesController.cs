using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Danh mục đồ dùng trong phòng (khăn, ga, gối, nước uống, minibar, amenities...): buồng phòng xem để kiểm phòng, quản lý thêm/sửa.</summary>
[ApiController]
[Route("api/v1/supplies")]
[Authorize]
public class SuppliesController : ControllerBase
{
    private readonly ISupplyService _supplies;

    public SuppliesController(ISupplyService supplies)
    {
        _supplies = supplies;
    }

    [HttpGet]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.QuanLyPhong)]
    public async Task<IActionResult> GetAll([FromQuery] bool chiHoatDong = true)
        => Ok(ApiResponse<List<SupplyDto>>.Ok(await _supplies.GetAllAsync(chiHoatDong)));

    /// <summary>Các loại phòng (kèm số giường, số người tiêu chuẩn) để chọn phạm vi áp dụng của đồ dùng.</summary>
    [HttpGet("room-types")]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.QuanLyPhong)]
    public async Task<IActionResult> GetRoomTypes()
        => Ok(ApiResponse<List<SupplyRoomTypeDto>>.Ok(await _supplies.GetRoomTypesAsync()));

    [HttpPost]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Create([FromBody] SupplyRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<SupplyDto>.Ok(await _supplies.CreateAsync(request)));

    [HttpPut("{maVatDung}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Update(string maVatDung, [FromBody] SupplyRequest request)
        => Ok(ApiResponse<SupplyDto>.Ok(await _supplies.UpdateAsync(maVatDung, request)));

    [HttpDelete("{maVatDung}")]
    [HasPermission(Permissions.QuanLyPhong)]
    public async Task<IActionResult> Delete(string maVatDung)
    {
        await _supplies.DeleteAsync(maVatDung);
        return NoContent();
    }
}
