using KLCN060.Api.DTOs.Rooms;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class RoomService : IRoomService
{
    private readonly KLCN060DbContext _context;

    public RoomService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<RoomDto>> GetAllAsync()
    {
        return await _context.Phongs
            .Include(x => x.LoaiPhong)
            .OrderBy(x => x.MaPhong)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<RoomDto> CreateAsync(RoomRequest request)
    {
        await KiemTraLoaiPhongTonTaiAsync(request.MaLoai);

        var maPhong = $"P{request.TenPhong}";
        if (await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TON_TAI", "Phòng với tên này đã tồn tại.", nameof(request.TenPhong));

        var tinhTrang = ParseTinhTrang(request.TinhTrang ?? nameof(TinhTrangPhong.VC));

        var phong = new Phong
        {
            MaPhong = maPhong,
            TenPhong = request.TenPhong,
            Tang = request.Tang,
            MaLoai = request.MaLoai,
            TinhTrang = tinhTrang
        };

        _context.Phongs.Add(phong);
        await _context.SaveChangesAsync();

        return await LayTheoMaAsync(maPhong);
    }

    public async Task<RoomDto> UpdateAsync(string maPhong, RoomRequest request)
    {
        var phong = await TimPhongAsync(maPhong);
        await KiemTraLoaiPhongTonTaiAsync(request.MaLoai);

        phong.TenPhong = request.TenPhong;
        phong.Tang = request.Tang;
        phong.MaLoai = request.MaLoai;
        if (!string.IsNullOrEmpty(request.TinhTrang))
            phong.TinhTrang = ParseTinhTrang(request.TinhTrang);

        await _context.SaveChangesAsync();
        return await LayTheoMaAsync(maPhong);
    }

    public async Task DeleteAsync(string maPhong)
    {
        var phong = await TimPhongAsync(maPhong);
        _context.Phongs.Remove(phong);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_SU_DUNG", "Không thể xóa phòng vì đang có dữ liệu liên quan (đặt phòng, cơ sở vật chất...).");
        }
    }

    // Sơ đồ chuyển trạng thái buồng phòng hợp lệ. Cố ý KHÔNG có OC -> VC/VI: phòng đang có khách phải qua OD
    // (cần dọn) rồi mới về trống; phòng chỉ thành OC qua check-in (không đặt tay), trả phòng (OC/OD -> VD) do luồng check-out xử lý, không đi qua endpoint này.
    private static readonly Dictionary<TinhTrangPhong, TinhTrangPhong[]> ChuyenTrangThaiHopLe = new()
    {
        [TinhTrangPhong.VD] = new[] { TinhTrangPhong.VC, TinhTrangPhong.OOO },
        [TinhTrangPhong.VC] = new[] { TinhTrangPhong.VI, TinhTrangPhong.VD, TinhTrangPhong.OOO },
        [TinhTrangPhong.VI] = new[] { TinhTrangPhong.VD, TinhTrangPhong.OOO },
        [TinhTrangPhong.OC] = new[] { TinhTrangPhong.OD },
        [TinhTrangPhong.OD] = new[] { TinhTrangPhong.OC, TinhTrangPhong.VD },
        [TinhTrangPhong.OOO] = new[] { TinhTrangPhong.VD, TinhTrangPhong.VC }
    };

    public async Task<RoomDto> UpdateStatusAsync(string maPhong, string tinhTrang, string? ghiChu, string tenDN)
    {
        var phong = await TimPhongAsync(maPhong);
        var moi = ParseTinhTrang(tinhTrang);
        var cu = phong.TinhTrang;

        if (!ChuyenTrangThaiHopLe[cu].Contains(moi))
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS_TRANSITION",
                $"Không thể chuyển phòng từ {cu} sang {moi}. Các trạng thái được phép từ {cu}: {string.Join(", ", ChuyenTrangThaiHopLe[cu])}.", "tinhTrang");

        phong.TinhTrang = moi;

        if (!string.IsNullOrEmpty(tenDN))
        {
            _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
            {
                MaTaiKhoan = tenDN,
                HanhDong = "DOI_TINH_TRANG_PHONG",
                DoiTuongTacDong = maPhong,
                ThoiGian = DateTime.Now,
                ChiTiet = string.IsNullOrWhiteSpace(ghiChu) ? $"{cu} -> {moi}" : $"{cu} -> {moi}; {ghiChu}"
            });
        }

        await _context.SaveChangesAsync();
        return await LayTheoMaAsync(maPhong);
    }

    private async Task<Phong> TimPhongAsync(string maPhong)
    {
        var phong = await _context.Phongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong);
        if (phong is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        return phong;
    }

    private async Task<RoomDto> LayTheoMaAsync(string maPhong)
    {
        var phong = await _context.Phongs.Include(x => x.LoaiPhong).FirstAsync(x => x.MaPhong == maPhong);
        return ToDto(phong);
    }

    private async Task KiemTraLoaiPhongTonTaiAsync(string maLoai)
    {
        if (!await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == maLoai))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.", nameof(maLoai));
    }

    private static TinhTrangPhong ParseTinhTrang(string tinhTrang)
    {
        if (!Enum.TryParse<TinhTrangPhong>(tinhTrang, ignoreCase: true, out var ketQua))
            throw new ApiException(StatusCodes.Status400BadRequest, "TINH_TRANG_KHONG_HOP_LE", "Tình trạng phòng không hợp lệ. Chỉ chấp nhận: VC, VD, VI, OC, OD, OOO.", "tinhTrang");
        return ketQua;
    }

    private static RoomDto ToDto(Phong x) => new()
    {
        MaPhong = x.MaPhong,
        TenPhong = x.TenPhong,
        Tang = x.Tang,
        TinhTrang = x.TinhTrang.ToString(),
        MaLoai = x.MaLoai,
        TenLoaiPhong = x.LoaiPhong.TenLoai
    };
}
