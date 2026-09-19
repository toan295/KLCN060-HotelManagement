using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Roles;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "QUAN_LY")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
        => Ok(ApiResponse<List<RoleDto>>.Ok(await _roleService.GetAllAsync()));

    /// <summary>Danh sách toàn bộ quyền có thể gán cho vai trò (phục vụ màn hình D15).</summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
        => Ok(ApiResponse<List<PermissionDto>>.Ok(await _roleService.GetPermissionsAsync()));

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] RoleRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<RoleDto>.Ok(await _roleService.CreateAsync(request)));

    [HttpPut("roles/{maVaiTro:int}")]
    public async Task<IActionResult> UpdateRole(int maVaiTro, [FromBody] RoleRequest request)
        => Ok(ApiResponse<RoleDto>.Ok(await _roleService.UpdateAsync(maVaiTro, request)));

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
        => Ok(ApiResponse<List<EmployeeDto>>.Ok(await _roleService.GetEmployeesAsync()));

    [HttpPut("employees/{maNV}/roles")]
    public async Task<IActionResult> ChangeEmployeeRole(string maNV, [FromBody] ChangeEmployeeRoleRequest request)
        => Ok(ApiResponse<ChangeEmployeeRoleResultDto>.Ok(await _roleService.ChangeEmployeeRoleAsync(maNV, request, CurrentUser.From(User))));
}
