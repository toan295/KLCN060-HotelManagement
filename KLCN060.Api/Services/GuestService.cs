using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Guests;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Auth;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class GuestService : IGuestService
{
    private const int PageSizeToiDa = 100;

    private readonly KLCN060DbContext _context;
    private readonly IEmailQueue _emailQueue;
    private readonly IAuditTrail _audit;

    public GuestService(KLCN060DbContext context, IEmailQueue emailQueue, IAuditTrail audit)
    {
        _context = context;
        _emailQueue = emailQueue;
        _audit = audit;
    }

    public async Task<GuestDto> CreateAsync(GuestRequest request)
    {
        KiemTraHoTenSoDT(request.HoTen, request.SoDT);
        request.SoDT = ContactValidator.KiemTraSoDT(request.SoDT);
        request.DiaChi = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim();
        ContactValidator.KiemTraEmail(request.Email);
        KiemTraCccdKhongBiChe(request.CCCD);
        request.CCCD = ChuanHoaCccd(request.CCCD);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // [Khối 8 - P1-4] Transaction + khóa sinh mã theo tiền tố (xem MaCodeGenerator.GenerateNextAsync): 2 request tạo
        // đồng thời không còn sinh trùng mã rồi bị từ chối oan bằng 409 XUNG_DOT_DU_LIEU.
        // Khóa "KH" dùng chung với AuthService.RegisterAsync và lấy TRƯỚC kiểm tra trùng SĐT → 2 lễ tân tạo cùng 1 SĐT
        // đồng thời cũng không còn lọt qua (bảng Khachs không có unique index trên SoDT).
        await MaCodeGenerator.KhoaTienToAsync(_context, "KH");

        if (await _context.Khachs.AnyAsync(x => x.SoDT == request.SoDT.Trim()))
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã tồn tại, hãy tìm khách theo số điện thoại thay vì tạo mới.", "soDT");

        if (request.CCCD is not null && await _context.Khachs.AnyAsync(x => x.CCCD == request.CCCD))
            throw new ApiException(StatusCodes.Status409Conflict, "CCCD_DA_TON_TAI", "Số giấy tờ này đã thuộc về một khách hàng khác, hãy tìm khách đó thay vì tạo mới.", "cccd");

        var maHienCo = await _context.Khachs.Select(x => x.MaKhach).ToListAsync();

        var khach = new Khach
        {
            MaKhach = MaCodeGenerator.GenerateNext(maHienCo, "KH", 2),
            HoTen = request.HoTen.Trim(),
            SoDT = request.SoDT.Trim(),
            CCCD = request.CCCD,
            Email = ContactValidator.ChuanHoaEmail(request.Email),
            DiaChi = request.DiaChi
        };
        _context.Khachs.Add(khach);
        _audit.Ghi("TAO_KHACH", khach.MaKhach, $"hoTen={khach.HoTen}; soDT={khach.SoDT}; cccd={CheCccd(khach.CCCD)}; email={khach.Email}; diaChi={khach.DiaChi}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo khách hàng, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return ToDto(khach);
    }

    public async Task<GuestDto> UpdateAsync(string maKhach, GuestRequest request)
    {
        var khach = await TimKhachAsync(maKhach);
        KiemTraHoTenSoDT(request.HoTen, request.SoDT);
        request.SoDT = ContactValidator.KiemTraSoDT(request.SoDT);
        request.DiaChi = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim();
        ContactValidator.KiemTraEmail(request.Email);
        KiemTraCccdKhongBiChe(request.CCCD);
        request.CCCD = ChuanHoaCccd(request.CCCD);
        await KiemTraTrungSoDTAsync(maKhach, request.SoDT);
        if (request.CCCD is not null && await _context.Khachs.AnyAsync(x => x.CCCD == request.CCCD && x.MaKhach != maKhach))
            throw new ApiException(StatusCodes.Status409Conflict, "CCCD_DA_TON_TAI", "Số giấy tờ này đã thuộc về một khách hàng khác.", "cccd");

        // Email của khách CÓ TÀI KHOẢN là kênh khôi phục mật khẩu: phải duy nhất giữa các tài khoản (giống lúc khách tự đổi) và
        // chủ tài khoản được báo qua email cũ - lễ tân đổi email không thể âm thầm chuyển quyền khôi phục sang người khác.
        var emailMoi = ContactValidator.ChuanHoaEmail(request.Email);
        var coTaiKhoan = await _context.TaiKhoans.AnyAsync(x => x.MaKhach == maKhach);
        if (coTaiKhoan && emailMoi is not null && await AuthService.EmailThuocTaiKhoanKhacAsync(_context, emailMoi, maKhach))
            throw new ApiException(StatusCodes.Status409Conflict, "EMAIL_DA_TON_TAI", "Email đã được dùng cho một tài khoản khác.", "email");
        var emailCu = khach.Email;
        var thayDoi = MoTaThayDoi(khach, request, emailMoi);

        khach.HoTen = request.HoTen.Trim();
        khach.SoDT = request.SoDT.Trim();
        khach.CCCD = request.CCCD;
        khach.Email = emailMoi;
        khach.DiaChi = request.DiaChi;
        _audit.Ghi("CAP_NHAT_KHACH", maKhach, thayDoi.Length == 0 ? "không có thay đổi" : thayDoi);

        await _context.SaveChangesAsync();
        if (coTaiKhoan)
            await SauKhiDoiEmailAsync(khach, emailCu);
        return ToDto(khach);
    }

    public async Task<GuestDto> GetMeAsync(string? maKhach)
    {
        return ToDto(await TimKhachAsync(maKhach ?? string.Empty));
    }

    public async Task<GuestDto> UpdateMeAsync(string? maKhach, UpdateProfileRequest request)
    {
        if (request.TruongKhac is not null && request.TruongKhac.Keys.Any(k => k.Equals("cccd", StringComparison.OrdinalIgnoreCase)))
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_DUOC_SUA_CCCD", "Khách hàng không được tự sửa CCCD, thông tin này chỉ do lễ tân cập nhật khi nhận phòng.", "cccd");

        var khach = await TimKhachAsync(maKhach ?? string.Empty);
        KiemTraHoTenSoDT(request.HoTen, request.SoDT);
        request.SoDT = ContactValidator.KiemTraSoDT(request.SoDT);
        request.DiaChi = string.IsNullOrWhiteSpace(request.DiaChi) ? null : request.DiaChi.Trim();
        ContactValidator.KiemTraEmail(request.Email);
        await KiemTraTrungSoDTAsync(khach.MaKhach, request.SoDT);
        // [Khối 8 - K8-9] Khách tự đổi email sang email của TÀI KHOẢN khác → đăng nhập bằng email trở nên mơ hồ.
        if (!string.IsNullOrWhiteSpace(request.Email) && await AuthService.EmailThuocTaiKhoanKhacAsync(_context, request.Email, khach.MaKhach))
            throw new ApiException(StatusCodes.Status409Conflict, "EMAIL_DA_TON_TAI", "Email đã được dùng cho một tài khoản khác.", "email");

        // Lưu ý: TaiKhoan.TenDN (tên đăng nhập của khách) được cấp theo SĐT lúc đăng ký và là khóa chính,
        // nên đổi SoDT ở đây KHÔNG đổi tên đăng nhập.
        var emailCu = khach.Email;
        khach.HoTen = request.HoTen.Trim();
        khach.SoDT = request.SoDT.Trim();
        khach.Email = ContactValidator.ChuanHoaEmail(request.Email);
        khach.DiaChi = request.DiaChi;

        await _context.SaveChangesAsync();
        await SauKhiDoiEmailAsync(khach, emailCu);
        return ToDto(khach);
    }

    private static bool LaDoiEmail(string? cu, string? moi)
        => !string.Equals((cu ?? string.Empty).Trim(), (moi ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sau khi email của TÀI KHOẢN đổi: báo email cũ; thu hồi các liên kết đặt lại mật khẩu đang chờ (đã gửi tới email cũ, không còn
    /// hợp lệ để chiếm tài khoản).
    /// </summary>
    private async Task SauKhiDoiEmailAsync(Khach khach, string? emailCu)
    {
        if (!LaDoiEmail(emailCu, khach.Email)) return;

        ThongBaoDoiEmail(khach.HoTen, emailCu, khach.Email);

        var bayGio = DateTime.Now;
        await _context.YeuCauDatLaiMatKhaus
            .Where(x => x.NgaySuDung == null && _context.TaiKhoans.Any(t => t.MaKhach == khach.MaKhach && t.TenDN == x.TenDN))
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.NgaySuDung, bayGio));
    }

    /// <summary>Báo cho email CŨ khi email của tài khoản đổi sang địa chỉ khác (dấu hiệu chiếm tài khoản nếu không phải chủ đổi).</summary>
    private void ThongBaoDoiEmail(string hoTen, string? emailCu, string? emailMoi)
    {
        if (string.IsNullOrWhiteSpace(emailCu) || string.IsNullOrWhiteSpace(emailMoi)) return;
        if (string.Equals(emailCu.Trim(), emailMoi.Trim(), StringComparison.OrdinalIgnoreCase)) return;
        if (_emailQueue.IsAvailable)
            _emailQueue.TryEnqueue(MauEmail.DaDoiEmail(emailCu.Trim(), hoTen, emailMoi.Trim(), DateTime.Now));
    }

    private async Task<Khach> TimKhachAsync(string maKhach)
    {
        var khach = await _context.Khachs.FirstOrDefaultAsync(x => x.MaKhach == maKhach);
        if (khach is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHACH_HANG", "Không tìm thấy khách hàng.");
        return khach;
    }

    private async Task KiemTraTrungSoDTAsync(string maKhach, string soDT)
    {
        var sdt = soDT.Trim();
        if (await _context.Khachs.AnyAsync(x => x.SoDT == sdt && x.MaKhach != maKhach))
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã được khách hàng khác sử dụng.", "soDT");
    }

    private static void KiemTraHoTenSoDT(string? hoTen, string? soDT)
    {
        if (string.IsNullOrWhiteSpace(hoTen))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_HO_TEN", "Họ tên là bắt buộc.", "hoTen");
        var ten = hoTen.Trim();
        if (ten.Length < 2 || !ten.Any(char.IsLetter))
            throw new ApiException(StatusCodes.Status400BadRequest, "HO_TEN_KHONG_HOP_LE", "Họ tên tối thiểu 2 ký tự và phải có chữ cái.", "hoTen");
        if (string.IsNullOrWhiteSpace(soDT))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_SO_DIEN_THOAI", "Số điện thoại là bắt buộc.", "soDT");
    }

    /// <summary>Che số giấy tờ trong danh sách: chỉ giữ 4 ký tự cuối (VD "********1234").</summary>
    internal static string? CheCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return cccd;
        var s = cccd.Trim();
        return s.Length <= 4 ? new string('*', s.Length) : new string('*', s.Length - 4) + s[^4..];
    }

    /// <summary>Số đã bị che (chứa dấu *) không phải giấy tờ thật: chặn để danh sách che không vô tình ghi đè hồ sơ khi sửa.</summary>
    private static void KiemTraCccdKhongBiChe(string? cccd)
    {
        if (!string.IsNullOrEmpty(cccd) && cccd.Contains('*'))
            throw new ApiException(StatusCodes.Status400BadRequest, "CCCD_KHONG_HOP_LE", "Số giấy tờ không hợp lệ (đang là số đã che): hãy mở hồ sơ chi tiết của khách để xem/sửa số đầy đủ.", "cccd");
    }

    private static GuestDto ToDto(Khach x) => new()
    {
        MaKhach = x.MaKhach,
        HoTen = x.HoTen,
        SoDT = x.SoDT,
        CCCD = x.CCCD,
        Email = x.Email,
        DiaChi = x.DiaChi
    };
}
