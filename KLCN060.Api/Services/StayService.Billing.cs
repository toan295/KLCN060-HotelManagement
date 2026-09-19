using KLCN060.Api.DTOs.Stays;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class StayService
{
    public async Task<List<StaySummaryDto>> SearchAsync(string? maPhong, string? trangThai)
    {
        var tt = TrangThaiChiTietPhieuNhan.DANG_O;
        if (!string.IsNullOrWhiteSpace(trangThai) && !Enum.TryParse(trangThai, ignoreCase: true, out tt))
            throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE", "TrangThai không hợp lệ. Chỉ chấp nhận: DANG_O, DA_TRA_PHONG.", "trangThai");

        var q = _context.ChiTietPhieuNhans.Where(x => x.TrangThai == tt);
        if (!string.IsNullOrWhiteSpace(maPhong))
            q = q.Where(x => x.MaPhong == maPhong);

        return await q.OrderBy(x => x.MaPhong).Select(x => new StaySummaryDto
        {
            MaPhieuNhan = x.MaPhieuNhan,
            MaPhong = x.MaPhong,
            MaPhieuDat = x.PhieuNhanPhong.MaPhieuDat,
            MaKhach = x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.MaKhach,
            HoTenKhach = x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen,
            NgayNhan = x.NgayNhan,
            NgayTra = x.NgayTra,
            SoNguoi = x.SoNguoi,
            DonGia = x.DonGia,
            TrangThai = x.TrangThai.ToString()
        }).ToListAsync();
    }

    public async Task<ChangeRoomResultDto> ChangeRoomAsync(string maPhieuNhan, string maPhong, ChangeRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MaPhongMoi))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_MOI", "Cần chỉ định phòng mới.", "maPhongMoi");
        if (request.MaPhongMoi == maPhong)
            throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_MOI_TRUNG_PHONG_CU", "Phòng mới phải khác phòng hiện tại.", "maPhongMoi");

        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);

        var luotCu = await _context.ChiTietPhieuNhans
            .Include(x => x.Phong).ThenInclude(p => p.LoaiPhong)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        if (luotCu.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng, không thể đổi phòng.");

        var maPhongMoi = request.MaPhongMoi;
        var phongMoi = await _context.Phongs
            .FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhongMoi}")
            .Include(x => x.LoaiPhong)
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng mới.", "maPhongMoi");

        RoomAvailability.EnsureReady(phongMoi);
        await RoomAvailability.EnsureFreeTodayAsync(_context, maPhongMoi, homNay, "maPhongMoi");

        // Khóa chính kép (MaPhieuNhan, MaPhong) nên không thể quay lại phòng đã ở trong cùng lượt lưu trú.
        if (await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhongMoi))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TUNG_O_TRONG_LUOT", "Khách đã từng ở phòng này trong cùng lượt lưu trú, hãy chọn phòng khác.", "maPhongMoi");

        var tienTo = $"RM{bayGio:yyyyMMdd}-";
        var maHienCo = await _context.PhieuDoiPhongs.Where(x => x.MaPhieuDoi.StartsWith(tienTo)).Select(x => x.MaPhieuDoi).ToListAsync();
        var phieuDoi = new PhieuDoiPhong
        {
            MaPhieuDoi = MaCodeGenerator.GenerateNext(maHienCo, tienTo, 4),
            MaPhieuNhan = maPhieuNhan,
            MaPhongCu = maPhong,
            MaPhongMoi = maPhongMoi,
            NgayDoi = bayGio,
            ChenhLechGia = phongMoi.LoaiPhong.DonGia - luotCu.DonGia,
            GhiChu = request.GhiChu
        };
        _context.PhieuDoiPhongs.Add(phieuDoi);

        // PhieuDoiPhong giữ FK Restrict tới dòng phòng cũ nên KHÔNG xóa được: giữ lại dòng cũ như lịch sử
        // (đóng lại hôm nay) và mở dòng mới cho phòng mới, để tiền phòng những đêm đã ở vẫn được tính đúng.
        luotCu.TrangThai = TrangThaiChiTietPhieuNhan.DA_TRA_PHONG;
        luotCu.NgayTra = homNay;
        luotCu.Phong.TinhTrang = TinhTrangPhong.VD;

        _context.ChiTietPhieuNhans.Add(new ChiTietPhieuNhan
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhong = maPhongMoi,
            NgayNhan = homNay,
            NgayTra = null,
            SoNguoi = luotCu.SoNguoi,
            DonGia = phongMoi.LoaiPhong.DonGia,
            TienPhuThu = 0,
            TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
        });
        phongMoi.TinhTrang = TinhTrangPhong.OC;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi đổi phòng, vui lòng thử lại.");
        }
        await transaction.CommitAsync();

        return new ChangeRoomResultDto
        {
            MaPhieuDoi = phieuDoi.MaPhieuDoi,
            MaPhieuNhan = maPhieuNhan,
            MaPhongCu = maPhong,
            MaPhongMoi = maPhongMoi,
            NgayDoi = bayGio,
            ChenhLechGia = phieuDoi.ChenhLechGia,
            TinhTrangPhongCu = nameof(TinhTrangPhong.VD),
            TinhTrangPhongMoi = nameof(TinhTrangPhong.OC)
        };
    }

    public async Task<CheckOutResultDto> CheckOutAsync(string maPhieuNhan, string maPhong, CheckOutRequest request, CurrentUser user)
    {
        if (string.IsNullOrEmpty(user.MaNV))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Tài khoản không gắn với nhân viên nào.");

        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Khóa phiếu nhận: các phòng của cùng phiếu trả đồng thời sẽ được xử lý tuần tự, tránh lập 2 hóa đơn hoặc bỏ sót hóa đơn.
        var phieuNhan = await _context.PhieuNhanPhongs
            .FromSql($"SELECT * FROM PhieuNhanPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuNhan = {maPhieuNhan}")
            .FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");

        var luot = await _context.ChiTietPhieuNhans
            .Include(x => x.Phong)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        if (luot.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng.");

        luot.NgayTra = homNay;
        luot.TrangThai = TrangThaiChiTietPhieuNhan.DA_TRA_PHONG;
        if (request.ApDungPhuThuTreGio)
            luot.TienPhuThu += luot.DonGia;
        luot.Phong.TinhTrang = TinhTrangPhong.VD;
        await _context.SaveChangesAsync();

        var conPhongDangO = await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var folio = await FolioCalculator.ComputeAsync(_context, maPhieuNhan, homNay);

        string? maHoaDon = null;
        if (!conPhongDangO && !await _context.HoaDons.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan))
            maHoaDon = await LapHoaDonAsync(folio, user.MaNV, bayGio);

        await transaction.CommitAsync();

        return new CheckOutResultDto
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhong = maPhong,
            TrangThai = nameof(TrangThaiChiTietPhieuNhan.DA_TRA_PHONG),
            TinhTrangPhong = nameof(TinhTrangPhong.VD),
            MaHoaDon = maHoaDon,
            TongHopFolio = ToSummary(folio)
        };
    }

    public async Task<FolioDto> GetFolioAsync(string maPhieuNhan, string maPhong, CurrentUser user)
    {
        var luot = await TimLuotLuuTruAsync(maPhieuNhan, maPhong);
        var folio = await FolioCalculator.ComputeAsync(_context, luot.MaPhieuNhan, DateOnly.FromDateTime(DateTime.Now));

        if (user.LaKhachHang && (folio.MaKhach is null || folio.MaKhach != user.MaKhach))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Bạn không có quyền xem folio này.");

        return new FolioDto
        {
            MaPhieuNhan = maPhieuNhan,
            TongHopFolio = ToSummary(folio),
            ChiTiet = folio.Lines.Select(l => new FolioLineDto
            {
                LoaiKhoanMuc = l.Loai.ToString(),
                MoTa = l.MoTa,
                SoLuong = l.SoLuong,
                DonGia = l.DonGia,
                ThanhTien = l.ThanhTien
            }).ToList()
        };
    }

    private async Task<string> LapHoaDonAsync(Folio folio, string maNV, DateTime bayGio)
    {
        var tienTo = $"HD{bayGio:yyyyMMdd}-";
        var maHienCo = await _context.HoaDons.Where(x => x.MaHD.StartsWith(tienTo)).Select(x => x.MaHD).ToListAsync();

        var hoaDon = new HoaDon
        {
            MaHD = MaCodeGenerator.GenerateNext(maHienCo, tienTo, 4),
            MaPhieuNhan = folio.MaPhieuNhan,
            MaNV = maNV,
            NgayLap = bayGio,
            TienPhong = folio.TienPhong,
            TienDV = folio.TienDichVu,
            PhuThu = folio.PhuThu,
            TienDaCoc = folio.TienDaCoc,
            TongTien = folio.ConPhaiThu,
            HinhThucThanhToan = HinhThucThanhToan.TIEN_MAT,
            // Khách đã cọc đủ (hoặc hơn) thì không còn gì phải thu.
            TrangThaiThanhToan = folio.ConPhaiThu == 0 ? TrangThaiThanhToan.DA_THANH_TOAN : TrangThaiThanhToan.CHUA_THANH_TOAN
        };
        foreach (var l in folio.Lines)
        {
            hoaDon.ChiTietHoaDons.Add(new ChiTietHoaDon
            {
                LoaiKhoanMuc = l.Loai,
                MoTa = l.MoTa,
                SoLuong = l.SoLuong,
                DonGia = l.DonGia,
                ThanhTien = l.ThanhTien
            });
        }

        _context.HoaDons.Add(hoaDon);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi lập hóa đơn, vui lòng thử lại.");
        }
        return hoaDon.MaHD;
    }

    private static FolioSummaryDto ToSummary(Folio f) => new()
    {
        TienPhong = f.TienPhong,
        PhuThu = f.PhuThu,
        TienDichVu = f.TienDichVu,
        TongCong = f.TongCong,
        TongKhuyenMai = f.TongKhuyenMai,
        TienDaCoc = f.TienDaCoc,
        ConPhaiThu = f.ConPhaiThu
    };
}
