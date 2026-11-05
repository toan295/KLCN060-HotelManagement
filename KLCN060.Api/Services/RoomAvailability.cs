using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Logic phòng dùng chung cho đặt phòng, nhận phòng và đổi phòng.</summary>
public static class RoomAvailability
{
    /// <summary>
    /// Các phòng đã bị chiếm trong khoảng [nhan, tra) bởi phiếu đặt đã đặt cọc hoặc khách đang ở (bỏ qua phiếu <paramref name="boQuaMaPhieuDat"/> khi đang sửa chính phiếu đó).
    /// Phiếu CHO_XAC_NHAN (chưa nhận được tiền cọc) KHÔNG giữ phòng: khách khác vẫn đặt được, ai đặt cọc xong trước thì được giữ phòng.
    /// </summary>
    public static async Task<HashSet<string>> GetOccupiedRoomIdsAsync(KLCN060DbContext context, DateOnly nhan, DateOnly tra, string? boQuaMaPhieuDat = null)
    {
        var daDat = await context.ChiTietPhieuDats
            .Where(ct => ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.DA_HUY
                      && ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.KHONG_DEN
                      && ct.PhieuDatPhong.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN
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

    /// <summary>
    /// Lúc phiếu CHO_XAC_NHAN nhận được tiền cọc (chuyển khoản hoặc lễ tân thu): phiếu chưa cọc không giữ phòng nên phòng đã chọn có thể đã bị khách khác đặt cọc trước.
    /// Còn trống thì giữ nguyên; bị chiếm thì tự chuyển sang phòng cùng loại còn trống (cùng giá vì cùng loại, cùng ngày); hết phòng thì trả về lý do để không nhận cọc.
    /// Phải gọi TRONG transaction, trước khi đổi trạng thái phiếu.
    /// </summary>
    public static async Task<string?> GiuPhongKhiDatCocAsync(KLCN060DbContext context, PhieuDatPhong phieu, bool chiKiemTra = false)
    {
        var chiTiets = await context.ChiTietPhieuDats
            .Include(x => x.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.GiaTheoDems)
            .Where(x => x.MaPhieuDat == phieu.MaPhieuDat)
            .ToListAsync();

        // Khóa phòng của các loại liên quan (cùng thứ tự với đặt phòng) để hai khách đặt cọc cùng lúc không cùng giữ một phòng cuối.
        if (!chiKiemTra)
            foreach (var maLoai in chiTiets.Select(x => x.Phong.MaLoai).Distinct().OrderBy(x => x, StringComparer.Ordinal))
                await context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaLoai = {maLoai}").AsNoTracking().ToListAsync();

        var daChiem = await GetOccupiedRoomIdsAsync(context, phieu.NgayDonDuKien, phieu.NgayTraDuKien, phieu.MaPhieuDat);
        var dangGiu = chiTiets.Select(x => x.MaPhong).ToHashSet();

        // Bước 1: lập kế hoạch đổi phòng, chưa sửa gì - hết phòng ở bất kỳ dòng nào thì bỏ cả phiếu mà không để lại thay đổi dở dang.
        var keHoach = new List<(ChiTietPhieuDat Cu, Phong Moi)>();
        foreach (var ct in chiTiets.OrderBy(x => x.MaPhong, StringComparer.Ordinal))
        {
            if (ct.Phong.TinhTrang != TinhTrangPhong.OOO && !daChiem.Contains(ct.MaPhong)) continue;

            var maLoai = ct.Phong.MaLoai;
            var thay = await context.Phongs.AsNoTracking()
                .Where(p => p.MaLoai == maLoai && p.TinhTrang != TinhTrangPhong.OOO)
                .OrderBy(p => p.MaPhong).ToListAsync();
            var phongMoi = thay.FirstOrDefault(p => !daChiem.Contains(p.MaPhong) && !dangGiu.Contains(p.MaPhong));
            if (phongMoi is null)
                return $"Loại phòng {ct.Phong.LoaiPhong.TenLoai} đã hết phòng trong khoảng {phieu.NgayDonDuKien:dd/MM/yyyy} - {phieu.NgayTraDuKien:dd/MM/yyyy} " +
                       "(khách khác đã đặt cọc trước). Phiếu này chưa được xác nhận.";
            keHoach.Add((ct, phongMoi));
            dangGiu.Remove(ct.MaPhong);
            dangGiu.Add(phongMoi.MaPhong);
        }

        if (chiKiemTra) return null;

        // Bước 2: áp dụng.
        foreach (var (ct, phongMoi) in keHoach)
        {
            var moi = new ChiTietPhieuDat { MaPhieuDat = ct.MaPhieuDat, MaPhong = phongMoi.MaPhong, SoLuong = ct.SoLuong, DonGiaApDung = ct.DonGiaApDung, ThanhTien = ct.ThanhTien };
            foreach (var g in ct.GiaTheoDems)
                moi.GiaTheoDems.Add(new GiaDemDatPhong
                {
                    MaPhieuDat = g.MaPhieuDat, MaPhong = phongMoi.MaPhong, Dem = g.Dem, GiaCoBan = g.GiaCoBan, Gia = g.Gia
                });
            var maCu = ct.MaPhong;
            context.GiaDemDatPhongs.RemoveRange(ct.GiaTheoDems);
            context.ChiTietPhieuDats.Remove(ct);
            context.ChiTietPhieuDats.Add(moi);
            var maPhongMoi = phongMoi.MaPhong;
            await context.KhachDiCungs.Where(x => x.MaPhieuDat == ct.MaPhieuDat && x.MaPhong == maCu).ExecuteUpdateAsync(s => s.SetProperty(x => x.MaPhong, maPhongMoi));
        }
        return null;
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
                && x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN
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
