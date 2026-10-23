using KLCN060.Api.Authorization;
using KLCN060.Domain;
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
    private readonly IPermissionService _permissions;

    public ServicesController(IDichVuService dichVuService, IPermissionService permissions)
    {
        _dichVuService = dichVuService;
        _permissions = permissions;
    }

    /// <summary>Danh sách dịch vụ đang phục vụ (công khai). <c>?tatCa=true</c> chỉ có tác dụng với người có quyền QUAN_LY_DICH_VU: gồm cả dịch vụ đã ngừng phục vụ.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool tatCa = false)
    {
        var gomNgung = tatCa && User.Identity?.IsAuthenticated == true
            && await _permissions.CoQuyenAsync(CurrentUser.From(User).TenDN, Permissions.QuanLyDichVu);
        var result = await _dichVuService.GetAllAsync(gomNgung);
        return Ok(ApiResponse<List<ServiceDto>>.Ok(result));
    }

    /// <summary>Danh mục cho người quản lý: kèm thống kê sử dụng, gói liên quan, việc còn được phép làm.</summary>
    [HttpGet("catalog")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Catalog()
        => Ok(ApiResponse<ServiceCatalogDto>.Ok(await _dichVuService.GetCatalogAsync()));

    [HttpGet("{id}/history")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> History(string id)
        => Ok(ApiResponse<List<ServiceHistoryEntryDto>>.Ok(await _dichVuService.GetHistoryAsync(id)));

    [HttpPost]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Create([FromBody] ServiceRequest request)
    {
        var result = await _dichVuService.CreateAsync(request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ServiceDto>.Ok(result));
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Update(string id, [FromBody] ServiceRequest request)
    {
        var result = await _dichVuService.UpdateAsync(id, request);
        return Ok(ApiResponse<ServiceDto>.Ok(result));
    }

    [HttpDelete("{id}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Delete(string id)
    {
        await _dichVuService.DeleteAsync(id);
        return NoContent();
    }

    // ---- Chi tiết dịch vụ: quản lý xem đủ (kể cả mục đang ẩn) và chỉnh sửa; khách/lễ tân xem các mục hiển thị qua GET /services ----

    [HttpGet("{id}/details")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> GetDetails(string id)
    {
        var result = await _dichVuService.GetDetailsAsync(id);
        return Ok(ApiResponse<List<ServiceDetailDto>>.Ok(result));
    }

    [HttpPost("{id}/details")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> CreateDetail(string id, [FromBody] ServiceDetailRequest request)
    {
        var result = await _dichVuService.CreateDetailAsync(id, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ServiceDetailDto>.Ok(result));
    }

    [HttpPut("{id}/details/{maCTDV}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> UpdateDetail(string id, string maCTDV, [FromBody] ServiceDetailRequest request)
    {
        var result = await _dichVuService.UpdateDetailAsync(id, maCTDV, request);
        return Ok(ApiResponse<ServiceDetailDto>.Ok(result));
    }

    [HttpDelete("{id}/details/{maCTDV}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> DeleteDetail(string id, string maCTDV)
    {
        await _dichVuService.DeleteDetailAsync(id, maCTDV);
        return NoContent();
    }
}
