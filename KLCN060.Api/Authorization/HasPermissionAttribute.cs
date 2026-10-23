using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Authorization;

/// <summary>
/// Yêu cầu người gọi có ÍT NHẤT MỘT trong các quyền nêu tên, theo phân quyền HIỆN TẠI trong CSDL (bảng VaiTro_Quyen do Quản lý chỉnh ở D15):
/// đọc lại ở mỗi request nên quyền vừa thêm/gỡ có hiệu lực ngay, không phải chờ token hết hạn.
/// <see cref="AllowRoles"/>: các vai trò được qua không cần quyền (dành cho khách hàng thao tác trên dữ liệu của chính họ; API kiểm tra
/// quyền sở hữu ở tầng Service). Vai trò KHACH_HANG không bao giờ được cấp quyền nhân viên.
/// </summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    private readonly string[] _quyen;
    private string _allowRoles = "";

    public HasPermissionAttribute(params string[] quyen)
    {
        _quyen = quyen;
        CapNhatChinhSach();
    }

    public string AllowRoles
    {
        get => _allowRoles;
        set { _allowRoles = value; CapNhatChinhSach(); }
    }

    private void CapNhatChinhSach() => Policy = PermissionPolicy.TaoTen(_quyen, _allowRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

internal static class PermissionPolicy
{
    public const string TienTo = "perm:";

    public static string TaoTen(string[] quyen, string[] vaiTro) => $"{TienTo}{string.Join(',', quyen)};roles:{string.Join(',', vaiTro)}";

    public static PermissionRequirement? Phan(string policyName)
    {
        if (!policyName.StartsWith(TienTo, StringComparison.Ordinal)) return null;
        var phan = policyName[TienTo.Length..].Split(";roles:");
        return new PermissionRequirement(
            phan[0].Split(',', StringSplitOptions.RemoveEmptyEntries),
            phan.Length > 1 ? phan[1].Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>());
    }
}

public sealed record PermissionRequirement(string[] Quyen, string[] AllowRoles) : IAuthorizationRequirement;

/// <summary>Tạo chính sách "perm:..." theo tên (mỗi tổ hợp quyền/vai trò là một chính sách), các chính sách khác dùng mặc định của ASP.NET Core.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var yeuCau = PermissionPolicy.Phan(policyName);
        if (yeuCau is null) return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(yeuCau).Build();
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly KLCN060DbContext _context;

    public PermissionHandler(KLCN060DbContext context) => _context = context;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var vaiTroTrongToken = context.User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(vaiTroTrongToken)) return;

        // Trạng thái tài khoản và vai trò HIỆN TẠI lấy từ CSDL ở mỗi request (không tin vai trò ghi trong token): Quản lý khóa tài khoản
        // hoặc đổi vai trò nhân viên thì có hiệu lực ngay, không phải chờ access token hết hạn.
        var tenDN = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(tenDN)) return;
        var taiKhoan = await _context.TaiKhoans.AsNoTracking()
            .Where(x => x.TenDN == tenDN)
            .Select(x => new { x.TrangThai, x.LoaiTaiKhoan, VaiTro = x.NhanVien == null ? null : x.NhanVien.VaiTro.TenVaiTro })
            .FirstOrDefaultAsync();
        if (taiKhoan is null || taiKhoan.TrangThai != TrangThaiTaiKhoan.HOAT_DONG) return;

        var vaiTro = taiKhoan.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG ? "KHACH_HANG" : taiKhoan.VaiTro;
        if (string.IsNullOrEmpty(vaiTro)) return;

        if (requirement.AllowRoles.Contains(vaiTro))
        {
            context.Succeed(requirement);
            return;
        }

        // Khách hàng không có quyền nhân viên dù dữ liệu phân quyền có bị chỉnh sai.
        if (vaiTro == "KHACH_HANG") return;

        var coQuyen = await _context.VaiTroQuyens.AsNoTracking()
            .AnyAsync(x => x.VaiTro.TenVaiTro == vaiTro && requirement.Quyen.Contains(x.Quyen.TenQuyen));
        if (coQuyen)
            context.Succeed(requirement);
    }
}
