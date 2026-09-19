using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class ShiftHandoverService : IShiftHandoverService
{
    private readonly KLCN060DbContext _context;

    public ShiftHandoverService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<ShiftHandoverDto> CreateAsync(ShiftHandoverRequest request, CurrentUser user)
    {
        if (string.IsNullOrWhiteSpace(request.MaTaiKhoanNhan))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_NGUOI_NHAN", "Cần chỉ định tài khoản nhận ca.", "maTaiKhoanNhan");
        if (request.MaTaiKhoanNhan == user.TenDN)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGUOI_NHAN_TRUNG_NGUOI_GIAO", "Không thể bàn giao ca cho chính mình.", "maTaiKhoanNhan");
        if (request.TongTienMatCuoiCa < 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE", "Tiền mặt cuối ca không được âm.", "tongTienMatCuoiCa");
        if (request.TongTienMatDauCa < 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_TIEN_KHONG_HOP_LE", "Tiền mặt đầu ca không được âm.", "tongTienMatDauCa");

        var nguoiNhan = await _context.TaiKhoans
            .Include(x => x.NhanVien).ThenInclude(nv => nv!.VaiTro)
            .FirstOrDefaultAsync(x => x.TenDN == request.MaTaiKhoanNhan)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_TAI_KHOAN", "Không tìm thấy tài khoản nhận ca.", "maTaiKhoanNhan");
        if (nguoiNhan.LoaiTaiKhoan != LoaiTaiKhoan.NHAN_VIEN || nguoiNhan.TrangThai != TrangThaiTaiKhoan.HOAT_DONG
            || nguoiNhan.NhanVien?.VaiTro.TenVaiTro is not ("LE_TAN" or "QUAN_LY"))
            throw new ApiException(StatusCodes.Status400BadRequest, "NGUOI_NHAN_KHONG_HOP_LE", "Người nhận ca phải là tài khoản lễ tân hoặc quản lý đang hoạt động.", "maTaiKhoanNhan");

        var bayGio = DateTime.Now;
        var lanTruoc = await _context.NhatKyBanGiaoCas.AsNoTracking()
            .OrderByDescending(x => x.ThoiGianBanGiao).ThenByDescending(x => x.MaBanGiao)
            .FirstOrDefaultAsync();

        var dauCa = lanTruoc?.TongTienMatCuoiCa ?? request.TongTienMatDauCa ?? 0m;
        var tuLuc = lanTruoc?.ThoiGianBanGiao ?? bayGio.Date;

        var soPhieu = await _context.HoaDons.CountAsync(x => x.NgayLap > tuLuc && x.NgayLap <= bayGio);
        var thuTienMat = await _context.ChiTietThanhToans
            .Where(x => x.HinhThucThanhToan == HinhThucThanhToan.TIEN_MAT && x.ThoiGianThanhToan > tuLuc && x.ThoiGianThanhToan <= bayGio)
            .SumAsync(x => (decimal?)x.SoTien) ?? 0m;

        var entity = new NhatKyBanGiaoCa
        {
            MaTaiKhoanGiao = user.TenDN,
            MaTaiKhoanNhan = request.MaTaiKhoanNhan,
            ThoiGianBanGiao = bayGio,
            TongTienMatDauCa = dauCa,
            TongTienMatCuoiCa = request.TongTienMatCuoiCa,
            SoLuongPhieuTrongCa = soPhieu,
            GhiChu = request.GhiChu
        };
        _context.NhatKyBanGiaoCas.Add(entity);
        await _context.SaveChangesAsync();

        var dto = ToDto(entity);
        dto.TienMatDuKien = dauCa + thuTienMat;
        dto.ChenhLech = request.TongTienMatCuoiCa - dto.TienMatDuKien;
        return dto;
    }

    public async Task<List<ShiftHandoverDto>> GetAsync(DateOnly? tuNgay, DateOnly? denNgay)
    {
        if (tuNgay is not null && denNgay is not null && denNgay < tuNgay)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "denNgay phải sau hoặc bằng tuNgay.", "denNgay");

        var q = _context.NhatKyBanGiaoCas.AsNoTracking().AsQueryable();
        if (tuNgay is { } tu)
        {
            var tuLuc = tu.ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.ThoiGianBanGiao >= tuLuc);
        }
        if (denNgay is { } den)
        {
            var denLuc = den.AddDays(1).ToDateTime(TimeOnly.MinValue);
            q = q.Where(x => x.ThoiGianBanGiao < denLuc);
        }

        var ds = await q.OrderByDescending(x => x.ThoiGianBanGiao).ThenByDescending(x => x.MaBanGiao).ToListAsync();
        return ds.Select(ToDto).ToList();
    }

    private static ShiftHandoverDto ToDto(NhatKyBanGiaoCa x) => new()
    {
        MaBanGiao = x.MaBanGiao,
        MaTaiKhoanGiao = x.MaTaiKhoanGiao,
        MaTaiKhoanNhan = x.MaTaiKhoanNhan,
        ThoiGianBanGiao = x.ThoiGianBanGiao,
        TongTienMatDauCa = x.TongTienMatDauCa,
        TongTienMatCuoiCa = x.TongTienMatCuoiCa,
        SoLuongPhieuTrongCa = x.SoLuongPhieuTrongCa,
        GhiChu = x.GhiChu
    };
}
