using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Stays;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Finance;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class StayService
{
    public async Task<List<StaySummaryDto>> SearchAsync(string? maPhong, string? trangThai)
    {
        var tt = TrangThaiChiTietPhieuNhan.DANG_O;
        if (!string.IsNullOrWhiteSpace(trangThai) && !(Enum.TryParse(trangThai, ignoreCase: true, out tt) && Enum.IsDefined(tt)))
            throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE", "TrangThai không hợp lệ. Chỉ chấp nhận: DANG_O, DA_TRA_PHONG.", "trangThai");

        var q = _context.ChiTietPhieuNhans.Where(x => x.TrangThai == tt);
        if (!string.IsNullOrWhiteSpace(maPhong))
            q = q.Where(x => x.MaPhong == maPhong);

        return await q.OrderBy(x => x.MaPhong).Select(x => new StaySummaryDto
        {
            MaPhieuNhan = x.MaPhieuNhan,
            MaPhong = x.MaPhong,
            MaPhieuDat = x.PhieuNhanPhong.MaPhieuDat,
            // [Khối 8 - K8-3] Cùng nguyên nhân K7-1: lượt walk-in (không có phiếu đặt) trước đây luôn hiện khách = null trên
            // danh sách lưu trú của lễ tân. Ưu tiên PhieuNhanPhong.Khach, fallback qua phiếu đặt cho dữ liệu trước migration K7-1.
            MaKhach = x.PhieuNhanPhong.MaKhach ?? (x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.MaKhach),
            HoTenKhach = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen
                : x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen,
            SoDTKhach = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.SoDT
                : x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.Khach.SoDT,
            MaLoai = x.Phong.MaLoai,
            TenLoai = x.Phong.LoaiPhong.TenLoai,
            NgayNhan = x.NgayNhan,
            NgayTra = x.NgayTra,
            NgayTraDuKien = x.NgayTraDuKien ?? (x.PhieuNhanPhong.PhieuDatPhong == null ? null : x.PhieuNhanPhong.PhieuDatPhong.NgayTraDuKien),
            SoNguoi = x.SoNguoi,
            DonGia = x.DonGia,
            TrangThai = x.TrangThai.ToString()
        }).ToListAsync();
    }

    /// <summary>Kết quả kiểm tra điều kiện đổi phòng (dùng chung cho đổi thật và xem trước).</summary>
    private sealed record KeHoachDoiPhong(ChiTietPhieuNhan LuotCu, Phong PhongMoi, decimal DonGiaMoi, decimal DonGiaBangGia, string LoaiDoi, int SoDemConLai, DateOnly DenNgay);

    /// <summary>Kiểm tra mọi điều kiện đổi phòng (phải gọi trong transaction đang mở): khóa phiếu nhận + phòng mới, trạng thái, giấy phép nâng hạng, sức chứa, trùng đặt chỗ trong những đêm còn lại, giá.</summary>
    private async Task<KeHoachDoiPhong> ChuanBiDoiPhongAsync(string maPhieuNhan, string maPhong, ChangeRoomRequest request, DateTime bayGio, bool batBuocLyDo)
    {
        if (string.IsNullOrWhiteSpace(request.MaPhongMoi))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_MOI", "Cần chỉ định phòng mới.", "maPhongMoi");
        if (request.MaPhongMoi == maPhong)
            throw new ApiException(StatusCodes.Status400BadRequest, "PHONG_MOI_TRUNG_PHONG_CU", "Phòng mới phải khác phòng hiện tại.", "maPhongMoi");
        if (batBuocLyDo && string.IsNullOrWhiteSpace(request.GhiChu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần ghi lý do đổi phòng (vì sao đổi, ai yêu cầu): ảnh hưởng phòng và tiền của khách, có ghi nhật ký.", "ghiChu");

        var homNay = DateOnly.FromDateTime(bayGio);
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

        // Khách còn ở tới ngày trả dự kiến: phòng mới không được có phiếu đặt nào bắt đầu trong khoảng đó (nếu không khách sẽ phải đổi phòng lần nữa hoặc đặt trùng).
        var denNgay = luotCu.NgayTraDuKien is { } ngayTra && ngayTra > homNay ? ngayTra : homNay.AddDays(1);
        var vuong = await _context.ChiTietPhieuDats
            .Where(x => x.MaPhong == maPhongMoi
                        && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                        && x.PhieuDatPhong.NgayDonDuKien < denNgay && x.PhieuDatPhong.NgayTraDuKien > homNay)
            .OrderBy(x => x.PhieuDatPhong.NgayDonDuKien)
            .Select(x => new { x.MaPhieuDat, x.PhieuDatPhong.NgayDonDuKien })
            .FirstOrDefaultAsync();
        if (vuong is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                $"Phòng {maPhongMoi} đã có phiếu đặt {vuong.MaPhieuDat} từ {vuong.NgayDonDuKien:dd/MM/yyyy}, trong khi khách ở tới {denNgay:dd/MM/yyyy}. Hãy chọn phòng khác.", "maPhongMoi");

        var toiDa = phongMoi.LoaiPhong.SoNguoiTieuChuan + CheckInService.NguoiThemToiDa;
        if (luotCu.SoNguoi > toiDa)
            throw new ApiException(StatusCodes.Status409Conflict, "VUOT_SUC_CHUA",
                $"Khách đang ở {luotCu.SoNguoi} người, phòng {maPhongMoi} ({phongMoi.LoaiPhong.TenLoai}) chỉ chứa tối đa {toiDa} người.", "maPhongMoi");

        // Khóa chính kép (MaPhieuNhan, MaPhong) nên không thể quay lại phòng đã ở trong cùng lượt lưu trú.
        if (await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhongMoi))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TUNG_O_TRONG_LUOT", "Khách đã từng ở phòng này trong cùng lượt lưu trú, hãy chọn phòng khác.", "maPhongMoi");

        // Đơn giá phòng mới: theo bảng giá của ngày đổi (loại đặt phòng của phiếu gốc); nâng hạng luôn thu chênh lệch.
        var loaiDat = await _context.PhieuNhanPhongs.Where(x => x.MaPhieuNhan == maPhieuNhan)
            .Select(x => x.PhieuDatPhong == null ? LoaiDatPhong.CA_NHAN : x.PhieuDatPhong.LoaiDatPhong).FirstAsync();
        var donGiaBangGia = await GiaPhongCalculator.DonGiaDemAsync(_context, phongMoi.LoaiPhong, homNay, loaiDat);
        var donGiaMoi = donGiaBangGia;

        var loaiDoi = donGiaMoi > luotCu.DonGia ? "NANG_HANG" : donGiaMoi < luotCu.DonGia ? "HA_HANG" : "NGANG_HANG";
        return new KeHoachDoiPhong(luotCu, phongMoi, donGiaMoi, donGiaBangGia, loaiDoi, Math.Max(denNgay.DayNumber - homNay.DayNumber, 1), denNgay);
    }

    public async Task<ChangeRoomResultDto> ChangeRoomAsync(string maPhieuNhan, string maPhong, ChangeRoomRequest request)
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var ke = await ChuanBiDoiPhongAsync(maPhieuNhan, maPhong, request, bayGio, batBuocLyDo: true);
        var (luotCu, phongMoi, donGiaMoi, loaiDoi) = (ke.LuotCu, ke.PhongMoi, ke.DonGiaMoi, ke.LoaiDoi);
        var maPhongMoi = phongMoi.MaPhong;

        var tienTo = $"RM{bayGio:yyyyMMdd}-";
        // [Khối 8 - P1-4] Khóa theo tiền tố tới hết transaction (sau khóa phiếu nhận + phòng mới).
        var maPhieuDoi = await MaCodeGenerator.GenerateNextAsync(_context,
            _context.PhieuDoiPhongs.Where(x => x.MaPhieuDoi.StartsWith(tienTo)).Select(x => x.MaPhieuDoi), tienTo, 4);
        var phieuDoi = new PhieuDoiPhong
        {
            MaPhieuDoi = maPhieuDoi,
            MaPhieuNhan = maPhieuNhan,
            MaPhongCu = maPhong,
            MaPhongMoi = maPhongMoi,
            NgayDoi = bayGio,
            ChenhLechGia = donGiaMoi - luotCu.DonGia,
            GhiChu = request.GhiChu!.Trim()
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
            NgayTraDuKien = luotCu.NgayTraDuKien,
            SoNguoi = luotCu.SoNguoi,
            DonGia = donGiaMoi,
            TienPhuThu = 0,
            SoNguoiTieuChuanChot = phongMoi.LoaiPhong.SoNguoiTieuChuan,
            PhuThuNguoiThemChot = phongMoi.LoaiPhong.PhuThu,
            TrangThai = TrangThaiChiTietPhieuNhan.DANG_O
        });
        phongMoi.TinhTrang = TinhTrangPhong.OC;

        _audit.LyDoTinhTrangPhong = $"Đổi phòng {maPhong} → {maPhongMoi}";
        _audit.Ghi("DOI_PHONG", maPhieuNhan,
            $"{maPhong} -> {maPhongMoi}; loai={loaiDoi}; loaiPhong={luotCu.Phong.MaLoai}->{phongMoi.MaLoai}; donGiaCu={Math.Round(luotCu.DonGia, 0)}; donGiaMoi={Math.Round(donGiaMoi, 0)}; soDemConLai={ke.SoDemConLai}; tacDongDonGia={Math.Round((donGiaMoi - luotCu.DonGia) * ke.SoDemConLai, 0)}; soNguoi={luotCu.SoNguoi}; lyDo={request.GhiChu!.Trim()}");

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
            TinhTrangPhongMoi = nameof(TinhTrangPhong.OC),
            LoaiDoi = loaiDoi
        };
    }

    /// <summary>Xem trước đổi phòng: cùng các bước kiểm tra như đổi thật (trong transaction rồi hủy) nhưng không ghi gì; trả đủ thông tin để lễ tân biết hạng/giá/chênh lệch/cần duyệt hay không.</summary>
    public async Task<ChangeRoomPreviewDto> XemTruocDoiPhongAsync(string maPhieuNhan, string maPhong, string? maPhongMoi)
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var luot = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Include(x => x.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(p => p!.Khach)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        var khach = luot.PhieuNhanPhong.Khach ?? luot.PhieuNhanPhong.PhieuDatPhong?.Khach;
        var denNgay = luot.NgayTraDuKien is { } t && t > homNay ? t : homNay.AddDays(1);
        var dto = new ChangeRoomPreviewDto
        {
            MaPhieuNhan = maPhieuNhan, MaPhieuDat = luot.PhieuNhanPhong.MaPhieuDat, MaKhach = khach?.MaKhach, HoTenKhach = khach?.HoTen,
            MaPhongCu = maPhong, MaLoaiCu = luot.Phong.MaLoai, TenLoaiCu = luot.Phong.LoaiPhong.TenLoai, DonGiaCu = luot.DonGia,
            NgayNhan = luot.NgayNhan, NgayTraDuKien = luot.NgayTraDuKien, SoDemConLai = Math.Max(denNgay.DayNumber - homNay.DayNumber, 1), SoNguoi = luot.SoNguoi,
            TrangThaiLuot = luot.TrangThai.ToString(),
            MaPhongMoi = maPhongMoi
        };
        if (luot.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
        {
            dto.CoTheDoi = false; dto.MaLoi = "LUOT_LUU_TRU_DA_KET_THUC"; dto.LyDoKhongDoi = "Lượt lưu trú này đã trả phòng, không thể đổi phòng.";
            return dto;
        }
        if (string.IsNullOrWhiteSpace(maPhongMoi))
        {
            dto.CoTheDoi = false; dto.HuongDan = "Chọn phòng mới để xem hạng, giá và chênh lệch.";
            return dto;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ke = await ChuanBiDoiPhongAsync(maPhieuNhan, maPhong, new ChangeRoomRequest { MaPhongMoi = maPhongMoi }, bayGio, batBuocLyDo: false);
            var cu = ke.LuotCu.Phong.LoaiPhong; var moi = ke.PhongMoi.LoaiPhong;
            var phuThuCu = Math.Max(0, ke.LuotCu.SoNguoi - cu.SoNguoiTieuChuan) * cu.PhuThu;
            var phuThuMoi = Math.Max(0, ke.LuotCu.SoNguoi - moi.SoNguoiTieuChuan) * moi.PhuThu;
            var chenh = ke.DonGiaMoi - ke.LuotCu.DonGia;
            dto.MaLoaiMoi = ke.PhongMoi.MaLoai; dto.TenLoaiMoi = moi.TenLoai; dto.TinhTrangPhongMoi = ke.PhongMoi.TinhTrang.ToString();
            dto.SucChuaPhongMoi = moi.SoNguoiTieuChuan + CheckInService.NguoiThemToiDa;
            dto.DonGiaBangGia = ke.DonGiaBangGia; dto.DonGiaApDung = ke.DonGiaMoi;
            dto.LoaiDoi = ke.LoaiDoi;
            dto.ChenhLechMoiDem = chenh; dto.PhuThuNguoiCuMoiDem = phuThuCu; dto.PhuThuNguoiMoiMoiDem = phuThuMoi;
            dto.SoDemConLai = ke.SoDemConLai;
            dto.TacDongTong = (chenh + phuThuMoi - phuThuCu) * ke.SoDemConLai;
            dto.CoTheDoi = true;
            dto.CanLyDo = true;
            dto.HuongDan = dto.LoaiDoi switch
            {
                "NANG_HANG" => $"Nâng hạng CÓ TÍNH TIỀN: khách trả thêm {chenh:N0}đ/đêm × {ke.SoDemConLai} đêm còn lại. Bắt buộc ghi lý do, có ghi nhật ký.",
                "HA_HANG" => $"Hạ hạng: tiền phòng GIẢM {-chenh:N0}đ/đêm × {ke.SoDemConLai} đêm còn lại. Bắt buộc ghi lý do, có ghi nhật ký.",
                _ => "Đổi ngang hạng: đơn giá không đổi. Bắt buộc ghi lý do."
            };
            if (ke.PhongMoi.LoaiPhong.SoNguoiTieuChuan < ke.LuotCu.SoNguoi)
                dto.CanhBao.Add($"Phòng mới tiêu chuẩn {moi.SoNguoiTieuChuan} người, khách đang ở {ke.LuotCu.SoNguoi} người: tính phụ thu người ở thêm {phuThuMoi:N0}đ/đêm.");
            if (luot.NgayTraDuKien is null)
                dto.CanhBao.Add("Lượt ở chưa khai ngày trả dự kiến: tác động tiền tính cho 1 đêm.");
        }
        catch (ApiException e)
        {
            dto.CoTheDoi = false; dto.MaLoi = e.Code; dto.LyDoKhongDoi = e.Message;
        }
        // transaction không commit → hủy mọi khóa, không ghi gì.
        return dto;
    }

    public async Task<ExtendStayResultDto> ExtendAsync(string maPhieuNhan, string maPhong, ExtendStayRequest request)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (request.NgayTraMoi <= homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_TRA_KHONG_HOP_LE", "Ngày trả mới phải sau hôm nay.", "ngayTraMoi");
        BookingService.KiemTraSoDem(homNay, request.NgayTraMoi, "ngayTraMoi");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);

        var luot = await _context.ChiTietPhieuNhans.Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        if (luot.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng, không gia hạn được.");

        var phieuDat = luot.PhieuNhanPhong.PhieuDatPhong;
        var hienTai = luot.NgayTraDuKien ?? phieuDat?.NgayTraDuKien;
        if (hienTai is { } h && request.NgayTraMoi <= h)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_TRA_KHONG_HOP_LE",
                $"Ngày trả mới phải sau ngày trả dự kiến hiện tại ({h:dd/MM/yyyy}). Muốn trả sớm hơn hãy làm thủ tục trả phòng.", "ngayTraMoi");

        // Khóa phòng rồi kiểm tra không có phiếu đặt nào khác giữ phòng này trong những đêm thêm.
        await _context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}").AsNoTracking().ToListAsync();
        var tu = hienTai is { } h2 && h2 > homNay ? h2 : homNay;
        var maPhieuDatCuaLuot = phieuDat?.MaPhieuDat;
        var vuong = await _context.ChiTietPhieuDats
            .Where(x => x.MaPhong == maPhong && x.MaPhieuDat != maPhieuDatCuaLuot
                        && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                        && x.PhieuDatPhong.NgayDonDuKien < request.NgayTraMoi && x.PhieuDatPhong.NgayTraDuKien > tu)
            .OrderBy(x => x.PhieuDatPhong.NgayDonDuKien)
            .Select(x => new { x.MaPhieuDat, x.PhieuDatPhong.NgayDonDuKien })
            .FirstOrDefaultAsync();
        if (vuong is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "ROOM_NOT_AVAILABLE",
                $"Không gia hạn được: phòng {maPhong} đã có phiếu đặt {vuong.MaPhieuDat} từ {vuong.NgayDonDuKien:dd/MM/yyyy}. Hãy đổi phòng hoặc chọn ngày trả sớm hơn.", "ngayTraMoi");

        luot.NgayTraDuKien = request.NgayTraMoi;
        if (phieuDat is not null && request.NgayTraMoi > phieuDat.NgayTraDuKien)
            phieuDat.NgayTraDuKien = request.NgayTraMoi;

        var soDemThem = request.NgayTraMoi.DayNumber - (hienTai ?? homNay).DayNumber;
        var tienThem = soDemThem * luot.DonGia;
        _audit.Ghi("GIA_HAN", maPhieuNhan,
            $"phong={maPhong}; ngayTraCu={(hienTai is { } c ? c.ToString("yyyy-MM-dd") : "chua khai")}; ngayTraMoi={request.NgayTraMoi:yyyy-MM-dd}; soDemThem={soDemThem}; donGia={Math.Round(luot.DonGia, 0)}; tienThem={Math.Round(tienThem, 0)}" +
            (string.IsNullOrWhiteSpace(request.LyDo) ? "" : $"; lyDo={request.LyDo.Trim()}"));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi gia hạn, vui lòng thử lại.");
        }
        await transaction.CommitAsync();

        return new ExtendStayResultDto
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhong = maPhong,
            NgayTraDuKienCu = hienTai,
            NgayTraDuKienMoi = request.NgayTraMoi,
            SoDemThem = soDemThem,
            DonGia = luot.DonGia,
            TienThemDuKien = tienThem
        };
    }

    public async Task<ExtendPreviewDto> XemTruocGiaHanAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var luot = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(p => p!.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.Khach)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        var phieuDat = luot.PhieuNhanPhong.PhieuDatPhong;
        var hienTai = luot.NgayTraDuKien ?? phieuDat?.NgayTraDuKien;
        var dto = new ExtendPreviewDto
        {
            MaPhieuNhan = maPhieuNhan, MaPhong = maPhong, NgayTraDuKienCu = hienTai, NgayTraMoi = ngayTraMoi,
            HoTenKhach = (luot.PhieuNhanPhong.Khach ?? phieuDat?.Khach)?.HoTen, DonGia = luot.DonGia
        };
        dto.SoDemThem = Math.Max(ngayTraMoi.DayNumber - (hienTai ?? homNay).DayNumber, 0);
        dto.TienThemDuKien = dto.SoDemThem * luot.DonGia;

        var tu = hienTai is { } h2 && h2 > homNay ? h2 : homNay;
        var maPhieuDatCuaLuot = phieuDat?.MaPhieuDat; // khách vãng lai không có phiếu đặt
        var keTiep = await _context.ChiTietPhieuDats
            .Where(x => x.MaPhong == maPhong && x.MaPhieuDat != maPhieuDatCuaLuot
                        && (x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || x.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                        && x.PhieuDatPhong.NgayTraDuKien > tu)
            .OrderBy(x => x.PhieuDatPhong.NgayDonDuKien)
            .Select(x => new { x.MaPhieuDat, x.PhieuDatPhong.NgayDonDuKien })
            .FirstOrDefaultAsync();
        if (keTiep is not null) { dto.MaPhieuDatKeTiep = keTiep.MaPhieuDat; dto.NgayToiDaGiaHan = keTiep.NgayDonDuKien; }

        if (luot.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O) { dto.MaLoi = "LUOT_LUU_TRU_DA_KET_THUC"; dto.LyDoKhongGiaHan = "Lượt lưu trú này đã trả phòng, không gia hạn được."; }
        else if (ngayTraMoi <= homNay) { dto.MaLoi = "NGAY_TRA_KHONG_HOP_LE"; dto.LyDoKhongGiaHan = "Ngày trả mới phải sau hôm nay."; }
        else if (hienTai is { } h && ngayTraMoi <= h) { dto.MaLoi = "NGAY_TRA_KHONG_HOP_LE"; dto.LyDoKhongGiaHan = $"Ngày trả mới phải sau ngày trả dự kiến hiện tại ({h:dd/MM/yyyy})."; }
        else if (keTiep is not null && keTiep.NgayDonDuKien < ngayTraMoi)
        { dto.MaLoi = "ROOM_NOT_AVAILABLE"; dto.LyDoKhongGiaHan = $"Phòng {maPhong} đã có phiếu đặt {keTiep.MaPhieuDat} từ {keTiep.NgayDonDuKien:dd/MM/yyyy}: chỉ gia hạn tối đa tới ngày đó, hoặc đổi phòng."; }
        else dto.CoTheGiaHan = true;
        return dto;
    }

    private static string GhepGhiChu(string? ghiChu, string them)
    {
        var s = string.IsNullOrWhiteSpace(ghiChu) ? them : $"{ghiChu.Trim()}; {them}";
        return s.Length > 255 ? s[..255] : s;
    }

    /// <summary>Danh sách minibar buồng phòng đã ghi nhận mà lễ tân chưa tính phí/bỏ qua và đang chặn trả phòng này.</summary>
    private async Task<List<string>> LayMinibarChanTraPhongAsync(string maPhieuNhan, string maPhong, bool phongKhacDangO)
        => (await _context.ChiTietKiemPhongs
            .Where(c => c.TrangThaiTinhPhi == TrangThaiTinhPhi.CHO_TINH_PHI && c.PhieuKiemPhong.MaPhieuNhan == maPhieuNhan
                        && (!phongKhacDangO || c.PhieuKiemPhong.MaPhong == maPhong))
            .Select(c => new { c.VatDung.TenVatDung, c.SoLuongTinhPhi }).ToListAsync())
            .Select(x => $"{x.TenVatDung} x{x.SoLuongTinhPhi}").ToList();

    public async Task<CheckOutInfoDto> XemTruocTraPhongAsync(string maPhieuNhan, string maPhong, bool apDungPhuThuTreGio)
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var luot = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Include(x => x.Phong).ThenInclude(p => p.LoaiPhong)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(p => p!.Khach)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        var phieuDat = luot.PhieuNhanPhong.PhieuDatPhong;
        var khach = luot.PhieuNhanPhong.Khach ?? phieuDat?.Khach;
        var ngayTraDuKien = luot.NgayTraDuKien ?? phieuDat?.NgayTraDuKien;

        var cacLuot = await _context.ChiTietPhieuNhans.AsNoTracking().Where(x => x.MaPhieuNhan == maPhieuNhan).OrderBy(x => x.NgayNhan).ThenBy(x => x.MaPhong).ToListAsync();
        var doiDen = (await _context.PhieuDoiPhongs.AsNoTracking().Where(x => x.MaPhieuNhan == maPhieuNhan).Select(x => new { x.MaPhongCu, x.MaPhongMoi }).ToListAsync())
            .ToDictionary(x => x.MaPhongCu, x => x.MaPhongMoi);
        var phongKhacDangO = cacLuot.Any(x => x.MaPhong != maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);

        var folio = await FolioCalculator.ComputeAsync(_context, maPhieuNhan, homNay, chotLuuTru: !phongKhacDangO);
        var lines = folio.Lines.Select(l => new FolioLineDto { LoaiKhoanMuc = l.Loai.ToString(), MoTa = l.MoTa, SoLuong = l.SoLuong, DonGia = l.DonGia, ThanhTien = l.ThanhTien }).ToList();
        decimal phuThuTre = 0;
        var phuThu = folio.PhuThu; var tongCong = folio.TongCong; var conPhaiThu = folio.ConPhaiThu;
        if (apDungPhuThuTreGio && luot.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
        {
            phuThuTre = luot.DonGia;
            phuThu += phuThuTre; tongCong += phuThuTre;
            conPhaiThu = Math.Max(0, tongCong - folio.TongKhuyenMai - folio.TienDaCoc);
            lines.Add(new FolioLineDto { LoaiKhoanMuc = LoaiKhoanMuc.PHU_THU.ToString(), MoTa = $"Phụ thu trả phòng trễ giờ - phòng {maPhong} (1 đêm, sẽ tính nếu xác nhận)", SoLuong = 1, DonGia = phuThuTre, ThanhTien = phuThuTre });
        }

        var soDemO = Math.Max((luot.NgayTra ?? homNay).DayNumber - luot.NgayNhan.DayNumber, 1);
        var soNgayLech = ngayTraDuKien is { } nt ? homNay.DayNumber - nt.DayNumber : 0;
        var thoiDiem = ngayTraDuKien is null ? "CHUA_KHAI" : soNgayLech > 0 ? "TRE" : soNgayLech < 0 ? "SOM" : "DUNG_HAN";
        var maHoaDon = await _context.HoaDons.Where(x => x.MaPhieuNhan == maPhieuNhan).Select(x => x.MaHD).FirstOrDefaultAsync();

        var dto = new CheckOutInfoDto
        {
            MaPhieuNhan = maPhieuNhan, MaPhong = maPhong, TenLoaiPhong = luot.Phong.LoaiPhong.TenLoai, MaPhieuDat = luot.PhieuNhanPhong.MaPhieuDat,
            MaKhach = khach?.MaKhach, HoTenKhach = khach?.HoTen, SoDTKhach = khach?.SoDT, SoNguoi = luot.SoNguoi,
            NgayNhan = luot.NgayNhan, NgayTraDuKien = ngayTraDuKien, HomNay = homNay, SoDemO = soDemO, ThoiDiemTra = thoiDiem, SoNgayLech = soNgayLech,
            TrangThaiLuot = luot.TrangThai.ToString(), MaHoaDonDaCo = maHoaDon, LaPhongCuoi = !phongKhacDangO, TinhLuc = bayGio,
            PhuThuTreGioApDung = phuThuTre,
            TongHopFolio = new FolioSummaryDto
            {
                TienPhong = folio.TienPhong, PhuThu = phuThu, TienDichVu = folio.TienDichVu, TongCong = tongCong,
                TongKhuyenMai = folio.TongKhuyenMai, TienDaCoc = folio.TienDaCoc, ConPhaiThu = conPhaiThu
            },
            ChiTiet = lines,
            PhongKhac = cacLuot.Where(x => x.MaPhong != maPhong).Select(x => new CheckOutOtherRoomDto
            {
                MaPhong = x.MaPhong, TrangThai = x.TrangThai.ToString(), NgayNhan = x.NgayNhan, NgayTra = x.NgayTra,
                GhiChu = doiDen.TryGetValue(x.MaPhong, out var den) ? $"đã đổi sang {den}" : null
            }).ToList()
        };

        // Giải thích số còn phải thu (đặc biệt khi 0đ).
        var gt = $"{tongCong:N0}đ (phòng {folio.TienPhong:N0} + phụ thu {phuThu:N0} + dịch vụ {folio.TienDichVu:N0})";
        if (folio.TongKhuyenMai > 0) gt += $" − khuyến mãi {folio.TongKhuyenMai:N0}đ";
        if (folio.GhiChuKhuyenMai is not null) gt += " " + folio.GhiChuKhuyenMai;
        if (folio.TienDaCoc > 0) gt += $" − tiền cọc đã thu {folio.TienDaCoc:N0}đ";
        gt += $" = còn phải thu {conPhaiThu:N0}đ.";
        if (conPhaiThu == 0)
            gt += folio.TienDaCoc >= tongCong - folio.TongKhuyenMai && folio.TienDaCoc > 0
                ? " Bằng 0đ vì tiền cọc đã thu đủ cho toàn bộ chi phí" + (folio.TienDaCoc > tongCong - folio.TongKhuyenMai ? $" (cọc dư {folio.TienDaCoc - (tongCong - folio.TongKhuyenMai):N0}đ cần trả lại khách, ghi nhật ký khi lập hóa đơn)." : ".")
                : " Bằng 0đ vì chưa phát sinh khoản nào.";
        if (phongKhacDangO) gt += " (Số này tính cho cả phiếu nhận; hóa đơn chỉ lập khi trả phòng cuối cùng.)";
        gt += " Hệ thống không áp thuế/phí khác ngoài các khoản trên.";
        dto.GiaiThichConPhaiThu = gt;

        // Điều kiện trả phòng.
        var dk = dto.DieuKien;
        var dangO = luot.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O;
        dk.Add(new CheckOutConditionDto { Ten = "Lượt ở đang hoạt động (chưa trả)", Dat = dangO, Chan = true, ChiTiet = dangO ? null : "Lượt lưu trú này đã trả phòng, không trả hai lần được." });
        dk.Add(new CheckOutConditionDto { Ten = "Chưa lập hóa đơn cho phiếu nhận", Dat = maHoaDon is null, Chan = true, ChiTiet = maHoaDon is null ? null : $"Đã có hóa đơn {maHoaDon}." });
        var minibar = dangO ? await LayMinibarChanTraPhongAsync(maPhieuNhan, maPhong, phongKhacDangO) : new List<string>();
        dk.Add(new CheckOutConditionDto { Ten = "Minibar buồng phòng đã được tính phí hoặc bỏ qua", Dat = minibar.Count == 0, Chan = true, ChiTiet = minibar.Count == 0 ? null : $"Còn {minibar.Count} khoản chờ xử lý: {string.Join(", ", minibar)} - xử lý ở Buồng phòng → Minibar chờ tính phí." });
        var phongKhop = luot.Phong.TinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD;
        dk.Add(new CheckOutConditionDto { Ten = "Tình trạng phòng khớp lượt ở (có khách)", Dat = !dangO || phongKhop, Chan = false, ChiTiet = !dangO || phongKhop ? null : $"Phòng đang ở trạng thái {luot.Phong.TinhTrang} nhưng lượt ở còn mở: kiểm tra Sơ đồ phòng." });
        dk.Add(new CheckOutConditionDto { Ten = "Folio được tính lại ngay lúc này", Dat = true, Chan = false, ChiTiet = $"Tính lúc {bayGio:HH:mm:ss}; nếu có thay đổi trước khi xác nhận hệ thống sẽ yêu cầu xem lại." });

        if (thoiDiem == "TRE") dto.CanhBao.Add($"Khách ở QUÁ ngày trả dự kiến {soNgayLech} ngày: tiền phòng đã tính tới hôm nay; cân nhắc phụ thu trễ giờ nếu khách trả sau giờ quy định.");
        if (thoiDiem == "SOM") dto.CanhBao.Add($"Khách trả SỚM {-soNgayLech} ngày so với dự kiến: chỉ tính tiền tới hôm nay; phần cọc dư (nếu có) cần trả lại khách và được ghi nhật ký khi lập hóa đơn.");
        if (phongKhacDangO) dto.CanhBao.Add($"Phiếu nhận còn {cacLuot.Count(x => x.MaPhong != maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)} phòng khác đang ở: trả phòng này CHƯA lập hóa đơn.");
        if (!phongKhacDangO && dangO) dto.CanhBao.Add("Đây là phòng cuối của phiếu nhận: xác nhận sẽ LẬP HÓA ĐƠN và đóng phiếu đặt.");
        if (dto.PhongKhac.Any(x => x.GhiChu is not null)) dto.CanhBao.Add("Khách đã đổi phòng trong lượt ở: folio gồm cả tiền các phòng đã ở trước đó.");

        var chan = dk.FirstOrDefault(x => x.Chan && !x.Dat);
        dto.DuocTra = chan is null;
        dto.LyDoKhongTra = chan?.ChiTiet ?? (chan is null ? null : chan.Ten);
        return dto;
    }

    public async Task<CheckOutResultDto> CheckOutAsync(string maPhieuNhan, string maPhong, CheckOutRequest request, CurrentUser user)
    {
        if (string.IsNullOrEmpty(user.MaNV))
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Tài khoản không gắn với nhân viên nào.");

        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Khóa phiếu nhận: các phòng của cùng phiếu trả đồng thời sẽ được xử lý tuần tự, tránh lập 2 hóa đơn hoặc bỏ sót hóa đơn.
        if (!await PhieuNhanLock.KhoaAsync(_context, maPhieuNhan))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        var phieuNhan = await _context.PhieuNhanPhongs.FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");

        var luot = await _context.ChiTietPhieuNhans
            .Include(x => x.Phong)
            .FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        if (luot.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng.");

        // Kiểm tra minibar: khoản tiêu thụ buồng phòng đã ghi nhận mà lễ tân chưa tính phí / bỏ qua thì chưa được trả phòng (phòng cuối của phiếu thì tính cả các phòng đã đổi đi).
        var phongKhacDangO = await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong != maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var minibarChua = await LayMinibarChanTraPhongAsync(maPhieuNhan, maPhong, phongKhacDangO);
        if (minibarChua.Count > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "CON_MINIBAR_CHUA_XU_LY",
                $"Còn {minibarChua.Count} khoản minibar do buồng phòng ghi nhận chưa tính phí hoặc bỏ qua ({string.Join(", ", minibarChua)}). Hãy xử lý ở mục minibar chờ tính phí trước khi trả phòng.");

        luot.NgayTra = homNay;
        luot.TrangThai = TrangThaiChiTietPhieuNhan.DA_TRA_PHONG;
        if (request.ApDungPhuThuTreGio)
            luot.TienPhuThu += luot.DonGia;
        luot.Phong.TinhTrang = TinhTrangPhong.VD;
        _audit.LyDoTinhTrangPhong = "Trả phòng";
        await _context.SaveChangesAsync();

        var conPhongDangO = await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var folio = await FolioCalculator.ComputeAsync(_context, maPhieuNhan, homNay, chotLuuTru: !conPhongDangO);

        // Folio đã đổi so với lúc lễ tân xem (vừa ghi/hủy dịch vụ, đổi phòng, gia hạn...): không trả phòng theo số cũ. Transaction chưa commit nên không có gì bị ghi.
        if (request.ConPhaiThuDaXem is { } daXem && Math.Round(daXem, 0) != Math.Round(folio.ConPhaiThu, 0))
            throw new ApiException(StatusCodes.Status409Conflict, "FOLIO_DA_THAY_DOI",
                $"Folio vừa thay đổi: bạn đã xem còn phải thu {Math.Round(daXem, 0):N0}đ nhưng hiện là {Math.Round(folio.ConPhaiThu, 0):N0}đ. Hãy xem lại folio rồi xác nhận lại; chưa trả phòng.");

        // Trả hết phòng của phiếu đặt (đã nhận đủ phòng) thì phiếu đặt chuyển sang DA_TRA_PHONG (CHECKED_OUT).
        if (!conPhongDangO && phieuNhan.MaPhieuDat is { } maPhieuDatCuaPhieuNhan)
        {
            var phieuDatCuaLuot = await _context.PhieuDatPhongs.FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDatCuaPhieuNhan);
            if (phieuDatCuaLuot is { TrangThai: TrangThaiPhieuDat.DA_NHAN_PHONG })
                phieuDatCuaLuot.TrangThai = TrangThaiPhieuDat.DA_TRA_PHONG;
        }

        string? maHoaDon = null;
        if (!conPhongDangO && !await _context.HoaDons.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan))
            maHoaDon = await LapHoaDonAsync(folio, user.MaNV, bayGio);

        var soDemO = Math.Max(homNay.DayNumber - luot.NgayNhan.DayNumber, 1);
        _audit.Ghi("TRA_PHONG", maPhieuNhan,
            $"phong={maPhong}; hoaDon={maHoaDon ?? "chua lap (con phong dang o)"}; phuThuTreGio={request.ApDungPhuThuTreGio}; soDem={soDemO}; tongCong={Math.Round(folio.TongCong, 0)}; khuyenMai={Math.Round(folio.TongKhuyenMai, 0)}; daCoc={Math.Round(folio.TienDaCoc, 0)}; conPhaiThu={Math.Round(folio.ConPhaiThu, 0)}; phongTrang=VD");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        string? trangThaiHoaDon = null;
        if (maHoaDon is not null)
            trangThaiHoaDon = (await _context.HoaDons.AsNoTracking().Where(x => x.MaHD == maHoaDon).Select(x => x.TrangThaiThanhToan).FirstAsync()).ToString();
        string? trangThaiPhieuDat = null;
        if (phieuNhan.MaPhieuDat is { } maPD)
            trangThaiPhieuDat = (await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.MaPhieuDat == maPD).Select(x => x.TrangThai).FirstAsync()).ToString();

        return new CheckOutResultDto
        {
            MaPhieuNhan = maPhieuNhan,
            MaPhong = maPhong,
            TrangThai = nameof(TrangThaiChiTietPhieuNhan.DA_TRA_PHONG),
            TinhTrangPhong = nameof(TinhTrangPhong.VD),
            MaHoaDon = maHoaDon,
            TongHopFolio = ToSummary(folio),
            ThoiGianTra = bayGio,
            NguoiThucHien = _audit.TenDN,
            SoPhongConDangO = await _context.ChiTietPhieuNhans.CountAsync(x => x.MaPhieuNhan == maPhieuNhan && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O),
            TrangThaiPhieuDat = trangThaiPhieuDat,
            TrangThaiHoaDon = trangThaiHoaDon,
            SoDemO = soDemO
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
        // [Khối 8 - P1-4] 2 lễ tân trả phòng cho 2 phiếu nhận khác nhau cùng lúc cùng sinh mã HDyyyyMMdd-NNNN;
        // khóa theo tiền tố tới hết transaction trả phòng (sau khóa phiếu nhận).
        var maHD = await MaCodeGenerator.GenerateNextAsync(_context,
            _context.HoaDons.Where(x => x.MaHD.StartsWith(tienTo)).Select(x => x.MaHD), tienTo, 4);

        // Cọc đã thu lớn hơn tiền phải trả (trả phòng sớm): hóa đơn chỉ ghi phần cọc được dùng (để "các khoản − cọc = phải thu" luôn khớp)
        // và phần dư được ghi nhật ký (COC_DU_CHO_HOAN) để lễ tân trả lại khách ngoài hệ thống.
        var phaiTraSauKm = Math.Max(0, folio.TongCong - folio.TongKhuyenMai);
        var cocDaDung = Math.Min(folio.TienDaCoc, phaiTraSauKm);
        var cocDu = folio.TienDaCoc - cocDaDung;

        var hoaDon = new HoaDon
        {
            MaHD = maHD,
            MaPhieuNhan = folio.MaPhieuNhan,
            MaNV = maNV,
            NgayLap = bayGio,
            TienPhong = folio.TienPhong,
            TienDV = folio.TienDichVu,
            PhuThu = folio.PhuThu,
            TienDaCoc = cocDaDung,
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
        if (cocDu > 0 && folio.MaPhieuDat is { } maPhieuDatCoc)
        {
            _audit.Ghi("COC_DU_CHO_HOAN", maHD, $"phieuDat={maPhieuDatCoc}; cocDaThu={Math.Round(folio.TienDaCoc, 0)}; cocDaDung={Math.Round(cocDaDung, 0)}; cocDu={Math.Round(cocDu, 0)}");
        }
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
