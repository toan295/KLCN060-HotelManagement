using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Promotions;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/promotions")]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var result = await _promotionService.GetActiveAsync();
        return Ok(ApiResponse<List<PromotionDto>>.Ok(result));
    }

    [HttpGet]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _promotionService.GetAllAsync();
        return Ok(ApiResponse<List<PromotionDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> GetById(string id)
        => Ok(ApiResponse<PromotionDto>.Ok(await _promotionService.GetByIdAsync(id)));

    /// <summary>Các đặt phòng đã dùng khuyến mãi này (ai, lúc nào, giảm bao nhiêu).</summary>
    [HttpGet("{id}/usage")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Usage(string id)
        => Ok(ApiResponse<List<PromotionUsageDto>>.Ok(await _promotionService.GetUsageAsync(id)));

    /// <summary>Giải thích vì sao một đặt phòng được / không được hưởng khuyến mãi này (từng điều kiện đạt hay không).</summary>
    [HttpGet("{id}/check")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Check(string id, [FromQuery] string maPhieuDat)
        => Ok(ApiResponse<PromotionCheckDto>.Ok(await _promotionService.KiemTraBookingAsync(id, maPhieuDat)));

    [HttpPost("{id}/pause")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Pause(string id, [FromBody] PausePromotionRequest request)
        => Ok(ApiResponse<PromotionDto>.Ok(await _promotionService.TamNgungAsync(id, request.LyDo ?? "")));

    [HttpPost("{id}/resume")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Resume(string id)
        => Ok(ApiResponse<PromotionDto>.Ok(await _promotionService.TiepTucAsync(id)));

    [HttpDelete("{id}")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Delete(string id)
    {
        await _promotionService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Create([FromBody] PromotionRequest request)
    {
        var result = await _promotionService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PromotionDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.QuanLyKhuyenMai)]
    public async Task<IActionResult> Update(string id, [FromBody] PromotionRequest request)
    {
        var result = await _promotionService.UpdateAsync(id, request);
        return Ok(ApiResponse<PromotionDto>.Ok(result));
    }
}
