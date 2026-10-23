using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Roles;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1")]
[HasPermission(Permissions.QuanTriHeThong)]
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

    /// <summary>Xem trước tác động của việc đổi tập quyền (ai bị ảnh hưởng, cấp thêm / thu hồi gì, có quyền nhạy cảm không) - chưa lưu gì.</summary>
    [HttpPost("roles/{maVaiTro:int}/preview")]
    public async Task<IActionResult> PreviewRole(int maVaiTro, [FromBody] RoleRequest request)
        => Ok(ApiResponse<RoleChangePreviewDto>.Ok(await _roleService.PreviewAsync(maVaiTro, request)));

    /// <summary>Lịch sử thay đổi phân quyền và tài khoản nhân viên (ai · lúc nào · làm gì · với ai · trước → sau · lý do); <c>?doiTuong=</c> là mã nhân viên hoặc tên vai trò.</summary>
    [HttpGet("roles/history")]
    public async Task<IActionResult> History([FromQuery] string? doiTuong)
        => Ok(ApiResponse<List<RoleHistoryEntryDto>>.Ok(await _roleService.GetHistoryAsync(doiTuong)));

    [HttpPut("roles/{maVaiTro:int}")]
    public async Task<IActionResult> UpdateRole(int maVaiTro, [FromBody] RoleRequest request)
        => Ok(ApiResponse<RoleDto>.Ok(await _roleService.UpdateAsync(maVaiTro, request)));

    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
        => Ok(ApiResponse<List<EmployeeDto>>.Ok(await _roleService.GetEmployeesAsync()));

    /// <summary>Tạo nhân viên mới kèm tài khoản đăng nhập (vai trò + mật khẩu tạm).</summary>
    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<EmployeeDto>.Ok(await _roleService.CreateEmployeeAsync(request, CurrentUser.From(User))));

    [HttpPut("employees/{maNV}")]
    public async Task<IActionResult> UpdateEmployee(string maNV, [FromBody] UpdateEmployeeRequest request)
        => Ok(ApiResponse<EmployeeDto>.Ok(await _roleService.UpdateEmployeeAsync(maNV, request, CurrentUser.From(User))));

    /// <summary>Khóa (vô hiệu hóa) tài khoản nhân viên: có hiệu lực ngay, thu hồi mọi phiên đăng nhập.</summary>
    [HttpPost("employees/{maNV}/lock")]
    public async Task<IActionResult> LockEmployee(string maNV, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AccountActionRequest? request = null)
        => Ok(ApiResponse<EmployeeAccountResultDto>.Ok(await _roleService.LockEmployeeAsync(maNV, CurrentUser.From(User), request?.LyDo)));

    [HttpPut("employees/{maNV}/roles")]
    public async Task<IActionResult> ChangeEmployeeRole(string maNV, [FromBody] ChangeEmployeeRoleRequest request)
        => Ok(ApiResponse<ChangeEmployeeRoleResultDto>.Ok(await _roleService.ChangeEmployeeRoleAsync(maNV, request, CurrentUser.From(User))));

    /// <summary>Mở khóa tài khoản nhân viên bị khóa do nhập sai mật khẩu quá 5 lần (nhân viên không có email để tự khôi phục).</summary>
    [HttpPost("employees/{maNV}/unlock")]
    public async Task<IActionResult> UnlockEmployee(string maNV, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AccountActionRequest? request = null)
        => Ok(ApiResponse<EmployeeAccountResultDto>.Ok(await _roleService.UnlockEmployeeAsync(maNV, CurrentUser.From(User), request?.LyDo)));

    /// <summary>Xóa nhân viên đã bị khóa (bắt buộc lý do): ẩn khỏi danh sách, không đăng nhập được, vẫn giữ lịch sử để truy vết.</summary>
    [HttpPost("employees/{maNV}/delete")]
    public async Task<IActionResult> DeleteEmployee(string maNV, [FromBody] AccountActionRequest request)
        => Ok(ApiResponse<EmployeeAccountResultDto>.Ok(await _roleService.DeleteEmployeeAsync(maNV, CurrentUser.From(User), request?.LyDo)));

    /// <summary>Quản lý đặt mật khẩu tạm cho nhân viên quên mật khẩu (đồng thời mở khóa và thu hồi các phiên đăng nhập cũ).</summary>
    [HttpPut("employees/{maNV}/password")]
    public async Task<IActionResult> ResetEmployeePassword(string maNV, [FromBody] ResetEmployeePasswordRequest request)
        => Ok(ApiResponse<EmployeeAccountResultDto>.Ok(await _roleService.ResetEmployeePasswordAsync(maNV, request, CurrentUser.From(User))));
}
