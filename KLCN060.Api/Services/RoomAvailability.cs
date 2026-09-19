using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Logic phòng dùng chung cho đặt phòng, nhận phòng và đổi phòng.</summary>
public static class RoomAvailability
{
    /// <summary>Các phòng đã bị chiếm trong khoảng [nhan, tra) bởi phiếu đặt còn hiệu lực hoặc khách đang ở.</summary>
    public static async Task<HashSet<string>> GetOccupiedRoomIdsAsync(KLCN060DbContext context, DateOnly nhan, DateOnly tra)
    {
        var daDat = await context.ChiTietPhieuDats
            .Where(ct => ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.DA_HUY
                      && ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.KHONG_DEN
                      && ct.PhieuDatPhong.NgayDonDuKien < tra
                      && ct.PhieuDatPhong.NgayTraDuKien > nhan
                      // Phòng khách đã trả sớm (hoặc đã được đổi đi) không còn bị giữ tới hết ngày trả dự kiến.
                      && !(ct.PhieuDatPhong.PhieuNhanPhong != null
                           && ct.PhieuDatPhong.PhieuNhanPhong.ChiTietPhieuNhans.Any(n => n.MaPhong == ct.MaPhong && n.TrangThai == TrangThaiChiTietPhieuNhan.DA_TRA_PHONG)))
            .Select(ct => ct.MaPhong)
            .Distinct()
            .ToListAsync();

        var dangO = await context.ChiTietPhieuNhans
            .Where(ct => ct.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O
                      && ct.NgayNhan < tra
                      && (ct.NgayTra == null || ct.NgayTra > nhan))
            .Select(ct => ct.MaPhong)
            .Distinct()
            .ToListAsync();

        return daDat.Concat(dangO).ToHashSet();
    }

    /// <summary>Chỉ nhận/đổi vào phòng đã dọn sạch (VC) hoặc đã kiểm tra (VI).</summary>
    public static void EnsureReady(Phong phong)
    {
        if (phong.TinhTrang is not (TinhTrangPhong.VC or TinhTrangPhong.VI))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_CHUA_SAN_SANG", $"Phòng {phong.MaPhong} đang ở trạng thái {phong.TinhTrang}, chưa sẵn sàng để nhận.");
    }

    /// <summary>Phòng không có khách đang ở và không được giữ cho phiếu đặt nào bao trùm ngày hôm nay.</summary>
    public static async Task EnsureFreeTodayAsync(KLCN060DbContext context, string maPhong, DateOnly homNay, string? field = null)
    {
        var dangO = await context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var daDuocDat = await context.ChiTietPhieuDats.AnyAsync(x => x.MaPhong == maPhong
            && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
            && x.PhieuDatPhong.NgayDonDuKien <= homNay && x.PhieuDatPhong.NgayTraDuKien > homNay);
        if (dangO || daDuocDat)
            throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE", $"Phòng {maPhong} đang có khách hoặc đã được đặt cho hôm nay.", field);
    }
}
