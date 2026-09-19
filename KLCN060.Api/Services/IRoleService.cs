using KLCN060.Api.DTOs.Roles;

namespace KLCN060.Api.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllAsync();
    Task<List<PermissionDto>> GetPermissionsAsync();
    Task<RoleDto> CreateAsync(RoleRequest request);
    Task<RoleDto> UpdateAsync(int maVaiTro, RoleRequest request);
    Task<List<EmployeeDto>> GetEmployeesAsync();
    Task<ChangeEmployeeRoleResultDto> ChangeEmployeeRoleAsync(string maNV, ChangeEmployeeRoleRequest request, CurrentUser user);
}
