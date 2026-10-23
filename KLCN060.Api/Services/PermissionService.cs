using KLCN060.Api.DTOs.Roles;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IPermissionService
{
    /// <summary>Quyền HIỆN TẠI (theo CSDL) của một vai trò.</summary>
    Task<MyPermissionsDto> GetForRoleAsync(string vaiTro);

    /// <summary>Vai trò và quyền HIỆN TẠI của một tài khoản (đọc từ CSDL, không phụ thuộc vai trò ghi trong token).</summary>
    Task<MyPermissionsDto> GetForAccountAsync(string tenDN);

    /// <summary>Tài khoản có (ít nhất một trong) các quyền này không — dùng để phân nhánh nghiệp vụ theo quyền thay vì theo tên vai trò.</summary>
    Task<bool> CoQuyenAsync(string tenDN, params string[] quyen);
}

public class PermissionService : IPermissionService
{
    private readonly KLCN060DbContext _context;

    public PermissionService(KLCN060DbContext context) => _context = context;

    public async Task<MyPermissionsDto> GetForRoleAsync(string vaiTro)
    {
        // Khách hàng không có quyền nhân viên.
        var quyen = vaiTro == "KHACH_HANG"
            ? new List<string>()
            : await _context.VaiTroQuyens.AsNoTracking()
                .Where(x => x.VaiTro.TenVaiTro == vaiTro)
                .Select(x => x.Quyen.TenQuyen)
                .OrderBy(x => x)
                .ToListAsync();

        return new MyPermissionsDto { VaiTro = vaiTro, Quyen = quyen };
    }

    public async Task<MyPermissionsDto> GetForAccountAsync(string tenDN)
    {
        var taiKhoan = await _context.TaiKhoans.AsNoTracking()
            .Where(x => x.TenDN == tenDN)
            .Select(x => new { x.LoaiTaiKhoan, x.TrangThai, VaiTro = x.NhanVien == null ? null : x.NhanVien.VaiTro.TenVaiTro })
            .FirstOrDefaultAsync();
        if (taiKhoan is null || taiKhoan.TrangThai != TrangThaiTaiKhoan.HOAT_DONG)
            return new MyPermissionsDto { VaiTro = string.Empty };

        return await GetForRoleAsync(taiKhoan.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG ? "KHACH_HANG" : taiKhoan.VaiTro ?? string.Empty);
    }

    public async Task<bool> CoQuyenAsync(string tenDN, params string[] quyen)
        => (await GetForAccountAsync(tenDN)).Quyen.Intersect(quyen).Any();
}
