using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Gói dịch vụ (package): xem công khai các gói đang bán; thêm/sửa/xóa cần quyền QUAN_LY_DICH_VU.</summary>
[ApiController]
[Route("api/v1/service-packages")]
public class ServicePackagesController : ControllerBase
{
    private readonly IServicePackageService _packages;
    private readonly IPermissionService _permissions;

    public ServicePackagesController(IServicePackageService packages, IPermissionService permissions)
    {
        _packages = packages;
        _permissions = permissions;
    }

    /// <summary><c>?tatCa=true</c> chỉ có tác dụng với người có quyền QUAN_LY_DICH_VU: gồm cả gói đã tắt.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool tatCa = false)
    {
        var gomNgung = tatCa && User.Identity?.IsAuthenticated == true
            && await _permissions.CoQuyenAsync(CurrentUser.From(User).TenDN, Permissions.QuanLyDichVu);
        return Ok(ApiResponse<List<ServicePackageDto>>.Ok(await _packages.GetAllAsync(gomNgung)));
    }

    [HttpGet("{maGoi}/history")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> History(string maGoi)
        => Ok(ApiResponse<List<ServiceHistoryEntryDto>>.Ok(await _packages.GetHistoryAsync(maGoi)));

    [HttpPost]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Create([FromBody] ServicePackageRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<ServicePackageDto>.Ok(await _packages.CreateAsync(request, CurrentUser.From(User).TenDN)));

    [HttpPut("{maGoi}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Update(string maGoi, [FromBody] ServicePackageRequest request)
        => Ok(ApiResponse<ServicePackageDto>.Ok(await _packages.UpdateAsync(maGoi, request)));

    [HttpDelete("{maGoi}")]
    [HasPermission(Permissions.QuanLyDichVu)]
    public async Task<IActionResult> Delete(string maGoi)
    {
        await _packages.DeleteAsync(maGoi);
        return NoContent();
    }
}
