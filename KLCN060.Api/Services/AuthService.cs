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
    private readonly ILogger<AuthService> _logger;

    public AuthService(KLCN060DbContext context, ITokenService tokenService, ILogger<AuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<RegisterResultDto> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.MatKhau) || request.MatKhau.Length < DoDaiMatKhauToiThieu)
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_QUA_NGAN", $"Mật khẩu phải có ít nhất {DoDaiMatKhauToiThieu} ký tự.", nameof(request.MatKhau));

        request.SoDT = ContactValidator.KiemTraSoDT(request.SoDT, nameof(request.SoDT));
        // Email BẮT BUỘC: đây là kênh duy nhất để khôi phục mật khẩu (nhận liên kết đặt lại qua đúng email đã đăng ký).
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_EMAIL", "Email là bắt buộc (dùng để khôi phục mật khẩu khi bạn quên).", nameof(request.Email));
        ContactValidator.KiemTraEmail(request.Email, nameof(request.Email));
        request.Email = request.Email.Trim();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // [Khối 8 - P1-4] Khóa sinh mã "KH" (DÙNG CHUNG với GuestService.CreateAsync — 2 luồng cùng sinh MaKhach) giữ tới hết
        // transaction, lấy TRƯỚC khi kiểm tra trùng SĐT → kiểm tra trùng + sinh mã + ghi là 1 khối tuần tự, không còn
        // khe hở để 2 đăng ký đồng thời cùng SĐT lọt qua hay sinh trùng MaKhach.
        await MaCodeGenerator.KhoaTienToAsync(_context, "KH");

        var daTonTai = await _context.Khachs.AnyAsync(x => x.SoDT == request.SoDT)
            || await _context.TaiKhoans.AnyAsync(x => x.TenDN == request.SoDT);
        if (daTonTai)
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã được đăng ký.", nameof(request.SoDT));

        // [Khối 8 - K8-9] CN02-BR-01: email dùng để đăng nhập nên phải duy nhất giữa các TÀI KHOẢN khách (hồ sơ khách lập tại
        // quầy, chưa có tài khoản, được phép trùng — không ảnh hưởng đăng nhập).
        if (!string.IsNullOrWhiteSpace(request.Email) && await EmailThuocTaiKhoanKhacAsync(request.Email, null))
            throw new ApiException(StatusCodes.Status409Conflict, "EMAIL_DA_TON_TAI", "Email đã được dùng cho một tài khoản khác.", nameof(request.Email));

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

        // Khách hàng cũng được đăng nhập bằng Email đã đăng ký (nhãn W5 "SĐT/Email") — TenDN vẫn luôn là SĐT,
        // ở đây chỉ tra Khach.Email để suy ra đúng TaiKhoan tương ứng, không đổi cơ chế TenDN hiện có.
        if (taiKhoan is null && request.TenDangNhap.Contains('@'))
        {
            // [Khối 8 - K8-9] Trước đây tìm Khachs.FirstOrDefault(Email) trên MỌI hồ sơ khách: nếu lễ tân đã lập hồ sơ (không
            // tài khoản) trùng email, bản ghi đó có thể được chọn trước → không có tài khoản → chính chủ không đăng nhập được
            // bằng email. Tìm thẳng trong các TÀI KHOẢN khách hàng (email là duy nhất giữa các tài khoản — xem RegisterAsync).
            taiKhoan = await _context.TaiKhoans
                .Include(x => x.NhanVien).ThenInclude(nv => nv!.VaiTro)
                .FirstOrDefaultAsync(x => x.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG && x.Khach != null && x.Khach.Email == request.TenDangNhap);
        }

        if (taiKhoan is null)
            throw new ApiException(StatusCodes.Status401Unauthorized, "SAI_TAI_KHOAN_MAT_KHAU", "Sai tài khoản hoặc mật khẩu.");

        // Tài khoản nhân viên đã xóa: coi như không tồn tại (không tiết lộ).
        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.DA_XOA)
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
        taiKhoan.LanDangNhapCuoi = DateTime.Now;

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

        if (taiKhoan.TrangThai != TrangThaiTaiKhoan.HOAT_DONG)
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

    internal static Task<bool> EmailThuocTaiKhoanKhacAsync(KLCN060DbContext context, string email, string? maKhachHienTai)
        => context.TaiKhoans.AnyAsync(x => x.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG && x.Khach != null
            && x.Khach.Email == email.Trim() && x.MaKhach != maKhachHienTai);

    private Task<bool> EmailThuocTaiKhoanKhacAsync(string email, string? maKhachHienTai)
        => EmailThuocTaiKhoanKhacAsync(_context, email, maKhachHienTai);

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
