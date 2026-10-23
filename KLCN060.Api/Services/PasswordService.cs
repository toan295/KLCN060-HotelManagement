using System.Net;
using System.Security.Cryptography;
using System.Text;
using KLCN060.Api.DTOs.Auth;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Auth;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

public interface IPasswordService
{
    Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordRequest request, string? diaChiIp);
    Task<VerifyResetCodeResultDto> XacNhanMaAsync(VerifyResetCodeRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task ChangePasswordAsync(string tenDangNhap, ChangePasswordRequest request);
}

/// <summary>Quên / đặt lại / đổi mật khẩu.</summary>
public class PasswordService : IPasswordService
{
    public const int DoDaiToiThieu = 8;
    public const int DoDaiToiDa = 64;      // BCrypt chỉ xét 72 byte đầu; giới hạn 64 ký tự để không cắt cụt âm thầm
    private const int SoLanSaiToiDa = 5;

    private const string ThongBaoDaGuiMa =
        "Chúng tôi đã gửi mã xác nhận gồm 6 chữ số tới email của bạn. Vui lòng kiểm tra cả mục thư rác (spam). " +
        "Nếu sau vài phút vẫn chưa nhận được, hãy bấm gửi lại mã hoặc gọi hotline 1900 0001.";

    private readonly KLCN060DbContext _context;
    private readonly AuthOptions _options;
    private readonly IEmailQueue _email;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(KLCN060DbContext context, IOptions<AuthOptions> options, IEmailQueue email, ILogger<PasswordService> logger)
    {
        _context = context;
        _options = options.Value;
        _email = email;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Quên mật khẩu (bước 1: gửi mã xác nhận)

    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(ForgotPasswordRequest request, string? diaChiIp)
    {
        var email = ChuanHoaEmail(request.Email);

        // Chưa cấu hình gửi email thì nói thẳng, KHÔNG giả vờ đã gửi.
        if (!_email.IsAvailable)
        {
            _logger.LogError("Không thể gửi mã xác nhận đặt lại mật khẩu: chưa cấu hình SMTP (Email:Smtp:Host/From).");
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "EMAIL_CHUA_KHA_DUNG",
                "Chức năng gửi email tạm thời chưa khả dụng. Vui lòng thử lại sau hoặc gọi hotline 1900 0001 để được hỗ trợ.");
        }

        var taiKhoan = await TimTaiKhoanTheoEmailAsync(email);
        var bayGio = DateTime.Now;

        // Giới hạn số yêu cầu theo tài khoản: chống spam hộp thư của chủ tài khoản.
        var moiDay = await _context.YeuCauDatLaiMatKhaus
            .CountAsync(x => x.TenDN == taiKhoan.TenDN && x.NgayTao > bayGio.AddHours(-1));
        if (moiDay >= _options.MaxResetRequestsPerHour)
        {
            _logger.LogWarning("Tài khoản {TenDN} đã yêu cầu mã đặt lại mật khẩu {N} lần trong 1 giờ - từ chối yêu cầu mới.", taiKhoan.TenDN, moiDay);
            throw new ApiException(StatusCodes.Status429TooManyRequests, "QUA_NHIEU_YEU_CAU",
                "Bạn đã yêu cầu mã xác nhận quá nhiều lần trong 1 giờ. Vui lòng thử lại sau hoặc gọi hotline 1900 0001.");
        }

        // Mã mới làm các yêu cầu cũ chưa dùng mất hiệu lực: mỗi tài khoản chỉ có một mã còn sống.
        await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.TenDN == taiKhoan.TenDN && x.NgaySuDung == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgaySuDung, bayGio));

        var ma = TaoMaXacNhan();
        _context.YeuCauDatLaiMatKhaus.Add(new YeuCauDatLaiMatKhau
        {
            TenDN = taiKhoan.TenDN,
            // Mã đặt lại thật chỉ được cấp sau khi nhập đúng mã xác nhận; trước đó điền giá trị ngẫu nhiên không ai biết để cột này luôn duy nhất.
            TokenHash = BamToken(TaoToken()),
            MaXacNhanHash = BamMaXacNhan(taiKhoan.TenDN, ma),
            NgayTao = bayGio,
            HanDung = bayGio.AddMinutes(Math.Max(1, _options.ResetCodeMinutes)),
            DiaChiIP = diaChiIp
        });
        await GhiNhatKyAsync(taiKhoan.TenDN, "YEU_CAU_DAT_LAI_MAT_KHAU", $"ip={diaChiIp}");
        await _context.SaveChangesAsync();

        // Gửi tới địa chỉ ĐÃ LƯU của tài khoản (không phải chuỗi người dùng vừa gõ - có thể khác hoa/thường).
        _email.TryEnqueue(MauEmail.MaXacNhan(taiKhoan.Khach!.Email!.Trim(), taiKhoan.Khach.HoTen, ma, _options.ResetCodeMinutes, bayGio));

        return new ForgotPasswordResultDto { ThongBao = ThongBaoDaGuiMa };
    }

    // ---------------------------------------------------------------- Bước 2: nhập mã xác nhận

    public async Task<VerifyResetCodeResultDto> XacNhanMaAsync(VerifyResetCodeRequest request)
    {
        var email = ChuanHoaEmail(request.Email);
        var ma = (request.Ma ?? string.Empty).Trim();
        if (ma.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_XAC_NHAN", "Vui lòng nhập mã xác nhận.", "ma");

        var taiKhoan = await TimTaiKhoanTheoEmailAsync(email);
        var bayGio = DateTime.Now;

        var yeuCau = await _context.YeuCauDatLaiMatKhaus.AsNoTracking()
            .Where(x => x.TenDN == taiKhoan.TenDN && x.NgaySuDung == null && x.MaXacNhanHash != null)
            .OrderByDescending(x => x.NgayTao)
            .FirstOrDefaultAsync();
        if (yeuCau is null || yeuCau.HanDung < bayGio)
            throw MaHetHan();

        // Tính lượt thử TRƯỚC khi so sánh, bằng một câu lệnh có điều kiện: gọi song song nhiều lần cũng không vượt quá số lần cho phép.
        var toiDa = Math.Max(1, _options.MaxCodeAttempts);
        var duocThu = await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.MaYeuCau == yeuCau.MaYeuCau && x.NgaySuDung == null && x.MaXacNhanHash != null && x.SoLanNhapMa < toiDa)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLanNhapMa, x => x.SoLanNhapMa + 1));
        if (duocThu != 1)
        {
            await _context.YeuCauDatLaiMatKhaus.Where(x => x.MaYeuCau == yeuCau.MaYeuCau && x.NgaySuDung == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgaySuDung, bayGio));
            throw new ApiException(StatusCodes.Status400BadRequest, "MA_XAC_NHAN_HET_LUOT",
                "Bạn đã nhập sai quá số lần cho phép. Vui lòng bấm gửi lại mã để nhận mã mới.", "ma");
        }

        var daNhap = yeuCau.SoLanNhapMa + 1;
        var dung = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(BamMaXacNhan(taiKhoan.TenDN, ma)), Encoding.ASCII.GetBytes(yeuCau.MaXacNhanHash!));
        if (!dung)
        {
            var conLai = toiDa - daNhap;
            await GhiNhatKyAsync(taiKhoan.TenDN, "NHAP_SAI_MA_DAT_LAI_MAT_KHAU", $"lan={daNhap}");
            await _context.SaveChangesAsync();
            throw new ApiException(StatusCodes.Status400BadRequest, "MA_XAC_NHAN_SAI",
                conLai > 0
                    ? $"Mã xác nhận không đúng. Bạn còn {conLai} lần thử."
                    : "Mã xác nhận không đúng. Bạn đã hết lượt thử, vui lòng bấm gửi lại mã để nhận mã mới.", "ma");
        }

        // Đúng mã: đổi lấy mã đặt lại dùng một lần (chỉ chạy được một lần dù gọi song song).
        var token = TaoToken();
        var daDoi = await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.MaYeuCau == yeuCau.MaYeuCau && x.NgaySuDung == null && x.MaXacNhanHash == yeuCau.MaXacNhanHash)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.TokenHash, BamToken(token))
                .SetProperty(x => x.MaXacNhanHash, (string?)null)
                .SetProperty(x => x.HanDung, bayGio.AddMinutes(Math.Max(1, _options.ResetTokenMinutes))));
        if (daDoi != 1)
            throw MaHetHan();

        await GhiNhatKyAsync(taiKhoan.TenDN, "XAC_NHAN_MA_DAT_LAI_MAT_KHAU");
        await _context.SaveChangesAsync();
        return new VerifyResetCodeResultDto { Token = token };
    }

    /// <summary>Tìm tài khoản khách theo email; báo lỗi rõ ràng nếu email chưa đăng ký.</summary>
    private async Task<TaiKhoan> TimTaiKhoanTheoEmailAsync(string email)
    {
        // Email duy nhất giữa các tài khoản khách (AuthService kiểm tra khi đăng ký/đổi email). Nếu dữ liệu cũ bị trùng thì KHÔNG đoán
        // - từ chối để không gửi mã chiếm quyền tài khoản nhầm người.
        var taiKhoans = await _context.TaiKhoans.Include(x => x.Khach)
            .Where(x => x.LoaiTaiKhoan == LoaiTaiKhoan.KHACH_HANG && x.Khach != null && x.Khach.Email == email)
            .Take(2)
            .ToListAsync();
        if (taiKhoans.Count == 0)
            throw new ApiException(StatusCodes.Status404NotFound, "EMAIL_CHUA_DANG_KY",
                "Email này chưa được đăng ký tài khoản. Vui lòng kiểm tra lại email hoặc đăng ký tài khoản mới.", "email");
        if (taiKhoans.Count > 1)
        {
            _logger.LogWarning("Yêu cầu quên mật khẩu cho email trùng ở nhiều tài khoản (từ chối, cần xử lý dữ liệu).");
            throw new ApiException(StatusCodes.Status409Conflict, "EMAIL_TRUNG",
                "Email này đang gắn với nhiều tài khoản nên không thể tự khôi phục. Vui lòng gọi hotline 1900 0001 để được hỗ trợ.", "email");
        }
        return taiKhoans[0];
    }

    private static ApiException MaHetHan()
        => new(StatusCodes.Status400BadRequest, "MA_XAC_NHAN_HET_HAN",
            "Mã xác nhận đã hết hạn hoặc không còn hiệu lực. Vui lòng bấm gửi lại mã để nhận mã mới.", "ma");

    // ---------------------------------------------------------------- Bước 3: đặt mật khẩu mới (bằng mã đặt lại cấp sau khi nhập đúng mã xác nhận)

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        // Kiểm tra mật khẩu mới TRƯỚC khi "đốt" mã: gõ mật khẩu quá ngắn không làm mất liên kết.
        KiemTraMatKhauMoi(request.MatKhauMoi);

        if (string.IsNullOrWhiteSpace(request.Token))
            throw KhongHopLe();

        var hash = BamToken(request.Token);
        var bayGio = DateTime.Now;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var yeuCau = await _context.YeuCauDatLaiMatKhaus.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (yeuCau is null || yeuCau.NgaySuDung is not null || yeuCau.HanDung < bayGio)
            throw KhongHopLe();

        // Đánh dấu đã dùng bằng MỘT câu lệnh có điều kiện: 2 người bấm cùng liên kết cùng lúc thì chỉ một người đổi được (số dòng = 1).
        var daDung = await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.MaYeuCau == yeuCau.MaYeuCau && x.NgaySuDung == null && x.HanDung >= bayGio)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgaySuDung, bayGio));
        if (daDung != 1)
            throw KhongHopLe();

        var taiKhoan = await _context.TaiKhoans.Include(x => x.Khach).FirstAsync(x => x.TenDN == yeuCau.TenDN);
        taiKhoan.MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhauMoi);
        taiKhoan.SoLanDangNhapSai = 0;
        // Đặt lại thành công cũng là cách lấy lại tài khoản bị khóa do nhập sai nhiều lần.
        taiKhoan.TrangThai = TrangThaiTaiKhoan.HOAT_DONG;
        // Mọi phiên đăng nhập cũ (refresh token) phải mất hiệu lực - người chiếm tài khoản không còn giữ được phiên.
        taiKhoan.RefreshToken = null;
        taiKhoan.RefreshTokenExpiry = null;

        await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.TenDN == taiKhoan.TenDN && x.NgaySuDung == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NgaySuDung, bayGio));

        await GhiNhatKyAsync(taiKhoan.TenDN, "DAT_LAI_MAT_KHAU");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        ThongBaoDoiMatKhau(taiKhoan);
    }

    // ---------------------------------------------------------------- Đổi mật khẩu (đã đăng nhập)

    public async Task ChangePasswordAsync(string tenDangNhap, ChangePasswordRequest request)
    {
        KiemTraMatKhauMoi(request.MatKhauMoi);
        if (string.IsNullOrEmpty(request.MatKhauCu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MAT_KHAU_CU", "Vui lòng nhập mật khẩu hiện tại.", nameof(request.MatKhauCu));

        var taiKhoan = await _context.TaiKhoans.Include(x => x.Khach).FirstOrDefaultAsync(x => x.TenDN == tenDangNhap)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "TAI_KHOAN_KHONG_HOP_LE", "Không tìm thấy tài khoản.");

        if (taiKhoan.TrangThai != TrangThaiTaiKhoan.HOAT_DONG)
            throw new ApiException(StatusCodes.Status423Locked, "TAI_KHOAN_BI_KHOA", "Tài khoản đang bị khóa.");

        if (!BCrypt.Net.BCrypt.Verify(request.MatKhauCu, taiKhoan.MatKhau))
        {
            // Đoán mật khẩu cũ bằng phiên đăng nhập bị đánh cắp cũng bị tính như đăng nhập sai (khóa sau 5 lần).
            taiKhoan.SoLanDangNhapSai++;
            if (taiKhoan.SoLanDangNhapSai >= SoLanSaiToiDa)
            {
                taiKhoan.TrangThai = TrangThaiTaiKhoan.BI_KHOA;
                await GhiNhatKyAsync(taiKhoan.TenDN, "TAI_KHOAN_BI_KHOA_TU_DONG", "Sai mat khau cu khi doi mat khau");
                await _context.SaveChangesAsync();
                throw new ApiException(StatusCodes.Status423Locked, "TAI_KHOAN_BI_KHOA", "Tài khoản đã bị khóa do nhập sai mật khẩu quá số lần cho phép.");
            }

            await GhiNhatKyAsync(taiKhoan.TenDN, "DOI_MAT_KHAU_THAT_BAI", $"Lan sai thu {taiKhoan.SoLanDangNhapSai}");
            await _context.SaveChangesAsync();
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_CU_KHONG_DUNG", "Mật khẩu hiện tại không đúng.", nameof(request.MatKhauCu));
        }

        if (BCrypt.Net.BCrypt.Verify(request.MatKhauMoi, taiKhoan.MatKhau))
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_MOI_TRUNG_CU", "Mật khẩu mới phải khác mật khẩu hiện tại.", nameof(request.MatKhauMoi));

        taiKhoan.MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhauMoi);
        taiKhoan.SoLanDangNhapSai = 0;
        taiKhoan.RefreshToken = null;              // các phiên đăng nhập khác (refresh token) mất hiệu lực
        taiKhoan.RefreshTokenExpiry = null;

        await GhiNhatKyAsync(taiKhoan.TenDN, "DOI_MAT_KHAU");
        await _context.SaveChangesAsync();

        ThongBaoDoiMatKhau(taiKhoan);
    }

    // ---------------------------------------------------------------- Hỗ trợ

    /// <summary>Chính sách mật khẩu dùng chung cho đặt lại và đổi mật khẩu.</summary>
    public static void KiemTraMatKhauMoi(string? matKhau)
    {
        if (string.IsNullOrEmpty(matKhau) || matKhau.Length < DoDaiToiThieu)
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_QUA_NGAN", $"Mật khẩu phải có ít nhất {DoDaiToiThieu} ký tự.", "matKhauMoi");
        if (matKhau.Length > DoDaiToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_QUA_DAI", $"Mật khẩu tối đa {DoDaiToiDa} ký tự.", "matKhauMoi");
        if (string.IsNullOrWhiteSpace(matKhau))
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_KHONG_HOP_LE", "Mật khẩu không được chỉ gồm khoảng trắng.", "matKhauMoi");
    }

    /// <summary>Mã xác nhận 6 chữ số ngẫu nhiên mật mã học (000000-999999).</summary>
    public static string TaoMaXacNhan() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>Băm mã xác nhận kèm tên đăng nhập. Mã chỉ có 1 triệu khả năng nên bắt buộc giới hạn số lần thử (AuthOptions.MaxCodeAttempts).</summary>
    public static string BamMaXacNhan(string tenDN, string ma) => BamToken($"otp:{tenDN}:{ma}");

    /// <summary>Mã đặt lại: 32 byte ngẫu nhiên mật mã học (256 bit), mã hóa base64url để đặt trong đường dẫn.</summary>
    public static string TaoToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Băm SHA-256 dạng hex - thứ duy nhất được lưu trong CSDL.</summary>
    public static string BamToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ApiException KhongHopLe()
        => new(StatusCodes.Status400BadRequest, "PHIEN_DAT_LAI_KHONG_HOP_LE", "Phiên đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã xác nhận mới.");

    /// <summary>Cắt khoảng trắng và kiểm tra định dạng email (bắt buộc). Số điện thoại KHÔNG được chấp nhận ở chức năng này.</summary>
    internal static string ChuanHoaEmail(string? email)
    {
        var mail = email?.Trim() ?? string.Empty;
        if (mail.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_THONG_TIN", "Vui lòng nhập email đã đăng ký.", "email");
        ContactValidator.KiemTraEmail(mail, "email");
        return mail;
    }

    private void ThongBaoDoiMatKhau(TaiKhoan taiKhoan)
    {
        var email = taiKhoan.Khach?.Email?.Trim();
        if (!string.IsNullOrEmpty(email) && _email.IsAvailable)
            _email.TryEnqueue(MauEmail.DaDoiMatKhau(email, taiKhoan.Khach!.HoTen, DateTime.Now));
    }

    private async Task GhiNhatKyAsync(string maTaiKhoan, string hanhDong, string? chiTiet = null)
    {
        // Ghi cùng SaveChanges với thao tác chính (không nuốt lỗi): không có dấu vết thì thao tác cũng không được thực hiện.
        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = maTaiKhoan,
            HanhDong = hanhDong,
            ThoiGian = DateTime.Now,
            ChiTiet = chiTiet
        });
        await Task.CompletedTask;
    }
}

/// <summary>Mẫu email: bản HTML (bảng + style nội tuyến để hiển thị ổn định trên các trình đọc mail) kèm bản văn bản thuần.</summary>
internal static class MauEmail
{
    private static string Khung(string tieuDe, string thanBan) => $@"<!doctype html><html lang=""vi""><body style=""margin:0;background:#f5f6f0;font-family:Segoe UI,Arial,sans-serif;color:#16241d"">
<table width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:32px 12px"">
<table width=""520"" cellpadding=""0"" cellspacing=""0"" style=""max-width:100%;background:#fff;border:1px solid #d7ddce;border-radius:16px;overflow:hidden"">
<tr><td style=""background:#1f4d3d;color:#fff;padding:22px 28px;font-size:20px;font-weight:700"">KLCN060 Hotel</td></tr>
<tr><td style=""padding:28px""><h2 style=""margin:0 0 12px;font-size:20px"">{tieuDe}</h2>{thanBan}</td></tr>
<tr><td style=""padding:16px 28px;background:#f5f6f0;color:#647a6c;font-size:12px"">Hotline hỗ trợ: 1900 0001 · Đà Lạt, Lâm Đồng<br>Đây là email tự động, vui lòng không trả lời.</td></tr>
</table></td></tr></table></body></html>";

    private static string ThoiGian(DateTime luc) => luc.ToString("HH:mm 'ngày' dd/MM/yyyy");

    public static EmailMessage MaXacNhan(string den, string hoTen, string ma, int soPhut, DateTime luc)
    {
        var ten = WebUtility.HtmlEncode(hoTen);
        var html = Khung("Mã xác nhận đặt lại mật khẩu", $@"<p>Xin chào <strong>{ten}</strong>,</p>
<p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản KLCN060 Hotel của bạn (lúc {ThoiGian(luc)}). Hãy nhập mã xác nhận dưới đây vào trang Quên mật khẩu. Mã có hiệu lực trong <strong>{soPhut} phút</strong>.</p>
<p style=""margin:24px 0;text-align:center""><span style=""display:inline-block;background:#f5f6f0;border:1px dashed #1f4d3d;color:#1f4d3d;font-size:32px;font-weight:700;letter-spacing:10px;padding:14px 24px 14px 34px;border-radius:12px"">{ma}</span></p>
<p style=""font-size:13px;color:#647a6c"">Không chia sẻ mã này cho bất kỳ ai, kể cả nhân viên khách sạn. Nếu bạn không yêu cầu, hãy bỏ qua email này - mật khẩu của bạn vẫn giữ nguyên và không ai đổi được mật khẩu nếu không có mã trên.</p>");
        var text = $@"Xin chào {hoTen},

Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản KLCN060 Hotel của bạn (lúc {ThoiGian(luc)}).
Mã xác nhận của bạn là: {ma}
Nhập mã này vào trang Quên mật khẩu (hiệu lực {soPhut} phút). Không chia sẻ mã cho bất kỳ ai.

Nếu bạn không yêu cầu, hãy bỏ qua email này - mật khẩu của bạn vẫn giữ nguyên.

KLCN060 Hotel - Hotline hỗ trợ: 1900 0001";
        return new EmailMessage(den, hoTen, "Mã xác nhận đặt lại mật khẩu KLCN060 Hotel", html, text);
    }

    public static EmailMessage DaDoiMatKhau(string den, string hoTen, DateTime luc)
    {
        var html = Khung("Mật khẩu đã được thay đổi", $@"<p>Xin chào <strong>{WebUtility.HtmlEncode(hoTen)}</strong>,</p>
<p>Mật khẩu tài khoản KLCN060 Hotel của bạn vừa được thay đổi lúc {ThoiGian(luc)}. Mọi phiên đăng nhập cũ đã bị đăng xuất.</p>
<p style=""font-size:13px;color:#647a6c"">Nếu không phải bạn thực hiện, hãy dùng chức năng &ldquo;Quên mật khẩu&rdquo; để lấy lại tài khoản ngay và gọi hotline 1900 0001.</p>");
        var text = $@"Xin chào {hoTen},

Mật khẩu tài khoản KLCN060 Hotel của bạn vừa được thay đổi lúc {ThoiGian(luc)}. Mọi phiên đăng nhập cũ đã bị đăng xuất.
Nếu không phải bạn thực hiện, hãy dùng chức năng ""Quên mật khẩu"" để lấy lại tài khoản ngay và gọi hotline 1900 0001.

KLCN060 Hotel";
        return new EmailMessage(den, hoTen, "Mật khẩu KLCN060 Hotel đã được thay đổi", html, text);
    }

    /// <summary>Gửi tới email CŨ khi email của tài khoản bị đổi: nếu không phải chủ tài khoản đổi thì họ biết để xử lý kịp.</summary>
    public static EmailMessage DaDoiEmail(string den, string hoTen, string emailMoi, DateTime luc)
    {
        var moi = WebUtility.HtmlEncode(MaAnEmail(emailMoi));
        var html = Khung("Email tài khoản đã được thay đổi", $@"<p>Xin chào <strong>{WebUtility.HtmlEncode(hoTen)}</strong>,</p>
<p>Email của tài khoản KLCN060 Hotel vừa được đổi sang <strong>{moi}</strong> lúc {ThoiGian(luc)}. Từ nay email này không còn nhận được thông báo của tài khoản.</p>
<p style=""font-size:13px;color:#647a6c"">Nếu không phải bạn thực hiện, hãy gọi hotline 1900 0001 ngay để được hỗ trợ khóa tài khoản.</p>");
        var text = $@"Xin chào {hoTen},

Email của tài khoản KLCN060 Hotel vừa được đổi sang {MaAnEmail(emailMoi)} lúc {ThoiGian(luc)}. Từ nay email này không còn nhận được thông báo của tài khoản.
Nếu không phải bạn thực hiện, hãy gọi hotline 1900 0001 ngay để được hỗ trợ.

KLCN060 Hotel";
        return new EmailMessage(den, hoTen, "Email tài khoản KLCN060 Hotel đã được thay đổi", html, text);
    }

    private static string MaAnEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? email : email[0] + new string('*', Math.Min(at - 1, 5)) + email[at..];
    }
}
