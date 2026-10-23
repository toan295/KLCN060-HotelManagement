using KLCN060.Api.DTOs.Invoices;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Danh sách / thống kê / tổng quan hóa đơn cho Quản lý và Kế toán: mọi con số (tổng tiền, đã thu, còn nợ, công nợ, quá hạn) được tính một nơi duy nhất
/// ở đây để danh sách, thẻ thống kê và panel chi tiết luôn khớp nhau.
/// </summary>
public partial class InvoiceService
{

    /// <summary>Một hóa đơn đã quy về các con số cần lọc / tổng hợp.</summary>
    private sealed class HoaDonRow
    {
        public string MaHD { get; set; } = null!;
        public string MaPhieuNhan { get; set; } = null!;
        public string? MaPhieuDat { get; set; }
        public string? HoTen { get; set; }
        public DateTime NgayLap { get; set; }
        public decimal TongTien { get; set; }
        public decimal DaThu { get; set; }
        public TrangThaiThanhToan TrangThai { get; set; }
        public bool DangO { get; set; }
        public DateOnly? NgayTraCuoi { get; set; }
        public DateTime? LanThuCuoi { get; set; }
    }

    private int NgayQuaHan => Math.Max(_finance.HoaDonQuaHanNgay, 0);

    private IQueryable<HoaDonRow> Hang() => _context.HoaDons.AsNoTracking().Select(x => new HoaDonRow
    {
        MaHD = x.MaHD,
        MaPhieuNhan = x.MaPhieuNhan,
        MaPhieuDat = x.PhieuNhanPhong.MaPhieuDat,
        HoTen = x.PhieuNhanPhong.Khach != null ? x.PhieuNhanPhong.Khach.HoTen
            : x.PhieuNhanPhong.PhieuDatPhong != null ? x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen : null,
        NgayLap = x.NgayLap,
        TongTien = x.TongTien,
        DaThu = x.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0,
        TrangThai = x.TrangThaiThanhToan,
        DangO = x.PhieuNhanPhong.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O),
        NgayTraCuoi = x.PhieuNhanPhong.ChiTietPhieuNhans.Max(c => c.NgayTra),
        LanThuCuoi = x.ChiTietThanhToans.Where(t => t.SoTien > 0).Max(t => (DateTime?)t.ThoiGianThanhToan)
    });

    private const string CollateKhongDau = "Latin1_General_100_CI_AI";

    private IQueryable<HoaDonRow> Loc(InvoiceListQuery query)
    {
        var q = Hang();
        if (!string.IsNullOrWhiteSpace(query.TrangThai))
        {
            if (!EnumParser.TryParse<TrangThaiThanhToan>(query.TrangThai, out var trangThai))
                throw new ApiException(StatusCodes.Status400BadRequest, "TRANG_THAI_KHONG_HOP_LE",
                    "TrangThai không hợp lệ. Chỉ chấp nhận: CHUA_THANH_TOAN, THANH_TOAN_MOT_PHAN, DA_THANH_TOAN.", "trangThai");
            q = q.Where(x => x.TrangThai == trangThai);
        }
        if (query.TuNgay is { } tu && query.DenNgay is { } den && tu > den)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "tuNgay");
        if (query.TuNgay is { } t0) { var d = t0.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.NgayLap >= d); }
        if (query.DenNgay is { } t1) { var d = t1.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.NgayLap < d); }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            // Tên khách tìm không phân biệt hoa/thường và dấu (ô tìm ghi "không cần gõ dấu"); mã phòng tìm theo một phần (gõ "LT811" ra phòng "PLT811").
            var khongDau = VanBanHelper.KhongDau(tuKhoa);
            var ids = _context.HoaDons.Where(x => x.MaHD.Contains(tuKhoa) || x.MaPhieuNhan.Contains(tuKhoa)
                || (x.PhieuNhanPhong.MaPhieuDat != null && x.PhieuNhanPhong.MaPhieuDat.Contains(tuKhoa))
                || (x.PhieuNhanPhong.Khach != null && (EF.Functions.Collate(x.PhieuNhanPhong.Khach.HoTen, CollateKhongDau).Contains(khongDau) || x.PhieuNhanPhong.Khach.SoDT.Contains(tuKhoa)))
                || (x.PhieuNhanPhong.PhieuDatPhong != null && (EF.Functions.Collate(x.PhieuNhanPhong.PhieuDatPhong.Khach.HoTen, CollateKhongDau).Contains(khongDau) || x.PhieuNhanPhong.PhieuDatPhong.Khach.SoDT.Contains(tuKhoa)))
                || x.PhieuNhanPhong.ChiTietPhieuNhans.Any(c => c.MaPhong.Contains(tuKhoa)))
                .Select(x => x.MaHD);
            q = q.Where(x => ids.Contains(x.MaHD));
        }
        if (query.QuaHan is { } qh)
        {
            var han = DateOnly.FromDateTime(DateTime.Now).AddDays(-NgayQuaHan);
            if (qh) q = q.Where(x => x.TongTien > x.DaThu && !x.DangO && x.NgayTraCuoi != null && x.NgayTraCuoi <= han);
            else q = q.Where(x => !(x.TongTien > x.DaThu && !x.DangO && x.NgayTraCuoi != null && x.NgayTraCuoi <= han));
        }
        return q;
    }

    private (bool QuaHan, int SoNgay) TinhQuaHan(HoaDonRow x, DateOnly homNay)
    {
        if (x.TongTien <= x.DaThu || x.DangO || x.NgayTraCuoi is not { } tra) return (false, 0);
        var soNgay = homNay.DayNumber - tra.DayNumber;
        return soNgay >= NgayQuaHan ? (true, soNgay) : (false, 0);
    }

    public async Task<(List<InvoiceSummaryDto> Items, int TotalItems)> SearchAsync(InvoiceListQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = Loc(query);
        var total = await q.CountAsync();

        q = (query.SapXep ?? "").ToLowerInvariant() switch
        {
            "con_no" => q.OrderByDescending(x => x.TongTien - x.DaThu).ThenByDescending(x => x.NgayLap),
            "tong_tien" => q.OrderByDescending(x => x.TongTien).ThenByDescending(x => x.NgayLap),
            "qua_han" => q.OrderBy(x => x.NgayTraCuoi == null).ThenBy(x => x.NgayTraCuoi).ThenByDescending(x => x.TongTien - x.DaThu),
            _ => q.OrderByDescending(x => x.NgayLap).ThenByDescending(x => x.MaHD)
        };
        var dong = await q.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync();

        var maPN = dong.Select(x => x.MaPhieuNhan).Distinct().ToList();
        var phong = (await _context.ChiTietPhieuNhans.AsNoTracking().Where(c => maPN.Contains(c.MaPhieuNhan))
                .Select(c => new { c.MaPhieuNhan, c.MaPhong, c.NgayNhan }).ToListAsync())
            .GroupBy(c => c.MaPhieuNhan).ToDictionary(g => g.Key, g => string.Join(", ", g.OrderBy(c => c.NgayNhan).Select(c => c.MaPhong).Distinct()));
        var maHDs = dong.Select(x => x.MaHD).ToList();
        var hinhThuc = (await _context.ChiTietThanhToans.AsNoTracking().Where(t => maHDs.Contains(t.MaHD) && t.SoTien > 0)
                .Select(t => new { t.MaHD, t.HinhThucThanhToan, t.ThoiGianThanhToan }).ToListAsync())
            .GroupBy(t => t.MaHD).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.ThoiGianThanhToan).First().HinhThucThanhToan.ToString());

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var items = dong.Select(x =>
        {
            var (quaHan, soNgay) = TinhQuaHan(x, homNay);
            return new InvoiceSummaryDto
            {
                MaHD = x.MaHD, MaPhieuNhan = x.MaPhieuNhan, MaPhieuDat = x.MaPhieuDat, HoTenKhach = x.HoTen, NgayLap = x.NgayLap,
                TongTien = x.TongTien, DaThanhToan = x.DaThu, ConNo = Math.Max(0, x.TongTien - x.DaThu), TrangThaiThanhToan = x.TrangThai.ToString(),
                DanhSachPhong = phong.GetValueOrDefault(x.MaPhieuNhan), LanThuCuoi = x.LanThuCuoi, HinhThucThuCuoi = hinhThuc.GetValueOrDefault(x.MaHD),
                QuaHan = quaHan, SoNgayQuaHan = soNgay, ThuThua = x.DaThu > x.TongTien
            };
        }).ToList();
        return (items, total);
    }

    public async Task<InvoiceStatsDto> GetStatsAsync(InvoiceListQuery query)
    {
        var dong = await Loc(query).ToListAsync();
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var stats = new InvoiceStatsDto { CapNhatLuc = DateTime.Now, NgayQuaHan = NgayQuaHan, TongHoaDon = dong.Count };
        foreach (var x in dong)
        {
            var conNo = Math.Max(0, x.TongTien - x.DaThu);
            stats.TongTien += x.TongTien;
            stats.DaThu += x.DaThu;
            stats.ConNo += conNo;
            switch (x.TrangThai)
            {
                case TrangThaiThanhToan.CHUA_THANH_TOAN: stats.SoChuaThanhToan++; break;
                case TrangThaiThanhToan.THANH_TOAN_MOT_PHAN: stats.SoThanhToanMotPhan++; break;
                default: stats.SoDaThanhToan++; break;
            }
            if (TinhQuaHan(x, homNay).QuaHan) { stats.SoQuaHan++; stats.ConNoQuaHan += conNo; }
            if (x.DaThu > x.TongTien) stats.SoThuThua++;
        }
        return stats;
    }

    public async Task<InvoiceOverviewDto> GetOverviewAsync(string maHD, CurrentUser user)
    {
        // Quyền thật của người đang xem: để bảng "ai được làm gì" nói đúng cho CHÍNH họ (Quản lý chỉ xem, không thu tiền).
        var vaiTro = await _context.TaiKhoans.AsNoTracking().Where(x => x.TenDN == user.TenDN)
            .Select(x => x.NhanVien != null ? x.NhanVien.VaiTro.TenVaiTro : null).FirstOrDefaultAsync();
        var quyen = vaiTro is null ? new List<string>()
            : await _context.VaiTroQuyens.AsNoTracking().Where(x => x.VaiTro.TenVaiTro == vaiTro).Select(x => x.Quyen.TenQuyen).ToListAsync();
        bool Co(string q) => quyen.Contains(q);
        var duocThanhToan = Co(Permissions.ThanhToan);

        var h = await _context.HoaDons.AsNoTracking().AsSplitQuery()
            .Include(x => x.NhanVien)
            .Include(x => x.ChiTietHoaDons)
            .Include(x => x.ChiTietThanhToans).ThenInclude(t => t.NhanVienThuNgan)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong).ThenInclude(d => d!.Khach)
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.ChiTietPhieuNhans).ThenInclude(c => c.Phong).ThenInclude(ph => ph.LoaiPhong)
            .FirstOrDefaultAsync(x => x.MaHD == maHD)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_HOA_DON", "Không tìm thấy hóa đơn.");

        var pn = h.PhieuNhanPhong;
        var khach = pn.Khach ?? pn.PhieuDatPhong?.Khach;
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var daThu = h.ChiTietThanhToans.Sum(t => t.SoTien);
        var conNo = Math.Max(0, h.TongTien - daThu);
        var dangO = pn.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        var ngayTraCuoi = pn.ChiTietPhieuNhans.Max(c => c.NgayTra);

        var dto = new InvoiceOverviewDto
        {
            MaHD = h.MaHD, NgayLap = h.NgayLap, NguoiLap = $"{h.NhanVien.HoTen} ({h.MaNV})",
            MaKhach = khach?.MaKhach, HoTenKhach = khach?.HoTen, SoDTKhach = khach?.SoDT,
            MaPhieuNhan = h.MaPhieuNhan, MaPhieuDat = pn.MaPhieuDat, NguonDat = pn.PhieuDatPhong?.NguonDat.ToString(),
            TienPhong = h.TienPhong, TienDV = h.TienDV, PhuThu = h.PhuThu, TienDaCoc = h.TienDaCoc, TongTien = h.TongTien,
            DaThu = daThu, ConNo = conNo, TrangThaiThanhToan = h.TrangThaiThanhToan.ToString(), NgayTraCuoi = ngayTraCuoi,
            CacKhoan = h.ChiTietHoaDons.OrderBy(x => x.MaChiTietHoaDon).Select(x => new InvoiceLineDto
            {
                LoaiKhoanMuc = x.LoaiKhoanMuc.ToString(), MoTa = x.MoTa, SoLuong = x.SoLuong, DonGia = x.DonGia, ThanhTien = x.ThanhTien
            }).ToList()
        };
        dto.TongGiamGia = -h.ChiTietHoaDons.Where(x => x.LoaiKhoanMuc == LoaiKhoanMuc.KHUYEN_MAI).Sum(x => x.ThanhTien);

        // Cùng quy tắc tính tiền phòng của folio: ở ít nhất 1 đêm, trừ phòng đã đổi đi (đổi ngay trong ngày thì không tính đêm cho phòng cũ).
        var phongDaDoiDi = (await _context.PhieuDoiPhongs.AsNoTracking().Where(x => x.MaPhieuNhan == h.MaPhieuNhan).Select(x => x.MaPhongCu).ToListAsync()).ToHashSet();
        dto.Phong = pn.ChiTietPhieuNhans.OrderBy(c => c.NgayNhan).ThenBy(c => c.MaPhong, StringComparer.Ordinal).Select(c => new InvoiceRoomOverviewDto
        {
            MaPhong = c.MaPhong, TenPhong = c.Phong.TenPhong, TenLoaiPhong = c.Phong.LoaiPhong.TenLoai, NgayNhan = c.NgayNhan, NgayTra = c.NgayTra,
            SoDem = Math.Max((c.NgayTra ?? c.NgayTraDuKien ?? c.NgayNhan).DayNumber - c.NgayNhan.DayNumber, phongDaDoiDi.Contains(c.MaPhong) ? 0 : 1), DonGia = c.DonGia,
            TrangThai = c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O ? "DANG_O" : "DA_TRA"
        }).ToList();

        // ---- Công thức: Tổng các khoản - Cọc = Tổng phải thu; Tổng phải thu = Đã thu + Còn nợ
        var tongCacKhoan = h.ChiTietHoaDons.Sum(x => x.ThanhTien);
        var chenhCoCau = Math.Round(tongCacKhoan - h.TienDaCoc - h.TongTien, 0);
        var thuThua = daThu > h.TongTien;
        dto.CongThucKhop = chenhCoCau == 0 && !thuThua;
        dto.CongThucGhiChu = thuThua
            ? $"Đã thu {Tien(daThu)} lớn hơn tổng phải thu {Tien(h.TongTien)}: thu thừa {Tien(daThu - h.TongTien)} cần trả lại cho khách (ngoài hệ thống)."
            : chenhCoCau != 0
                ? $"Các khoản {Tien(tongCacKhoan)} - cọc {Tien(h.TienDaCoc)} = {Tien(tongCacKhoan - h.TienDaCoc)} nhưng tổng phải thu là {Tien(h.TongTien)} (lệch {Tien(chenhCoCau)})."
                : $"Các khoản {Tien(tongCacKhoan)} - cọc {Tien(h.TienDaCoc)} = {Tien(h.TongTien)} phải thu = đã thu {Tien(daThu)} + còn nợ {Tien(conNo)}.";

        // ---- Hạn thanh toán
        var hang = new HoaDonRow { TongTien = h.TongTien, DaThu = daThu, DangO = dangO, NgayTraCuoi = ngayTraCuoi };
        var (quaHan, soNgay) = TinhQuaHan(hang, homNay);
        dto.QuaHan = quaHan; dto.SoNgayQuaHan = soNgay;
        dto.HanThanhToan = conNo <= 0 ? "Không còn nợ."
            : dangO ? "Khách còn đang ở: thanh toán khi trả phòng."
            : ngayTraCuoi is null ? "Chưa có ngày trả phòng."
            : quaHan ? $"QUÁ HẠN {soNgay} ngày (khách trả phòng {ngayTraCuoi:dd/MM/yyyy}, hạn thanh toán sau {NgayQuaHan} ngày)."
            : $"Khách trả phòng {ngayTraCuoi:dd/MM/yyyy}; quá hạn nếu chưa thu sau {NgayQuaHan} ngày.";

        // ---- Thanh toán (ai thu, bằng gì, khi nào)
        dto.ThanhToan = h.ChiTietThanhToans.OrderBy(t => t.ThoiGianThanhToan).ThenBy(t => t.MaThanhToan).Select(t => new InvoicePaymentOverviewDto
        {
            MaThanhToan = t.MaThanhToan, ThoiGian = t.ThoiGianThanhToan, HinhThuc = t.HinhThucThanhToan.ToString(), SoTien = t.SoTien,
            NguoiThu = $"{t.NhanVienThuNgan.HoTen} ({t.MaTaiKhoanThuNgan})", MaGiaoDich = t.MaGiaoDich,
            Nguon = t.HinhThucThanhToan == HinhThucThanhToan.CHUYEN_KHOAN ? "CHUYEN_KHOAN_QR" : "TIEN_MAT_QUAY"
        }).ToList();

        // ---- Lịch sử thao tác (nhật ký) trên hóa đơn
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking().Where(x => x.DoiTuongTacDong == maHD)
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(40).ToListAsync();
        var ten = await TenNguoiAsync(nhatKy.Select(x => x.MaTaiKhoan).Distinct().ToList());
        string Ten(string tk) => ten.GetValueOrDefault(tk, tk);

        dto.LichSu = nhatKy.Select(x => new InvoiceHistoryOverviewDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = Ten(x.MaTaiKhoan), HanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();

        // ---- Ai được làm gì, hiện có làm được không
        var heThongKhongSua = h.TrangThaiThanhToan == TrangThaiThanhToan.DA_THANH_TOAN && conNo <= 0;
        dto.ThaoTac =
        [
            new() { Ma = "XEM", Ten = "Xem hóa đơn và công nợ", AiThucHien = "Quản lý, Kế toán (quyền báo cáo); Lễ tân/Kế toán (quyền thanh toán)", ChoPhep = true },
            new() { Ma = "THU_TIEN", Ten = "Thu tiền", AiThucHien = "Kế toán / Lễ tân (quyền thanh toán) - Quản lý chỉ xem", ChoPhep = conNo > 0 && duocThanhToan,
                    GiaiThich = !duocThanhToan ? "Bạn chỉ có quyền xem - việc thu tiền do Kế toán / Lễ tân thực hiện." : conNo > 0 ? $"Còn nợ {Tien(conNo)}; chuyển khoản phải đi qua mã QR để đối soát với ngân hàng." : "Hóa đơn đã thu đủ." },
            new() { Ma = "XUAT_PDF", Ten = "Xuất hóa đơn PDF", AiThucHien = "Kế toán / Lễ tân (quyền thanh toán)", ChoPhep = heThongKhongSua && duocThanhToan,
                    GiaiThich = !duocThanhToan ? "Việc xuất hóa đơn do Kế toán / Lễ tân thực hiện." : heThongKhongSua ? "" : "Chỉ xuất khi đã thanh toán đủ." },
            new() { Ma = "SUA_XOA", Ten = "Sửa tiền / xóa hóa đơn", AiThucHien = "Không ai - hệ thống không có thao tác này", ChoPhep = false,
                    GiaiThich = "Hóa đơn chỉ được lập từ lúc trả phòng và không chỉnh sửa; khoản thu chỉ ghi thêm, không xóa." },
        ];

        // ---- Cảnh báo
        if (quaHan) dto.CanhBao.Add($"Hóa đơn QUÁ HẠN {soNgay} ngày, còn nợ {Tien(conNo)}.");
        if (thuThua) dto.CanhBao.Add($"Thu thừa {Tien(daThu - h.TongTien)}: cần trả lại cho khách (ngoài hệ thống).");
        if (chenhCoCau != 0) dto.CanhBao.Add($"Cơ cấu hóa đơn lệch {Tien(chenhCoCau)} so với tổng phải thu - cần kiểm tra.");
        return dto;
    }

    private async Task<Dictionary<string, string>> TenNguoiAsync(List<string> tenDNs)
    {
        if (tenDNs.Count == 0) return new();
        var ds = await _context.TaiKhoans.AsNoTracking().Where(x => tenDNs.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : x.Khach != null ? x.Khach.HoTen : null }).ToListAsync();
        return ds.ToDictionary(x => x.TenDN, x => string.IsNullOrEmpty(x.HoTen) ? x.TenDN : $"{x.HoTen} ({x.TenDN})");
    }

    private static string Tien(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}đ";
}
