namespace KLCN060.Api.DTOs.Roles;

public class PermissionDto
{
    public int MaQuyen { get; set; }
    public string TenQuyen { get; set; } = null!;
    public string? NhomChucNang { get; set; }
}

public class RoleDto
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public string? MoTa { get; set; }
    public int SoNhanVien { get; set; }
    public List<PermissionDto> Quyen { get; set; } = new();
}

public class RoleRequest
{
    public string TenVaiTro { get; set; } = null!;
    public string? MoTa { get; set; }

    /// <summary>Tập quyền mới của vai trò (khi PUT: thay thế toàn bộ tập cũ).</summary>
    public List<int> MaQuyen { get; set; } = new();
}

public class EmployeeDto
{
    public string MaNV { get; set; } = null!;
    public string HoTen { get; set; } = null!;
    public string SoDT { get; set; } = null!;
    public string ChucVu { get; set; } = null!;
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = null!;
    public string? TenDangNhap { get; set; }
}

public class ChangeEmployeeRoleRequest
{
    public int MaVaiTro { get; set; }
}

public class ChangeEmployeeRoleResultDto
{
    public EmployeeDto NhanVien { get; set; } = null!;
    public string VaiTroCu { get; set; } = null!;
    public string GhiChu { get; set; } = null!;
}
