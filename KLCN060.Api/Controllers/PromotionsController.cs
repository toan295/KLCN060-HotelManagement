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
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _promotionService.GetAllAsync();
        return Ok(ApiResponse<List<PromotionDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Create([FromBody] PromotionRequest request)
    {
        var result = await _promotionService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PromotionDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Update(string id, [FromBody] PromotionRequest request)
    {
        var result = await _promotionService.UpdateAsync(id, request);
        return Ok(ApiResponse<PromotionDto>.Ok(result));
    }
}
