using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Tổng quan một phiếu đặt phòng cho nhân viên / Quản lý: gom khách, lưu trú, phòng, thanh toán (đã thu / còn lại), ai tạo / ai sửa, chính sách hủy,
/// bảng thao tác được phép (kèm lý do khi không được) và lịch sử gần nhất - để xử lý một phiếu mà không phải mở module khác.
/// </summary>
public partial class BookingService
{
    private const int SoNgayHoanCocMacDinh = 30;   // trùng CancelAsync

    public async Task<BookingOverviewDto> GetOverviewAsync(string maPhieuDat, CurrentUser user)
    {
        if (user.LaKhachHang)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_CO_QUYEN_TRUY_CAP", "Chỉ nhân viên xem được tổng quan phiếu đặt phòng.");

        var p = await _context.PhieuDatPhongs.AsNoTracking().AsSplitQuery()
            .Include(x => x.Khach)
            .Include(x => x.ChiTietPhieuDats).ThenInclude(ct => ct.Phong).ThenInclude(ph => ph.LoaiPhong)
            .Include(x => x.ChiTietKhuyenMais)
            .Include(x => x.KhachDiCungs)
            .Include(x => x.PhieuNhanPhong).ThenInclude(pn => pn!.ChiTietPhieuNhans).ThenInclude(c => c.Phong).ThenInclude(ph => ph.LoaiPhong)
            .Include(x => x.PhieuNhanPhong).ThenInclude(pn => pn!.HoaDons)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var tenNguoiHuy = await TenNguoiDung.HienThiAsync(_context, p.HuyBoi);
        var soDem = Math.Max(p.NgayTraDuKien.DayNumber - p.NgayDonDuKien.DayNumber, 0);
        var luot = p.PhieuNhanPhong?.ChiTietPhieuNhans.ToList() ?? new List<ChiTietPhieuNhan>();

        var dto = new BookingOverviewDto
        {
            MaPhieuDat = p.MaPhieuDat, TrangThai = p.TrangThai.ToString(), NguonDat = p.NguonDat.ToString(), LoaiDatPhong = p.LoaiDatPhong.ToString(),
            MaKhach = p.MaKhach, HoTenKhach = p.Khach.HoTen, SoDTKhach = p.Khach.SoDT, EmailKhach = p.Khach.Email, CCCDKhach = p.Khach.CCCD,
            SoNguoiOCung = p.KhachDiCungs.Count,
            SoNguoi = SoNguoiThucTe(luot) is { } thucTe ? Math.Max(thucTe, 1) : 1 + p.KhachDiCungs.Count,
            NgayDonDuKien = p.NgayDonDuKien, NgayTraDuKien = p.NgayTraDuKien, SoDem = soDem, MaPhieuNhan = p.PhieuNhanPhong?.MaPhieuNhan,
            TienCoc = p.TienCoc, DaThuCoc = p.NgayNhanCoc != null, NgayThuCoc = p.NgayNhanCoc, HinhThucCoc = p.HinhThucCoc?.ToString(), NguoiThuCoc = p.NguoiNhanCoc,
            NgayTao = p.NgayDat, NgayHuy = p.NgayHuy, LyDoHuy = p.LyDoHuy, HuyBoi = tenNguoiHuy,
            QuaHanChuaNhan = p.ConGiuPhong() && p.NgayDonDuKien < homNay && p.PhieuNhanPhong is null
        };

        dto.Phong = p.ChiTietPhieuDats.OrderBy(x => x.MaPhong, StringComparer.Ordinal).Select(ct =>
        {
            var l = luot.Where(x => x.MaPhong == ct.MaPhong).OrderByDescending(x => x.NgayNhan).FirstOrDefault();
            return new BookingRoomOverviewDto
            {
                MaPhong = ct.MaPhong, TenPhong = ct.Phong.TenPhong, TenLoaiPhong = ct.Phong.LoaiPhong.TenLoai, Tang = ct.Phong.Tang,
                DonGiaApDung = ct.DonGiaApDung, ThanhTien = ct.ThanhTien, TinhTrangPhong = ct.Phong.TinhTrang.ToString(),
                LuuTru = l is null ? "CHUA_NHAN" : l.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O ? "DANG_O" : "DA_TRA", SoNguoi = l?.SoNguoi
            };
        }).ToList();

        // Phòng khách thực tế ở nhưng không nằm trong phiếu đặt ban đầu (đã đổi phòng / nâng hạng): vẫn phải thấy được "phòng nào".
        foreach (var l in luot.Where(x => p.ChiTietPhieuDats.All(ct => ct.MaPhong != x.MaPhong)).OrderBy(x => x.NgayNhan))
            dto.Phong.Add(new BookingRoomOverviewDto
            {
                MaPhong = l.MaPhong, TenPhong = l.Phong.TenPhong + " (đổi phòng)", TenLoaiPhong = l.Phong.LoaiPhong.TenLoai, Tang = l.Phong.Tang,
                DonGiaApDung = l.DonGia, ThanhTien = 0, TinhTrangPhong = l.Phong.TinhTrang.ToString(),
                LuuTru = l.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O ? "DANG_O" : "DA_TRA", SoNguoi = l.SoNguoi
            });

        // ---- Tiền
        dto.TongTienPhong = p.ChiTietPhieuDats.Sum(x => x.ThanhTien);
        dto.TongKhuyenMai = p.ChiTietKhuyenMais.Sum(x => x.TongKhuyenMai);
        dto.TongSauKhuyenMai = Math.Max(dto.TongTienPhong - dto.TongKhuyenMai, 0);

        var hoaDons = p.PhieuNhanPhong?.HoaDons.ToList() ?? new List<HoaDon>();
        var maHDs = hoaDons.Select(x => x.MaHD).ToList();
        var daThuTheoHD = maHDs.Count == 0 ? new Dictionary<string, decimal>()
            : await _context.ChiTietThanhToans.AsNoTracking().Where(x => maHDs.Contains(x.MaHD)).GroupBy(x => x.MaHD)
                .Select(g => new { MaHD = g.Key, Tong = g.Sum(x => x.SoTien) }).ToDictionaryAsync(x => x.MaHD, x => x.Tong);
        dto.HoaDon = hoaDons.OrderBy(x => x.NgayLap).Select(h =>
        {
            var daThu = daThuTheoHD.GetValueOrDefault(h.MaHD);
            return new BookingInvoiceOverviewDto { MaHD = h.MaHD, NgayLap = h.NgayLap, TongTien = h.TongTien, DaThu = daThu, ConNo = h.TongTien - daThu, TrangThai = h.TrangThaiThanhToan.ToString() };
        }).ToList();

        var huy = p.TrangThai is TrangThaiPhieuDat.DA_HUY or TrangThaiPhieuDat.KHONG_DEN;
        var tienCocDaNhan = dto.DaThuCoc && !huy ? p.TienCoc : 0m;
        if (hoaDons.Count > 0)
        {
            // Đã có hóa đơn: số liệu theo hóa đơn (tổng hóa đơn đã tính tiền cọc đã đối trừ).
            dto.DaThu = dto.HoaDon.Sum(x => x.DaThu) + (hoaDons.Sum(h => h.TienDaCoc));
            dto.ConLai = Math.Max(dto.HoaDon.Sum(x => x.ConNo), 0);
        }
        else
        {
            dto.DaThu = tienCocDaNhan;
            dto.ConLai = huy ? 0 : Math.Max(dto.TongSauKhuyenMai - tienCocDaNhan, 0);
        }

        if (p.TrangThai == TrangThaiPhieuDat.DA_HUY)
        {
            dto.TrangThaiThanhToan = p.SoTienHoanCoc > 0 ? "DA_HUY_HOAN_COC" : p.TienCocGiuLai > 0 ? "MAT_COC" : "DA_HUY_KHONG_COC";
            dto.TomTatThanhToan = p.SoTienHoanCoc > 0
                ? $"Đã hủy. Hoàn cọc {Tien(p.SoTienHoanCoc)} cho khách (trả lại ngoài hệ thống)."
                : p.TienCocGiuLai > 0 ? $"Đã hủy. Khách sạn giữ lại tiền cọc {Tien(p.TienCocGiuLai)}." : "Đã hủy khi chưa thu cọc - không phát sinh tiền.";
        }
        else if (p.TrangThai == TrangThaiPhieuDat.KHONG_DEN)
        {
            dto.TrangThaiThanhToan = p.TienCocGiuLai > 0 ? "MAT_COC" : "DA_HUY_KHONG_COC";
            dto.TomTatThanhToan = p.TienCocGiuLai > 0 ? $"Khách không đến. Giữ lại tiền cọc {Tien(p.TienCocGiuLai)}." : "Khách không đến, chưa thu cọc nên không phát sinh tiền.";
        }
        else if (hoaDons.Count > 0)
        {
            var conNo = dto.ConLai;
            dto.TrangThaiThanhToan = conNo <= 0 ? "DA_THANH_TOAN" : "CON_NO";
            dto.TomTatThanhToan = conNo <= 0 ? $"Đã thanh toán đủ {Tien(dto.DaThu)}." : $"Đã thu {Tien(dto.DaThu)}, còn nợ {Tien(conNo)}.";
        }
        else if (!dto.DaThuCoc)
        {
            dto.TrangThaiThanhToan = "CHUA_COC";
            dto.TomTatThanhToan = $"Chưa thu cọc (cần {Tien(p.TienCoc)} để giữ phòng). Tổng tiền phòng dự kiến {Tien(dto.TongSauKhuyenMai)}.";
        }
        else
        {
            dto.TrangThaiThanhToan = "DA_COC";
            dto.TomTatThanhToan = $"Đã thu cọc {Tien(p.TienCoc)}" + (p.NgayNhanCoc is null ? "" : $" lúc {p.NgayNhanCoc:dd/MM/yyyy HH:mm}")
                + (p.HinhThucCoc is null ? "" : p.HinhThucCoc == HinhThucThanhToan.CHUYEN_KHOAN ? " (chuyển khoản)" : " (tiền mặt)")
                + (string.IsNullOrEmpty(p.NguoiNhanCoc) ? "" : $", thu bởi {p.NguoiNhanCoc}") + $". Còn phải thu khi trả phòng ≈ {Tien(dto.ConLai)}.";
        }

        // ---- Chính sách hủy: nếu hủy bây giờ thì sao
        dto.SoNgayConLaiToiNgayDon = p.NgayDonDuKien.DayNumber - homNay.DayNumber;
        if (p.ConGiuPhong() && p.PhieuNhanPhong is null)   // chỉ phiếu còn giữ phòng mới còn "nếu hủy bây giờ"
        {
            dto.HoanCocNeuHuyBayGio = dto.SoNgayConLaiToiNgayDon >= SoNgayHoanCocMacDinh;
            dto.SoTienHoanNeuHuy = dto.DaThuCoc && dto.HoanCocNeuHuyBayGio ? p.TienCoc : 0;
            dto.TienGiuLaiNeuHuy = dto.DaThuCoc && !dto.HoanCocNeuHuyBayGio ? p.TienCoc : 0;
        }

        // ---- Ai tạo / ai sửa + lịch sử
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking().Where(x => x.DoiTuongTacDong == maPhieuDat)
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(60).ToListAsync();
        var tenDNs = nhatKy.Select(x => x.MaTaiKhoan).Distinct().ToList();
        var nguoi = await _context.TaiKhoans.AsNoTracking().Where(x => tenDNs.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null, KhachHang = x.NhanVien == null })
            .ToDictionaryAsync(x => x.TenDN);
        string Ten(string tk) => nguoi.TryGetValue(tk, out var n) ? (n.HoTen is { Length: > 0 } t ? $"{t}{(n.KhachHang ? " (khách)" : "")} - {tk}" : tk) : tk;

        var tao = nhatKy.LastOrDefault(x => x.HanhDong == "TAO_DAT_PHONG");
        dto.NguoiTao = tao is null ? (p.NguonDat == NguonDatPhong.WEBSITE ? $"{p.Khach.HoTen} (khách đặt trên website)" : "(không có nhật ký)") : Ten(tao.MaTaiKhoan);
        var sua = nhatKy.Where(x => x.HanhDong == "SUA_DAT_PHONG").ToList();
        dto.SoLanSua = sua.Count;
        if (sua.Count > 0) { dto.NgaySuaCuoi = sua[0].ThoiGian; dto.NguoiSuaCuoi = Ten(sua[0].MaTaiKhoan); }
        dto.LichSuGanNhat = nhatKy.Take(8).Select(x => new BookingHistoryItemDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = Ten(x.MaTaiKhoan), HanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();

        dto.ThaoTac = await TinhThaoTacAsync(p, dto, user);
        return dto;
    }

    /// <summary>Số người đang ở (lượt DANG_O); nếu đã trả hết thì lấy các lượt trả muộn nhất. Null nếu chưa nhận phòng.</summary>
    private static int? SoNguoiThucTe(List<ChiTietPhieuNhan> luot)
    {
        if (luot.Count == 0) return null;
        var dangO = luot.Where(x => x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O).ToList();
        if (dangO.Count > 0) return dangO.Sum(x => x.SoNguoi);
        var cuoi = luot.Max(x => x.NgayTra);
        return luot.Where(x => x.NgayTra == cuoi).Sum(x => x.SoNguoi);
    }

    private static string Tien(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}đ";

    /// <summary>
    /// Bảng thao tác theo trạng thái phiếu + quyền của người xem. Giao diện chỉ bật nút khi ChoPhep = true, nên không còn tình trạng
    /// "nút bấm được nhưng nghiệp vụ không cho" - API vẫn kiểm tra lại ở từng thao tác.
    /// </summary>
    private async Task<List<BookingActionDto>> TinhThaoTacAsync(PhieuDatPhong p, BookingOverviewDto o, CurrentUser user)
    {
        var quyenLeTan = await _permissions.CoQuyenAsync(user.TenDN, Permissions.ThaoTacLeTan);
        var quyenDuyet = await _permissions.CoQuyenAsync(user.TenDN, Permissions.DuyetNghiepVu);
        var conGiu = p.TrangThai is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN;
        var daNhan = p.PhieuNhanPhong is not null || p.TrangThai is TrangThaiPhieuDat.DA_NHAN_PHONG or TrangThaiPhieuDat.DA_TRA_PHONG;
        string LyDoTrangThai() => daNhan ? "Phiếu đã nhận phòng - xử lý ở lượt lưu trú (đổi phòng, gia hạn, trả phòng)."
            : p.TrangThai == TrangThaiPhieuDat.DA_HUY ? "Phiếu đã hủy." : p.TrangThai == TrangThaiPhieuDat.KHONG_DEN ? "Phiếu đã đánh dấu khách không đến." : $"Phiếu đang ở trạng thái {p.TrangThai}.";

        var ds = new List<BookingActionDto>();

        // Xác nhận cọc
        if (p.TrangThai != TrangThaiPhieuDat.CHO_XAC_NHAN)
            ds.Add(new() { Ma = "XAC_NHAN_COC", Ten = "Xác nhận đã thu cọc", GiaiThich = p.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN ? "Đã xác nhận cọc." : LyDoTrangThai() });
        else if (!quyenLeTan)
            ds.Add(new() { Ma = "XAC_NHAN_COC", Ten = "Xác nhận đã thu cọc", GiaiThich = "Chỉ lễ tân thu và xác nhận cọc tiền mặt tại quầy; khách chuyển khoản QR được hệ thống tự xác nhận." });
        else
            ds.Add(new() { Ma = "XAC_NHAN_COC", Ten = "Xác nhận đã thu cọc", ChoPhep = true, GiaiThich = "Chỉ xác nhận khi đã nhận đủ tiền mặt." });

        // Sửa phiếu (đổi ngày / đổi phòng / đổi loại phòng)
        if (!conGiu)
            ds.Add(new() { Ma = "SUA_PHIEU", Ten = "Đổi ngày / đổi phòng", GiaiThich = LyDoTrangThai() });
        else if (!quyenLeTan && !quyenDuyet)
            ds.Add(new() { Ma = "SUA_PHIEU", Ten = "Đổi ngày / đổi phòng", GiaiThich = "Cần quyền lễ tân hoặc Quản lý." });
        else
            ds.Add(new() { Ma = "SUA_PHIEU", Ten = "Đổi ngày / đổi phòng", ChoPhep = true, BatBuocLyDo = true, GiaiThich = "Bắt buộc lý do; giá được tính lại theo bảng giá hiện hành" + (p.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN ? " và tiền cọc tính lại (50%)." : "; tiền cọc đã thu giữ nguyên.") });

        // Hủy phiếu theo chính sách
        if (!conGiu)
            ds.Add(new() { Ma = "HUY", Ten = "Hủy phiếu", GiaiThich = LyDoTrangThai() });
        else if (!quyenLeTan && !quyenDuyet)
            ds.Add(new() { Ma = "HUY", Ten = "Hủy phiếu", GiaiThich = "Cần quyền lễ tân hoặc Quản lý." });
        else
        {
            var tienCocHuy = o.DaThuCoc ? (o.HoanCocNeuHuyBayGio ? $"Hủy bây giờ: HOÀN cọc {Tien(p.TienCoc)}." : $"Hủy bây giờ: KHÔNG hoàn cọc, giữ lại {Tien(p.TienCoc)}.") : "Chưa thu cọc nên hủy không phát sinh tiền.";
            ds.Add(new() { Ma = "HUY", Ten = "Hủy phiếu", ChoPhep = true, BatBuocLyDo = quyenDuyet && !quyenLeTan, GiaiThich = tienCocHuy + " Hoàn cọc theo chính sách (hủy trước 30 ngày thì hoàn), tiền trả lại khách ngoài hệ thống." + (quyenDuyet && !quyenLeTan ? " Quản lý bắt buộc ghi lý do." : "") });
        }

        // Không đến
        if (!conGiu)
            ds.Add(new() { Ma = "KHONG_DEN", Ten = "Đánh dấu khách không đến", GiaiThich = LyDoTrangThai() });
        else if (p.NgayDonDuKien >= DateOnly.FromDateTime(DateTime.Now))
            ds.Add(new() { Ma = "KHONG_DEN", Ten = "Đánh dấu khách không đến", GiaiThich = $"Chưa quá ngày đón ({p.NgayDonDuKien:dd/MM/yyyy}); khách vẫn có thể đến." });
        else if (!quyenLeTan && !quyenDuyet)
            ds.Add(new() { Ma = "KHONG_DEN", Ten = "Đánh dấu khách không đến", GiaiThich = "Cần quyền lễ tân hoặc Quản lý." });
        else
            ds.Add(new() { Ma = "KHONG_DEN", Ten = "Đánh dấu khách không đến", ChoPhep = true, GiaiThich = o.DaThuCoc ? $"Khách mất cọc {Tien(p.TienCoc)}, phòng được trả lại để bán." : "Phòng được trả lại để bán." });

        return ds;
    }
}

internal static class PhieuDatPhongTrangThaiExt
{
    /// <summary>Phiếu còn giữ phòng (chờ xác nhận hoặc đã xác nhận cọc).</summary>
    public static bool ConGiuPhong(this PhieuDatPhong p) => p.TrangThai is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN;
}
