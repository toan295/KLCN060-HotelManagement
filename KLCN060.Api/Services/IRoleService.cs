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
    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CurrentUser user);
    Task<EmployeeDto> UpdateEmployeeAsync(string maNV, UpdateEmployeeRequest request, CurrentUser user);
    Task<EmployeeAccountResultDto> LockEmployeeAsync(string maNV, CurrentUser user, string? lyDo = null);
    Task<EmployeeAccountResultDto> UnlockEmployeeAsync(string maNV, CurrentUser user, string? lyDo = null);
    Task<EmployeeAccountResultDto> DeleteEmployeeAsync(string maNV, CurrentUser user, string? lyDo);
    Task<RoleChangePreviewDto> PreviewAsync(int maVaiTro, RoleRequest request);
    Task<List<RoleHistoryEntryDto>> GetHistoryAsync(string? doiTuong);
    Task<EmployeeAccountResultDto> ResetEmployeePasswordAsync(string maNV, ResetEmployeePasswordRequest request, CurrentUser user);
}
