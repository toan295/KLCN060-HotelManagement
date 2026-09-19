using KLCN060.Api.DTOs.Guests;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class GuestService : IGuestService
{
    private const int PageSizeToiDa = 100;

    private readonly KLCN060DbContext _context;

    public GuestService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<(List<GuestDto> Items, int TotalItems)> SearchAsync(string? q, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, PageSizeToiDa);

        var query = _context.Khachs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var tuKhoa = q.Trim();
            query = query.Where(x => x.HoTen.Contains(tuKhoa) || x.SoDT.Contains(tuKhoa) || (x.CCCD != null && x.CCCD.Contains(tuKhoa)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.MaKhach)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToListAsync();

        return (items, total);
    }

    public async Task<GuestDetailDto> GetDetailAsync(string maKhach)
    {
        var khach = await TimKhachAsync(maKhach);

        var lichSu = await _context.PhieuDatPhongs
            .Where(x => x.MaKhach == maKhach)
            .OrderByDescending(x => x.NgayDonDuKien)
            .Select(x => new GuestStayHistoryDto
            {
                MaPhieuDat = x.MaPhieuDat,
                TrangThai = x.TrangThai.ToString(),
                NgayDonDuKien = x.NgayDonDuKien,
                NgayTraDuKien = x.NgayTraDuKien,
                TongTien = x.ChiTietPhieuDats.Sum(ct => (decimal?)ct.ThanhTien) ?? 0
            })
            .ToListAsync();

        return new GuestDetailDto
        {
            MaKhach = khach.MaKhach,
            HoTen = khach.HoTen,
            SoDT = khach.SoDT,
            CCCD = khach.CCCD,
            Email = khach.Email,
            DiaChi = khach.DiaChi,
            LichSuLuuTru = lichSu
        };
    }

    public async Task<GuestDto> CreateAsync(GuestRequest request)
    {
        KiemTraHoTenSoDT(request.HoTen, request.SoDT);

        if (await _context.Khachs.AnyAsync(x => x.SoDT == request.SoDT.Trim()))
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_DA_TON_TAI", "Số điện thoại đã tồn tại, hãy tìm khách theo số điện thoại thay vì tạo mới.", "soDT");

        var maHienCo = await _context.Khachs.Select(x => x.MaKhach).ToListAsync();

        var khach = new Khach
        {
            MaKhach = MaCodeGenerator.GenerateNext(maHienCo, "KH", 2),
            HoTen = request.HoTen.Trim(),
            SoDT = request.SoDT.Trim(),
            CCCD = request.CCCD,
            Email = request.Email,
            DiaChi = request.DiaChi
        };
        _context.Khachs.Add(khach);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo khách hàng, vui lòng thử lại.");
        }

        return ToDto(khach);
    }

    public async Task<GuestDto> UpdateAsync(string maKhach, GuestRequest request)
    {
        var khach = await TimKhachAsync(maKhach);
        KiemTraHoTenSoDT(request.HoTen, request.SoDT);
        await KiemTraTrungSoDTAsync(maKhach, request.SoDT);

        khach.HoTen = request.HoTen.Trim();
        khach.SoDT = request.SoDT.Trim();
        khach.CCCD = request.CCCD;
        khach.Email = request.Email;
        khach.DiaChi = request.DiaChi;

        await _context.SaveChangesAsync();
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
        await KiemTraTrungSoDTAsync(khach.MaKhach, request.SoDT);

        // Lưu ý: TaiKhoan.TenDN (tên đăng nhập của khách) được cấp theo SĐT lúc đăng ký và là khóa chính,
        // nên đổi SoDT ở đây KHÔNG đổi tên đăng nhập.
        khach.HoTen = request.HoTen.Trim();
        khach.SoDT = request.SoDT.Trim();
        khach.Email = request.Email;
        khach.DiaChi = request.DiaChi;

        await _context.SaveChangesAsync();
        return ToDto(khach);
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
        if (string.IsNullOrWhiteSpace(soDT))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_SO_DIEN_THOAI", "Số điện thoại là bắt buộc.", "soDT");
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
