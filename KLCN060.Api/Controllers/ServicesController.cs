using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
public class ServicesController : ControllerBase
{
    private readonly IDichVuService _dichVuService;

    public ServicesController(IDichVuService dichVuService)
    {
        _dichVuService = dichVuService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var result = await _dichVuService.GetAllAsync();
        return Ok(ApiResponse<List<ServiceDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Create([FromBody] ServiceRequest request)
    {
        var result = await _dichVuService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ServiceDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Update(string id, [FromBody] ServiceRequest request)
    {
        var result = await _dichVuService.UpdateAsync(id, request);
        return Ok(ApiResponse<ServiceDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "QUAN_LY")]
    public async Task<IActionResult> Delete(string id)
    {
        await _dichVuService.DeleteAsync(id);
        return NoContent();
    }
}
