using KLCN060.Api.DTOs.Rooms;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Phần "sơ đồ phòng cho người điều hành" của RoomService: bổ sung cho mỗi phòng khách đang ở / khách sắp đến / sự cố / cảnh báo cần xử lý,
/// chi tiết một phòng (vì sao đang ở tình trạng này, ai đổi, việc tiếp theo) và cảnh báo ảnh hưởng đặt phòng khi khóa phòng.
/// </summary>
public partial class RoomService
{
    private static readonly TrangThaiPhieuDat[] PhieuDatConHieuLuc = { TrangThaiPhieuDat.CHO_XAC_NHAN, TrangThaiPhieuDat.DA_XAC_NHAN };
    private static readonly TrangThaiSuCo[] SuCoDangMo = { TrangThaiSuCo.MOI, TrangThaiSuCo.DA_TIEP_NHAN, TrangThaiSuCo.DANG_XU_LY };

    private async Task BoSungThongTinSoDoAsync(List<RoomDto> phongs)
    {
        if (phongs.Count == 0) return;
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var ngayMai = homNay.AddDays(1);

        var dangO = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => new
            {
                x.MaPhong,
                HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen
                    : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : null
            }).ToListAsync();

        var sapDen = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => (ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                         && ct.PhieuDatPhong.NgayDonDuKien <= ngayMai && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .Select(ct => new { ct.MaPhong, ct.MaPhieuDat, ct.PhieuDatPhong.NgayDonDuKien, HoTen = ct.PhieuDatPhong.Khach.HoTen }).ToListAsync();

        var suCo = await _context.SuCoPhongs.AsNoTracking().Where(x => SuCoDangMo.Contains(x.TrangThai))
            .Select(x => new { x.MaPhong, x.MucDo }).ToListAsync();

        foreach (var p in phongs)
        {
            p.KhachHienTai = dangO.FirstOrDefault(x => x.MaPhong == p.MaPhong)?.HoTen;
            var den = sapDen.Where(x => x.MaPhong == p.MaPhong).OrderBy(x => x.NgayDonDuKien).FirstOrDefault();
            if (den is not null)
            {
                p.KhachSapDen = den.HoTen;
                p.NgayDonSapDen = den.NgayDonDuKien;
                p.MaPhieuDatSapDen = den.MaPhieuDat;
            }
            p.SuCoDangMo = suCo.Count(x => x.MaPhong == p.MaPhong);
            var soGio = p.TinhTrangTuLuc is null ? (double?)null : (bayGio - p.TinhTrangTuLuc.Value).TotalHours;
            (p.CanhBao, p.MucCanhBao) = DanhGiaCanhBao(p.TinhTrang, den is null ? null : (den.HoTen, den.NgayDonDuKien), homNay, soGio, suCo.Count(x => x.MaPhong == p.MaPhong && x.MucDo is MucDoSuCo.CAO or MucDoSuCo.KHAN_CAP), p.KhachHienTai is not null || dangO.Any(x => x.MaPhong == p.MaPhong));
        }
    }

    /// <summary>Quy tắc cảnh báo dùng chung cho sơ đồ và chi tiết phòng: khách sắp đến mà phòng chưa nhận được &gt; bảo trì lâu &gt; chưa dọn lâu &gt; sự cố nặng.</summary>
    private (string? CanhBao, string? Muc) DanhGiaCanhBao(string tinhTrang, (string HoTen, DateOnly Ngay)? den, DateOnly homNay, double? soGio, int suCoNang, bool coLuotDangO)
    {
        // Tình trạng phòng phải khớp lượt lưu trú: "có khách" luôn gắn với một lượt đang ở và ngược lại (API không cho đặt tay OC/trống sai, đây là lớp kiểm tra dữ liệu).
        var phongCoKhach = tinhTrang is "OC" or "OD";
        if (phongCoKhach && !coLuotDangO)
            return ("Dữ liệu lệch: phòng ghi là có khách nhưng không có lượt lưu trú nào đang ở - kiểm tra nhận/trả phòng", "NGUY_HIEM");
        if (!phongCoKhach && coLuotDangO)
            return ("Dữ liệu lệch: có lượt lưu trú đang ở nhưng phòng không ghi là có khách - kiểm tra nhận/trả phòng", "NGUY_HIEM");
        var denHomNay = den is { } d && d.Ngay <= homNay;
        if (den is { } khach && tinhTrang is "OOO" or "VD" or "OD" or "OC")
        {
            if (tinhTrang == "OOO") return ($"Khách {khach.HoTen} đến {(denHomNay ? "hôm nay" : "ngày mai")} nhưng phòng đang bảo trì - cần đổi phòng", "NGUY_HIEM");
            if (denHomNay && tinhTrang is "VD" or "OD") return ($"Khách {khach.HoTen} đến hôm nay, phòng chưa dọn xong", "NGUY_HIEM");
            if (denHomNay && tinhTrang == "OC") return ($"Khách {khach.HoTen} đến hôm nay nhưng phòng vẫn còn khách ở", "NGUY_HIEM");
            if (tinhTrang is "VD" or "OD") return ($"Khách {khach.HoTen} đến ngày mai, phòng chưa dọn", "CANH_BAO");
        }
        if (den is { } quaHan && quaHan.Ngay < homNay)
            return ($"Khách {quaHan.HoTen} quá ngày đón ({quaHan.Ngay:dd/MM}) mà chưa nhận phòng - liên hệ khách hoặc xử lý không đến", "CANH_BAO");
        if (tinhTrang == "OOO" && soGio >= _alert.MaintenanceDays * 24) return ($"Bảo trì quá {_alert.MaintenanceDays} ngày", "CANH_BAO");
        if (tinhTrang == "VD" && soGio >= _alert.DirtyRoomHours) return ($"Chưa dọn quá {_alert.DirtyRoomHours} giờ", "CANH_BAO");
        if (tinhTrang == "OD") return ("Có khách, cần dọn", "CANH_BAO");
        if (suCoNang > 0) return ($"{suCoNang} sự cố mức cao/khẩn cấp chưa xử lý", "CANH_BAO");
        return (null, null);
    }

    public async Task<RoomDetailDto> GetDetailAsync(string maPhong)
    {
        var phong = await _context.Phongs.AsNoTracking().Include(x => x.LoaiPhong).FirstOrDefaultAsync(x => x.MaPhong == maPhong)
            ?? throw new Middlewares.ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);

        var lichSu = await _context.LichSuTinhTrangPhongs.AsNoTracking().Where(x => x.MaPhong == maPhong)
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLichSu).Take(1).FirstOrDefaultAsync();
        var ten = await TenNhanVien.TraCuuAsync(_context, new[] { lichSu?.MaTaiKhoan });

        var dto = new RoomDetailDto
        {
            MaPhong = phong.MaPhong, TenPhong = phong.TenPhong, Tang = phong.Tang, TenLoaiPhong = phong.LoaiPhong.TenLoai, TinhTrang = phong.TinhTrang.ToString(),
            TinhTrangTuLuc = lichSu?.ThoiGian, LyDoTinhTrang = lichSu?.LyDo, NguoiDoi = lichSu?.MaTaiKhoan,
            HoTenNguoiDoi = lichSu?.MaTaiKhoan is null ? null : ten.GetValueOrDefault(lichSu.MaTaiKhoan),
            TinhTrangTruoc = lichSu?.TuTinhTrang.ToString()
        };

        var luot = await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => new
            {
                x.MaPhieuNhan, x.NgayNhan, x.NgayTraDuKien, x.SoNguoi, x.PhieuNhanPhong.MaPhieuDat,
                HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : "(khách vãng lai)",
                SoDT = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.SoDT : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.SoDT : null
            }).FirstOrDefaultAsync();
        if (luot is not null)
            dto.KhachDangO = new RoomStayDto { HoTen = luot.HoTen, SoDT = luot.SoDT, MaPhieuNhan = luot.MaPhieuNhan, MaPhieuDat = luot.MaPhieuDat, NgayNhan = luot.NgayNhan, NgayTraDuKien = luot.NgayTraDuKien, SoNguoi = luot.SoNguoi };

        var datTruoc = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => ct.MaPhong == maPhong && (ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                         && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .OrderBy(ct => ct.PhieuDatPhong.NgayDonDuKien).Take(5)
            .Select(ct => new RoomReservationDto
            {
                MaPhieuDat = ct.MaPhieuDat, HoTen = ct.PhieuDatPhong.Khach.HoTen, SoDT = ct.PhieuDatPhong.Khach.SoDT,
                NgayDon = ct.PhieuDatPhong.NgayDonDuKien, NgayTra = ct.PhieuDatPhong.NgayTraDuKien, TrangThai = ct.PhieuDatPhong.TrangThai.ToString(), TienCoc = ct.PhieuDatPhong.TienCoc
            }).ToListAsync();
        dto.DatPhongSapToi = datTruoc;

        var suCo = await _context.SuCoPhongs.AsNoTracking().Where(x => x.MaPhong == maPhong && SuCoDangMo.Contains(x.TrangThai))
            .OrderByDescending(x => x.MucDo).ThenBy(x => x.ThoiGianBao).Take(5)
            .Select(x => new RoomIncidentBriefDto { MaSuCo = x.MaSuCo, TieuDe = x.TieuDe, MucDo = x.MucDo.ToString(), TrangThai = x.TrangThai.ToString() }).ToListAsync();
        dto.SuCo = suCo;

        var phieuDon = await _context.PhieuDonPhongs.AsNoTracking().Where(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiPhieuDon.DANG_DON).FirstOrDefaultAsync();
        if (phieuDon is not null)
        {
            var tenDon = (await TenNhanVien.TraCuuAsync(_context, new[] { phieuDon.NguoiDon })).GetValueOrDefault(phieuDon.NguoiDon) ?? phieuDon.NguoiDon;
            dto.NguoiDangDon = phieuDon.ThoiGianBatDau is null ? $"{tenDon} (được giao, chưa bắt đầu)" : $"{tenDon} (từ {phieuDon.ThoiGianBatDau:HH:mm})";
        }

        var den = datTruoc.FirstOrDefault(x => x.NgayDon <= homNay.AddDays(1));
        var soGio = lichSu is null ? (double?)null : (bayGio - lichSu.ThoiGian).TotalHours;
        (dto.CanhBao, dto.MucCanhBao) = DanhGiaCanhBao(dto.TinhTrang, den is null ? null : (den.HoTen, den.NgayDon), homNay, soGio,
            suCo.Count(x => x.MucDo is nameof(MucDoSuCo.CAO) or nameof(MucDoSuCo.KHAN_CAP)), luot is not null);

        dto.ViecTiepTheo = phong.TinhTrang switch
        {
            TinhTrangPhong.VD => phieuDon is null ? "Phòng bẩn chưa ai nhận: vào Buồng phòng để giao việc dọn (hoặc để buồng phòng tự nhận)." : "Đang chờ buồng phòng dọn xong rồi xác nhận phòng sạch.",
            TinhTrangPhong.VC => "Phòng đã dọn: kiểm tra chất lượng (VC → VI) trước khi bán, hoặc nhận phòng ngay nếu khách đang chờ.",
            TinhTrangPhong.VI => den is null ? "Phòng sẵn sàng bán." : $"Phòng sẵn sàng đón khách {den.HoTen} (đón {den.NgayDon:dd/MM}).",
            TinhTrangPhong.OC => luot?.NgayTraDuKien is { } tra ? $"Khách đang ở, dự kiến trả phòng {tra:dd/MM}. Phòng chuyển 'Có khách, cần dọn' khi cần dọn." : "Khách đang ở.",
            TinhTrangPhong.OD => "Phòng có khách cần dọn: buồng phòng dọn rồi xác nhận (OD → OC).",
            _ => dto.SuCo.Count > 0 ? $"Phòng đang bảo trì, còn {dto.SuCo.Count} sự cố chưa xử lý xong: xử lý sự cố rồi mở lại phòng (OOO → VD)." : "Phòng đang bảo trì/khóa: mở lại (OOO → VD) khi đã xong để buồng phòng dọn trước khi bán."
        };
        return dto;
    }

    /// <summary>Đoạn ghi thêm vào nhật ký khi khóa phòng: các phiếu đặt còn hiệu lực dùng phòng này từ hôm nay trở đi (để biết khóa phòng đã ảnh hưởng ai).</summary>
    private async Task<string> DatPhongBiAnhHuongAsync(string maPhong)
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var phieu = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => ct.MaPhong == maPhong && PhieuDatConHieuLuc.Contains(ct.PhieuDatPhong.TrangThai) && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .OrderBy(ct => ct.PhieuDatPhong.NgayDonDuKien).Take(5)
            .Select(ct => new { ct.MaPhieuDat, ct.PhieuDatPhong.NgayDonDuKien }).ToListAsync();
        return phieu.Count == 0 ? "" : $"; anhHuongDatPhong={string.Join(",", phieu.Select(x => $"{x.MaPhieuDat}({x.NgayDonDuKien:yyyy-MM-dd})"))}";
    }
}
