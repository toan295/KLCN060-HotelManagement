using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.RoomTypes;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class RoomTypeService : IRoomTypeService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;

    public RoomTypeService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
    }

    /// <summary>Đơn giá là quyết định giá: chỉ người có quyền QUAN_LY_GIA được đặt/đổi, dù có quyền QUAN_LY_PHONG (sửa các thông tin khác của loại phòng).</summary>
    private async Task KiemTraQuyenDatGiaAsync()
    {
        var tenDN = _audit.TenDN;
        if (string.IsNullOrEmpty(tenDN) || !await _permissions.CoQuyenAsync(tenDN, Permissions.QuanLyGia))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_DAT_GIA", "Chỉ người có quyền quản lý giá (QUAN_LY_GIA) được đặt hoặc đổi đơn giá loại phòng.", "donGia");
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
        await KiemTraQuyenDatGiaAsync();
        await ChuanHoaVaKiemTraAsync(request, null);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // [Khối 8 - P1-4] Transaction + khóa sinh mã theo tiền tố (xem MaCodeGenerator.GenerateNextAsync): 2 request tạo
        // đồng thời không còn sinh trùng mã rồi bị từ chối oan bằng 409 XUNG_DOT_DU_LIEU.
        var maLoai = await MaCodeGenerator.GenerateNextAsync(_context, _context.LoaiPhongs.Select(x => x.MaLoai), "LP", 2);

        var loaiPhong = new LoaiPhong
        {
            MaLoai = maLoai,
            TenLoai = request.TenLoai,
            SoNguoiTieuChuan = request.SoNguoiTieuChuan,
            DonGia = request.DonGia,
            PhuThu = request.PhuThu,
            DienTich = request.DienTich,
            LoaiGiuong = request.LoaiGiuong,
            TienNghi = request.TienNghi,
            MoTa = request.MoTa
        };

        _context.LoaiPhongs.Add(loaiPhong);
        _audit.Ghi("TAO_LOAI_PHONG", maLoai, $"ten={request.TenLoai}; sucChua={request.SoNguoiTieuChuan}; donGia={Math.Round(request.DonGia, 0)}; phuThu={Math.Round(request.PhuThu, 0)}; dienTich={request.DienTich}; giuong={request.LoaiGiuong}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Hiem gap: 2 request tao loai phong dong thoi sinh trung MaLoai (rang buoc PK bat o tang DB).
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo loại phòng, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return ToDto(loaiPhong);
    }

    public async Task<RoomTypeDto> UpdateAsync(string maLoai, RoomTypeRequest request)
    {
        var loaiPhong = await TimLoaiPhongAsync(maLoai);
        await ChuanHoaVaKiemTraAsync(request, maLoai);

        if (request.DonGia != loaiPhong.DonGia)
        {
            await KiemTraQuyenDatGiaAsync();
            _audit.Ghi("DOI_GIA_LOAI_PHONG", maLoai, $"donGia={Math.Round(loaiPhong.DonGia, 0)} -> {Math.Round(request.DonGia, 0)}");
        }

        var thayDoi = new List<string>();
        void So(string ten, string? a, string? b) { if (!string.Equals(a ?? "", b ?? "", StringComparison.Ordinal)) thayDoi.Add($"{ten}: {(string.IsNullOrEmpty(a) ? "(trống)" : a)} -> {(string.IsNullOrEmpty(b) ? "(trống)" : b)}"); }
        So("ten", loaiPhong.TenLoai, request.TenLoai);
        So("sucChua", loaiPhong.SoNguoiTieuChuan.ToString(), request.SoNguoiTieuChuan.ToString());
        So("donGia", Math.Round(loaiPhong.DonGia, 0).ToString(), Math.Round(request.DonGia, 0).ToString());
        So("phuThu", Math.Round(loaiPhong.PhuThu, 0).ToString(), Math.Round(request.PhuThu, 0).ToString());
        So("dienTich", loaiPhong.DienTich?.ToString(), request.DienTich?.ToString());
        So("giuong", loaiPhong.LoaiGiuong, request.LoaiGiuong);
        So("tienNghi", loaiPhong.TienNghi, request.TienNghi);
        So("moTa", loaiPhong.MoTa, request.MoTa);
        if (thayDoi.Count > 0) _audit.Ghi("SUA_LOAI_PHONG", maLoai, string.Join("; ", thayDoi));

        loaiPhong.TenLoai = request.TenLoai;
        loaiPhong.SoNguoiTieuChuan = request.SoNguoiTieuChuan;
        loaiPhong.DonGia = request.DonGia;
        loaiPhong.PhuThu = request.PhuThu;
        loaiPhong.DienTich = request.DienTich;
        loaiPhong.LoaiGiuong = request.LoaiGiuong;
        loaiPhong.TienNghi = request.TienNghi;
        loaiPhong.MoTa = request.MoTa;

        await _context.SaveChangesAsync();
        return ToDto(loaiPhong);
    }

    public async Task DeleteAsync(string maLoai)
    {
        var loaiPhong = await TimLoaiPhongAsync(maLoai);
        var (duoc, lyDo) = await CoTheXoaLoaiAsync(maLoai);
        if (!duoc)
            throw new ApiException(StatusCodes.Status409Conflict, "LOAI_PHONG_DANG_DUOC_SU_DUNG", lyDo!);
        _audit.Ghi("XOA_LOAI_PHONG", maLoai, $"ten={loaiPhong.TenLoai}; donGia={Math.Round(loaiPhong.DonGia, 0)}");
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
        if (guests < 1)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_KHACH_KHONG_HOP_LE", "Số khách phải từ 1 trở lên.", "guests");
        if (checkin < DateOnly.FromDateTime(DateTime.Now))
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_NHAN_TRONG_QUA", "Ngày nhận phòng không được ở quá khứ.", "checkin");

        var maPhongKhongTrong = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, checkin, checkout);

        var loaiPhongs = await _context.LoaiPhongs
            .Where(lp => lp.SoNguoiTieuChuan >= guests)
            .Include(lp => lp.Phongs)
            .OrderBy(lp => lp.MaLoai)
            .ToListAsync();

        // Giá hiển thị cho khách = đúng giá sẽ ghi vào phiếu đặt (theo bảng giá đang bật của Quản lý, cho đặt phòng cá nhân).
        var quyTacGia = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);

        return loaiPhongs.Select(lp =>
        {
            var gia = GiaPhongCalculator.Tinh(lp, checkin, checkout, LoaiDatPhong.CA_NHAN, quyTacGia);
            return new RoomTypeAvailabilityDto
            {
                MaLoai = lp.MaLoai,
                TenLoai = lp.TenLoai,
                SoNguoiTieuChuan = lp.SoNguoiTieuChuan,
                DonGia = gia.DonGiaBinhQuan,
                DonGiaCoBan = lp.DonGia,
                SoDem = gia.TheoDem.Count,
                TongTienDuKien = gia.TongTien,
                PhuThu = lp.PhuThu,
                SoPhongTrong = lp.Phongs.Count(p => p.TinhTrang != TinhTrangPhong.OOO && !maPhongKhongTrong.Contains(p.MaPhong))
            };
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
        PhuThu = x.PhuThu,
        DienTich = x.DienTich,
        LoaiGiuong = x.LoaiGiuong,
        TienNghi = x.TienNghi,
        MoTa = x.MoTa
    };
}
