using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Logic phòng dùng chung cho đặt phòng, nhận phòng và đổi phòng.</summary>
public static class RoomAvailability
{
    /// <summary>Các phòng đã bị chiếm trong khoảng [nhan, tra) bởi phiếu đặt còn hiệu lực hoặc khách đang ở (bỏ qua phiếu <paramref name="boQuaMaPhieuDat"/> khi đang sửa chính phiếu đó).</summary>
    public static async Task<HashSet<string>> GetOccupiedRoomIdsAsync(KLCN060DbContext context, DateOnly nhan, DateOnly tra, string? boQuaMaPhieuDat = null)
    {
        var daDat = await context.ChiTietPhieuDats
            .Where(ct => ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.DA_HUY
                      && ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.KHONG_DEN
                      && ct.MaPhieuDat != boQuaMaPhieuDat
                      && ct.PhieuDatPhong.NgayDonDuKien < tra
                      && ct.PhieuDatPhong.NgayTraDuKien > nhan
                      // Phòng khách đã trả sớm (hoặc đã được đổi đi) không còn bị giữ tới hết ngày trả dự kiến.
                      && !(ct.PhieuDatPhong.PhieuNhanPhong != null
                           && ct.PhieuDatPhong.PhieuNhanPhong.ChiTietPhieuNhans.Any(n => n.MaPhong == ct.MaPhong && n.TrangThai == TrangThaiChiTietPhieuNhan.DA_TRA_PHONG)))
            .Select(ct => ct.MaPhong)
            .Distinct()
            .ToListAsync();

        // Khách đang ở chỉ chiếm phòng tới ngày trả dự kiến (đã gia hạn thì theo ngày mới) - riêng khách dự kiến trả hôm nay vẫn còn trong
        // phòng tới khi trả thật nên hôm nay phòng chưa trống. Chưa biết ngày trả hoặc đã quá hạn (ở quá giờ) thì coi như còn chiếm tiếp.
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var ngayMai = homNay.AddDays(1);
        var dangO = await context.ChiTietPhieuNhans
            .Where(ct => ct.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O
                      && ct.NgayNhan < tra
                      && (ct.NgayTra == null
                          ? (ct.NgayTraDuKien == null || ct.NgayTraDuKien < homNay || (ct.NgayTraDuKien > ngayMai ? ct.NgayTraDuKien : ngayMai) > nhan)
                          : ct.NgayTra > nhan))
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

    /// <summary>
    /// Phòng không có khách đang ở và không được giữ cho phiếu đặt nào trong khoảng [hôm nay, <paramref name="denNgay"/>)
    /// (mặc định chỉ xét đêm nay; khách vãng lai ở nhiều đêm phải truyền ngày trả dự kiến để không đè lên phiếu đặt những ngày sau).
    /// </summary>
    public static async Task EnsureFreeTodayAsync(KLCN060DbContext context, string maPhong, DateOnly homNay, string? field = null, DateOnly? denNgay = null)
    {
        var den = denNgay is { } d && d > homNay ? d : homNay.AddDays(1);
        var dangO = await context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var phieuVuong = await context.ChiTietPhieuDats
            .Where(x => x.MaPhong == maPhong
                && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                && x.PhieuDatPhong.NgayDonDuKien < den && x.PhieuDatPhong.NgayTraDuKien > homNay)
            .OrderBy(x => x.PhieuDatPhong.NgayDonDuKien)
            .Select(x => new { x.MaPhieuDat, x.PhieuDatPhong.NgayDonDuKien })
            .FirstOrDefaultAsync();
        if (dangO)
            throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE", $"Phòng {maPhong} đang có khách hoặc đã được đặt cho hôm nay.", field);
        if (phieuVuong is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                phieuVuong.NgayDonDuKien <= homNay
                    ? $"Phòng {maPhong} đang có khách hoặc đã được đặt cho hôm nay."
                    : $"Phòng {maPhong} đã có phiếu đặt {phieuVuong.MaPhieuDat} từ {phieuVuong.NgayDonDuKien:dd/MM/yyyy}, trong khi khách ở tới {den:dd/MM/yyyy}. Hãy chọn phòng khác hoặc ngày trả sớm hơn.", field);
    }
}
