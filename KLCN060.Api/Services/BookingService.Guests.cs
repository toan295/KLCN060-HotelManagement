using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Người ở cùng (khai báo lưu trú): lễ tân thêm/xóa trong phiếu đặt phòng còn hiệu lực hoặc đang ở.</summary>
public partial class BookingService
{
    private const int SoKhachDiCungToiDa = 20;

    private static CompanionDto ToCompanionDto(KhachDiCung x) => new()
    {
        MaKhachDiCung = x.MaKhachDiCung,
        HoTen = x.HoTen,
        CCCD = x.CCCD,
        MaPhong = x.MaPhong,
        NgayThem = x.NgayThem,
        NguoiThem = x.NguoiThem
    };

    public async Task<List<CompanionDto>> GetCompanionsAsync(string maPhieuDat, CurrentUser user)
    {
        var phieu = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.MaPhieuDat == maPhieuDat).Select(x => new { x.MaKhach }).FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");
        KiemTraQuyenSoHuu(phieu.MaKhach, user);
        var ds = await _context.KhachDiCungs.AsNoTracking().Where(x => x.MaPhieuDat == maPhieuDat).OrderBy(x => x.MaKhachDiCung).ToListAsync();
        return ds.Select(ToCompanionDto).ToList();
    }

    public async Task<CompanionDto> AddCompanionAsync(string maPhieuDat, AddCompanionRequest request, CurrentUser user)
    {
        if (user.LaKhachHang)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Chỉ nhân viên lễ tân được khai báo khách ở cùng.");
        var hoTen = request.HoTen?.Trim();
        if (string.IsNullOrEmpty(hoTen))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_HO_TEN", "Cần nhập họ tên người ở cùng.", "hoTen");
        var cccd = string.IsNullOrWhiteSpace(request.CCCD) ? null : request.CCCD.Trim();
        if (cccd is not null && !System.Text.RegularExpressions.Regex.IsMatch(cccd, "^[0-9A-Za-z]{6,20}$"))
            throw new ApiException(StatusCodes.Status400BadRequest, "CCCD_KHONG_HOP_LE", "Số giấy tờ chỉ gồm chữ và số (6-20 ký tự).", "cccd");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var phieu = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .Include(x => x.ChiTietPhieuDats).Include(x => x.Khach).Include(x => x.KhachDiCungs)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");
        KiemTraTrangThaiKhachDiCung(phieu);

        if (phieu.KhachDiCungs.Count >= SoKhachDiCungToiDa)
            throw new ApiException(StatusCodes.Status409Conflict, "QUA_SO_KHACH_DI_CUNG", $"Mỗi phiếu chỉ khai báo tối đa {SoKhachDiCungToiDa} người ở cùng.");
        if (cccd is not null && (phieu.KhachDiCungs.Any(x => string.Equals(x.CCCD, cccd, StringComparison.OrdinalIgnoreCase)) || string.Equals(phieu.Khach.CCCD, cccd, StringComparison.OrdinalIgnoreCase)))
            throw new ApiException(StatusCodes.Status409Conflict, "CCCD_DA_KHAI_BAO", "Số giấy tờ này đã được khai báo trong phiếu (khách đại diện hoặc người ở cùng).", "cccd");
        var maPhong = string.IsNullOrWhiteSpace(request.MaPhong) ? null : request.MaPhong.Trim();
        if (maPhong is not null && phieu.ChiTietPhieuDats.All(x => x.MaPhong != maPhong))
            throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_KHONG_THUOC_PHIEU_DAT", $"Phòng {maPhong} không thuộc phiếu đặt phòng này.", "maPhong");

        var moi = new KhachDiCung { MaPhieuDat = maPhieuDat, HoTen = hoTen, CCCD = cccd, MaPhong = maPhong, NgayThem = DateTime.Now, NguoiThem = user.TenDN };
        _context.KhachDiCungs.Add(moi);
        _audit.Ghi("THEM_KHACH_DI_CUNG", maPhieuDat, $"hoTen={hoTen}; phong={maPhong ?? "chua xep"}; coGiayTo={(cccd is null ? "khong" : "co")}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ToCompanionDto(moi);
    }

    public async Task RemoveCompanionAsync(string maPhieuDat, int maKhachDiCung, CurrentUser user)
    {
        if (user.LaKhachHang)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Chỉ nhân viên lễ tân được xóa khách ở cùng.");
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var phieu = await _context.PhieuDatPhongs
            .FromSql($"SELECT * FROM PhieuDatPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuDat = {maPhieuDat}")
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");
        KiemTraTrangThaiKhachDiCung(phieu);
        var khach = await _context.KhachDiCungs.FirstOrDefaultAsync(x => x.MaKhachDiCung == maKhachDiCung && x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHACH_DI_CUNG", "Không tìm thấy người ở cùng trong phiếu này.");
        _context.KhachDiCungs.Remove(khach);
        _audit.Ghi("XOA_KHACH_DI_CUNG", maPhieuDat, $"hoTen={khach.HoTen}; phong={khach.MaPhong ?? "chua xep"}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void KiemTraTrangThaiKhachDiCung(PhieuDatPhong phieu)
    {
        if (phieu.TrangThai is not (TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN or TrangThaiPhieuDat.DA_NHAN_PHONG))
            throw new ApiException(StatusCodes.Status409Conflict, "TRANG_THAI_KHONG_HOP_LE", $"Không thay đổi danh sách người ở cùng khi phiếu đang ở trạng thái {phieu.TrangThai}.");
    }
}
