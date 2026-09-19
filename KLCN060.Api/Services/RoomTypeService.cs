using KLCN060.Api.DTOs.RoomTypes;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class RoomTypeService : IRoomTypeService
{
    private readonly KLCN060DbContext _context;

    public RoomTypeService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<RoomTypeDto>> GetAllAsync()
    {
        return await _context.LoaiPhongs
            .OrderBy(x => x.MaLoai)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<RoomTypeDto> GetByIdAsync(string maLoai)
    {
        var loaiPhong = await TimLoaiPhongAsync(maLoai);
        return ToDto(loaiPhong);
    }

    public async Task<RoomTypeDto> CreateAsync(RoomTypeRequest request)
    {
        var maHienCo = await _context.LoaiPhongs.Select(x => x.MaLoai).ToListAsync();
        var maLoai = MaCodeGenerator.GenerateNext(maHienCo, "LP", 2);

        var loaiPhong = new LoaiPhong
        {
            MaLoai = maLoai,
            TenLoai = request.TenLoai,
            SoNguoiTieuChuan = request.SoNguoiTieuChuan,
            DonGia = request.DonGia,
            PhuThu = request.PhuThu
        };

        _context.LoaiPhongs.Add(loaiPhong);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hiem gap: 2 request tao loai phong dong thoi sinh trung MaLoai (rang buoc PK bat o tang DB).
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo loại phòng, vui lòng thử lại.");
        }

        return ToDto(loaiPhong);
    }

    public async Task<RoomTypeDto> UpdateAsync(string maLoai, RoomTypeRequest request)
    {
        var loaiPhong = await TimLoaiPhongAsync(maLoai);

        loaiPhong.TenLoai = request.TenLoai;
        loaiPhong.SoNguoiTieuChuan = request.SoNguoiTieuChuan;
        loaiPhong.DonGia = request.DonGia;
        loaiPhong.PhuThu = request.PhuThu;

        await _context.SaveChangesAsync();
        return ToDto(loaiPhong);
    }

    public async Task DeleteAsync(string maLoai)
    {
        var loaiPhong = await TimLoaiPhongAsync(maLoai);
        _context.LoaiPhongs.Remove(loaiPhong);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "LOAI_PHONG_DANG_DUOC_SU_DUNG", "Không thể xóa loại phòng vì vẫn còn phòng thuộc loại này.");
        }
    }

    public async Task<List<RoomTypeAvailabilityDto>> GetAvailabilityAsync(DateOnly checkin, DateOnly checkout, int guests)
    {
        if (checkout <= checkin)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày nhận.");

        var maPhongKhongTrong = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, checkin, checkout);

        var loaiPhongs = await _context.LoaiPhongs
            .Where(lp => lp.SoNguoiTieuChuan >= guests)
            .Include(lp => lp.Phongs)
            .OrderBy(lp => lp.MaLoai)
            .ToListAsync();

        return loaiPhongs.Select(lp => new RoomTypeAvailabilityDto
        {
            MaLoai = lp.MaLoai,
            TenLoai = lp.TenLoai,
            SoNguoiTieuChuan = lp.SoNguoiTieuChuan,
            DonGia = lp.DonGia,
            PhuThu = lp.PhuThu,
            SoPhongTrong = lp.Phongs.Count(p => p.TinhTrang != TinhTrangPhong.OOO && !maPhongKhongTrong.Contains(p.MaPhong))
        }).ToList();
    }

    private async Task<LoaiPhong> TimLoaiPhongAsync(string maLoai)
    {
        var loaiPhong = await _context.LoaiPhongs.FirstOrDefaultAsync(x => x.MaLoai == maLoai);
        if (loaiPhong is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.");
        return loaiPhong;
    }

    private static RoomTypeDto ToDto(LoaiPhong x) => new()
    {
        MaLoai = x.MaLoai,
        TenLoai = x.TenLoai,
        SoNguoiTieuChuan = x.SoNguoiTieuChuan,
        DonGia = x.DonGia,
        PhuThu = x.PhuThu
    };
}
