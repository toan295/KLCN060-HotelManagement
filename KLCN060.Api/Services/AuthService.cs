using System.Security.Cryptography;
using System.Text;
using KLCN060.Api.DTOs.Auth;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class AuthService : IAuthService
{
    private const int SoLanDangNhapSaiToiDa = 5;
    private const int DoDaiMatKhauToiThieu = 8;

    private readonly KLCN060DbContext _context;
    private readonly ITokenService _tokenService;

    public AuthService(KLCN060DbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.MatKhau) || request.MatKhau.Length < DoDaiMatKhauToiThieu)
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_QUA_NGAN", $"Mật khẩu phải có ít nhất {DoDaiMatKhauToiThieu} ký tự.", nameof(request.MatKhau));

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var daTonTai = await _context.Khachs.AnyAsync(x => x.SoDT == request.SoDT)
            || await _context.TaiKhoans.AnyAsync(x => x.TenDN == request.SoDT);
        if (daTonTai)
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã được đăng ký.", nameof(request.SoDT));

        var maKhach = await SinhMaKhachMoiAsync();

        var khach = new Khach
        {
            MaKhach = maKhach,
            HoTen = request.HoTen,
            SoDT = request.SoDT,
            Email = request.Email
        };
        _context.Khachs.Add(khach);

        var taiKhoan = new TaiKhoan
        {
            TenDN = request.SoDT,
            MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhau),
            LoaiTaiKhoan = LoaiTaiKhoan.KHACH_HANG,
            MaKhach = maKhach,
            TrangThai = TrangThaiTaiKhoan.HOAT_DONG
        };
        _context.TaiKhoans.Add(taiKhoan);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã được đăng ký.", nameof(request.SoDT));
        }

        await transaction.CommitAsync();

        await GhiNhatKyAsync(taiKhoan.TenDN, "DANG_KY_TAI_KHOAN", $"MaKhach={maKhach}");

        return new RegisterResultDto { MaKhach = maKhach, TenDangNhap = taiKhoan.TenDN };
    }

    public async Task<LoginResultDto> LoginAsync(LoginRequest request)
    {
        var taiKhoan = await _context.TaiKhoans
            .Include(x => x.NhanVien).ThenInclude(nv => nv!.VaiTro)
            .FirstOrDefaultAsync(x => x.TenDN == request.TenDangNhap);

        if (taiKhoan is null)
            throw new ApiException(StatusCodes.Status401Unauthorized, "SAI_TAI_KHOAN_MAT_KHAU", "Sai tài khoản hoặc mật khẩu.");

        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.BI_KHOA)
            throw new ApiException(StatusCodes.Status423Locked, "TAI_KHOAN_BI_KHOA", "Tài khoản đã bị khóa do đăng nhập sai quá số lần cho phép.");

        var matKhauDung = BCrypt.Net.BCrypt.Verify(request.MatKhau, taiKhoan.MatKhau);
        if (!matKhauDung)
        {
            taiKhoan.SoLanDangNhapSai++;
            if (taiKhoan.SoLanDangNhapSai >= SoLanDangNhapSaiToiDa)
            {
                taiKhoan.TrangThai = TrangThaiTaiKhoan.BI_KHOA;
                await _context.SaveChangesAsync();
                await GhiNhatKyAsync(taiKhoan.TenDN, "TAI_KHOAN_BI_KHOA_TU_DONG", $"Sai mat khau {SoLanDangNhapSaiToiDa} lan lien tiep");
                throw new ApiException(StatusCodes.Status423Locked, "TAI_KHOAN_BI_KHOA", "Tài khoản đã bị khóa do đăng nhập sai quá số lần cho phép.");
            }

            await _context.SaveChangesAsync();
            await GhiNhatKyAsync(taiKhoan.TenDN, "DANG_NHAP_THAT_BAI", $"Lan sai thu {taiKhoan.SoLanDangNhapSai}");
            throw new ApiException(StatusCodes.Status401Unauthorized, "SAI_TAI_KHOAN_MAT_KHAU", "Sai tài khoản hoặc mật khẩu.");
        }

        taiKhoan.SoLanDangNhapSai = 0;

        var vaiTro = taiKhoan.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG
            ? "KHACH_HANG"
            : taiKhoan.NhanVien?.VaiTro.TenVaiTro
                ?? throw new ApiException(StatusCodes.Status401Unauthorized, "TAI_KHOAN_KHONG_HOP_LE", "Tài khoản nhân viên chưa được gán vai trò.");

        var accessToken = _tokenService.GenerateAccessToken(taiKhoan, vaiTro);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Chi luu HASH cua refresh token trong DB (giong nguyen tac khong luu mat khau tho) - neu CSDL bi ro ri,
        // ke tan cong khong the dung truc tiep gia tri trong cot RefreshToken de gia mao phien dang nhap.
        taiKhoan.RefreshToken = HashToken(refreshToken);
        taiKhoan.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_tokenService.RefreshTokenExpiryDays);

        await _context.SaveChangesAsync();
        await GhiNhatKyAsync(taiKhoan.TenDN, "DANG_NHAP_THANH_CONG");

        return new LoginResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInMinutes = _tokenService.AccessTokenExpiryMinutes
        };
    }

    public async Task<RefreshResultDto> RefreshAsync(RefreshRequest request)
    {
        var hash = HashToken(request.RefreshToken);
        var taiKhoan = await _context.TaiKhoans
            .Include(x => x.NhanVien).ThenInclude(nv => nv!.VaiTro)
            .FirstOrDefaultAsync(x => x.RefreshToken == hash);

        if (taiKhoan is null || taiKhoan.RefreshTokenExpiry is null || taiKhoan.RefreshTokenExpiry < DateTime.UtcNow)
            throw new ApiException(StatusCodes.Status401Unauthorized, "REFRESH_TOKEN_KHONG_HOP_LE", "Refresh token không hợp lệ hoặc đã hết hạn.");

        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.BI_KHOA)
            throw new ApiException(StatusCodes.Status423Locked, "TAI_KHOAN_BI_KHOA", "Tài khoản đã bị khóa.");

        var vaiTro = taiKhoan.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG
            ? "KHACH_HANG"
            : taiKhoan.NhanVien?.VaiTro.TenVaiTro
                ?? throw new ApiException(StatusCodes.Status401Unauthorized, "TAI_KHOAN_KHONG_HOP_LE", "Tài khoản nhân viên chưa được gán vai trò.");

        var accessToken = _tokenService.GenerateAccessToken(taiKhoan, vaiTro);

        return new RefreshResultDto
        {
            AccessToken = accessToken,
            ExpiresInMinutes = _tokenService.AccessTokenExpiryMinutes
        };
    }

    public async Task LogoutAsync(string tenDangNhap)
    {
        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(x => x.TenDN == tenDangNhap);
        if (taiKhoan is null) return;

        taiKhoan.RefreshToken = null;
        taiKhoan.RefreshTokenExpiry = null;
        await _context.SaveChangesAsync();
        await GhiNhatKyAsync(taiKhoan.TenDN, "DANG_XUAT");
    }

    /// <summary>Băm refresh token bằng SHA-256 trước khi lưu/so khớp trong DB - token thô chỉ tồn tại phía client.</summary>
    private static string HashToken(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    /// <summary>
    /// Ghi audit trail vào NhatKyThaoTac (Mục 2, Nhóm 7). Best-effort: lỗi ghi log không được làm hỏng
    /// thao tác nghiệp vụ chính đang thực hiện.
    /// </summary>
    private async Task GhiNhatKyAsync(string maTaiKhoan, string hanhDong, string? chiTiet = null)
    {
        try
        {
            _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaTaiKhoan = maTaiKhoan,
                HanhDong = hanhDong,
                ThoiGian = DateTime.Now,
                ChiTiet = chiTiet
            });
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Khong de loi ghi audit lam that bai luong dang nhap/dang ky chinh.
        }
    }

    private async Task<string> SinhMaKhachMoiAsync()
    {
        var maKhachs = await _context.Khachs.Select(x => x.MaKhach).ToListAsync();
        var soLon = maKhachs
            .Select(ma => int.TryParse(ma.Replace("KH", string.Empty), out var so) ? so : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"KH{soLon + 1:D2}";
    }
}
