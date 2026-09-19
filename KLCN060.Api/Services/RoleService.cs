using KLCN060.Api.DTOs.Roles;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class RoleService : IRoleService
{
    /// <summary>5 vai trò hệ thống: tên được dùng trong [Authorize(Roles=...)] nên không được đổi tên.</summary>
    private static readonly string[] VaiTroHeThong = { "QUAN_LY", "LE_TAN", "BUONG_PHONG", "KE_TOAN", "KHACH_HANG" };

    private const string GhiChuHieuLuc =
        "Vai trò mới có hiệu lực khi nhân viên đăng nhập lại hoặc làm mới token; access token đang dùng vẫn giữ vai trò cũ tối đa 30 phút.";

    private readonly KLCN060DbContext _context;

    public RoleService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<RoleDto>> GetAllAsync()
    {
        var vaiTros = await _context.VaiTros
            .AsNoTracking()
            .Include(x => x.VaiTroQuyens).ThenInclude(vq => vq.Quyen)
            .Include(x => x.NhanViens)
            .OrderBy(x => x.MaVaiTro)
            .ToListAsync();

        return vaiTros.Select(ToDto).ToList();
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
    {
        return await _context.Quyens
            .OrderBy(x => x.NhomChucNang).ThenBy(x => x.TenQuyen)
            .Select(x => new PermissionDto { MaQuyen = x.MaQuyen, TenQuyen = x.TenQuyen, NhomChucNang = x.NhomChucNang })
            .ToListAsync();
    }

    public async Task<RoleDto> CreateAsync(RoleRequest request)
    {
        var ten = ChuanHoaTen(request.TenVaiTro);
        if (await _context.VaiTros.AnyAsync(x => x.TenVaiTro == ten))
            throw new ApiException(StatusCodes.Status409Conflict, "VAI_TRO_DA_TON_TAI", "Tên vai trò đã tồn tại.", "tenVaiTro");

        var maQuyens = await KiemTraQuyenAsync(request.MaQuyen);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var vaiTro = new VaiTro { TenVaiTro = ten, MoTa = request.MoTa };
        _context.VaiTros.Add(vaiTro);
        await _context.SaveChangesAsync();

        foreach (var maQuyen in maQuyens)
            _context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = vaiTro.MaVaiTro, MaQuyen = maQuyen });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo vai trò, vui lòng thử lại.");
        }
        await transaction.CommitAsync();

        return await LayAsync(vaiTro.MaVaiTro);
    }

    public async Task<RoleDto> UpdateAsync(int maVaiTro, RoleRequest request)
    {
        var vaiTro = await _context.VaiTros.Include(x => x.VaiTroQuyens).FirstOrDefaultAsync(x => x.MaVaiTro == maVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.");

        var ten = ChuanHoaTen(request.TenVaiTro);
        if (ten != vaiTro.TenVaiTro)
        {
            if (VaiTroHeThong.Contains(vaiTro.TenVaiTro))
                throw new ApiException(StatusCodes.Status400BadRequest, "VAI_TRO_HE_THONG", $"Không được đổi tên vai trò hệ thống {vaiTro.TenVaiTro}.", "tenVaiTro");
            if (await _context.VaiTros.AnyAsync(x => x.TenVaiTro == ten && x.MaVaiTro != maVaiTro))
                throw new ApiException(StatusCodes.Status409Conflict, "VAI_TRO_DA_TON_TAI", "Tên vai trò đã tồn tại.", "tenVaiTro");
        }

        var maQuyens = (await KiemTraQuyenAsync(request.MaQuyen)).ToHashSet();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        vaiTro.TenVaiTro = ten;
        vaiTro.MoTa = request.MoTa;

        // Kết quả tương đương "xóa hết rồi thêm lại": tập quyền cuối cùng đúng bằng danh sách gửi lên.
        foreach (var cu in vaiTro.VaiTroQuyens.Where(x => !maQuyens.Contains(x.MaQuyen)).ToList())
            _context.VaiTroQuyens.Remove(cu);
        var daCo = vaiTro.VaiTroQuyens.Select(x => x.MaQuyen).ToHashSet();
        foreach (var moi in maQuyens.Where(q => !daCo.Contains(q)))
            _context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = maVaiTro, MaQuyen = moi });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LayAsync(maVaiTro);
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        return await _context.NhanViens
            .AsNoTracking()
            .OrderBy(x => x.MaNV)
            .Select(x => new EmployeeDto
            {
                MaNV = x.MaNV,
                HoTen = x.HoTen,
                SoDT = x.SoDT,
                ChucVu = x.ChucVu,
                MaVaiTro = x.MaVaiTro,
                TenVaiTro = x.VaiTro.TenVaiTro,
                TenDangNhap = x.TaiKhoan == null ? null : x.TaiKhoan.TenDN
            })
            .ToListAsync();
    }

    public async Task<ChangeEmployeeRoleResultDto> ChangeEmployeeRoleAsync(string maNV, ChangeEmployeeRoleRequest request, CurrentUser user)
    {
        if (maNV == user.MaNV)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_THE_TU_DOI_VAI_TRO_CUA_CHINH_MINH", "Không thể tự đổi vai trò của chính mình.");

        var nhanVien = await _context.NhanViens.Include(x => x.VaiTro).Include(x => x.TaiKhoan).FirstOrDefaultAsync(x => x.MaNV == maNV)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Không tìm thấy nhân viên.");

        var vaiTroMoi = await _context.VaiTros.FirstOrDefaultAsync(x => x.MaVaiTro == request.MaVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.", "maVaiTro");
        if (vaiTroMoi.TenVaiTro == "KHACH_HANG")
            throw new ApiException(StatusCodes.Status400BadRequest, "VAI_TRO_KHONG_HOP_LE", "Không thể gán vai trò KHACH_HANG cho nhân viên.", "maVaiTro");

        var vaiTroCu = nhanVien.VaiTro.TenVaiTro;
        nhanVien.MaVaiTro = vaiTroMoi.MaVaiTro;

        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = user.TenDN,
            HanhDong = "DOI_VAI_TRO_NHAN_VIEN",
            DoiTuongTacDong = maNV,
            ThoiGian = DateTime.Now,
            ChiTiet = $"{vaiTroCu} -> {vaiTroMoi.TenVaiTro}"
        });

        await _context.SaveChangesAsync();

        return new ChangeEmployeeRoleResultDto
        {
            NhanVien = new EmployeeDto
            {
                MaNV = nhanVien.MaNV,
                HoTen = nhanVien.HoTen,
                SoDT = nhanVien.SoDT,
                ChucVu = nhanVien.ChucVu,
                MaVaiTro = vaiTroMoi.MaVaiTro,
                TenVaiTro = vaiTroMoi.TenVaiTro,
                TenDangNhap = nhanVien.TaiKhoan?.TenDN
            },
            VaiTroCu = vaiTroCu,
            GhiChu = GhiChuHieuLuc
        };
    }

    private async Task<RoleDto> LayAsync(int maVaiTro)
    {
        var vaiTro = await _context.VaiTros
            .AsNoTracking()
            .Include(x => x.VaiTroQuyens).ThenInclude(vq => vq.Quyen)
            .Include(x => x.NhanViens)
            .FirstAsync(x => x.MaVaiTro == maVaiTro);
        return ToDto(vaiTro);
    }

    private async Task<List<int>> KiemTraQuyenAsync(List<int>? maQuyen)
    {
        var distinct = (maQuyen ?? new()).Distinct().ToList();
        var tonTai = await _context.Quyens.Where(x => distinct.Contains(x.MaQuyen)).Select(x => x.MaQuyen).ToListAsync();
        var thieu = distinct.Except(tonTai).ToList();
        if (thieu.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUYEN_KHONG_TON_TAI", $"Không tìm thấy quyền: {string.Join(", ", thieu)}.", "maQuyen");
        return distinct;
    }

    private static string ChuanHoaTen(string? ten)
    {
        var t = ten?.Trim();
        if (string.IsNullOrEmpty(t))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_VAI_TRO", "Tên vai trò là bắt buộc.", "tenVaiTro");
        if (t.Length > 50)
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_VAI_TRO_QUA_DAI", "Tên vai trò tối đa 50 ký tự.", "tenVaiTro");
        return t;
    }

    private static RoleDto ToDto(VaiTro x) => new()
    {
        MaVaiTro = x.MaVaiTro,
        TenVaiTro = x.TenVaiTro,
        MoTa = x.MoTa,
        SoNhanVien = x.NhanViens.Count,
        Quyen = x.VaiTroQuyens
            .OrderBy(vq => vq.Quyen.MaQuyen)
            .Select(vq => new PermissionDto { MaQuyen = vq.Quyen.MaQuyen, TenQuyen = vq.Quyen.TenQuyen, NhomChucNang = vq.Quyen.NhomChucNang })
            .ToList()
    };
}
