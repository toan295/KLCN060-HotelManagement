using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Phần điều hành của Quản lý trên màn hình Buồng phòng: bảng tổng quan (phòng nào bẩn / ai đang dọn / ai chưa xong / phòng nào chờ kiểm tra /
/// phòng nào sắp có khách), giao việc cho nhân viên, và nhật ký hoạt động (ai → làm gì → phòng nào → lúc nào → trước → sau).
/// </summary>
public partial class HousekeepingService
{
    /// <summary>Phòng đang dọn quá số phút này mà chưa xong thì bị đếm là "quá giờ" trên bảng nhân viên.</summary>
    private const int PhutDonQuaGio = 45;

    private sealed record PhongRow(string MaPhong, string TenPhong, int Tang, string TenLoai, TinhTrangPhong TinhTrang, DateTime? ThoiGian, string? LyDo, string? MaTaiKhoan);

    private static string GiaiDoanCua(TinhTrangPhong tt, PhieuDonPhong? phieu) => tt switch
    {
        TinhTrangPhong.VD => phieu is null ? "BAN" : phieu.ThoiGianBatDau is null ? "DA_GIAO" : "DANG_DON",
        TinhTrangPhong.OD => phieu is null ? "BAN_CO_KHACH" : phieu.ThoiGianBatDau is null ? "DA_GIAO" : "DANG_DON",
        TinhTrangPhong.VC => "CHO_KIEM_TRA",
        TinhTrangPhong.VI => "SAN_SANG",
        TinhTrangPhong.OC => "CO_KHACH",
        _ => "BAO_TRI"
    };

    public async Task<HousekeepingOverviewDto> GetOverviewAsync(string tenDN)
    {
        var bayGio = DateTime.Now;
        var homNay = DateOnly.FromDateTime(bayGio);
        var ngayMai = homNay.AddDays(1);
        var dauNgay = bayGio.Date;

        var phongRaw = await _context.Phongs.AsNoTracking()
            .Select(x => new
            {
                x.MaPhong, x.TenPhong, x.Tang, TenLoai = x.LoaiPhong.TenLoai, x.TinhTrang,
                Cuoi = _context.LichSuTinhTrangPhongs.Where(h => h.MaPhong == x.MaPhong && h.DenTinhTrang == x.TinhTrang)
                    .OrderByDescending(h => h.ThoiGian).ThenByDescending(h => h.MaLichSu)
                    .Select(h => new { h.ThoiGian, h.LyDo, h.MaTaiKhoan }).FirstOrDefault()
            }).ToListAsync();
        var phongs = phongRaw.Select(x => new PhongRow(x.MaPhong, x.TenPhong, x.Tang, x.TenLoai, x.TinhTrang, x.Cuoi?.ThoiGian, x.Cuoi?.LyDo, x.Cuoi?.MaTaiKhoan)).ToList();

        var phieuMo = await _context.PhieuDonPhongs.AsNoTracking().Where(x => x.TrangThai == TrangThaiPhieuDon.DANG_DON).ToListAsync();
        // Phiếu dọn đã hoàn thành gần nhất của từng phòng: để phòng "đã dọn - chờ kiểm tra" vẫn cho biết ai dọn và xong lúc nào.
        var phieuXongGanNhat = (await _context.PhieuDonPhongs.AsNoTracking()
                .Where(x => x.TrangThai == TrangThaiPhieuDon.DA_XONG && x.ThoiGianXong != null)
                .Select(x => new { x.MaPhong, x.NguoiDon, x.ThoiGianXong }).ToListAsync())
            .GroupBy(x => x.MaPhong).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ThoiGianXong).First());
        var suCoMo = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => x.TrangThai != TrangThaiSuCo.DA_XU_LY && x.TrangThai != TrangThaiSuCo.DA_HUY)
            .Select(x => new { x.MaPhong, x.MucDo }).ToListAsync();

        // Khách sắp đến (đến hôm nay, quá ngày đón mà chưa nhận, hoặc ngày mai): phòng đã gán trong phiếu đặt.
        var sapDen = await _context.ChiTietPhieuDats.AsNoTracking()
            .Where(ct => (ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || ct.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN)
                         && ct.PhieuDatPhong.NgayDonDuKien <= ngayMai && ct.PhieuDatPhong.NgayTraDuKien > homNay)
            .Select(ct => new { ct.MaPhong, ct.MaPhieuDat, ct.PhieuDatPhong.NgayDonDuKien, HoTen = ct.PhieuDatPhong.Khach.HoTen }).ToListAsync();

        var tenDNs = phieuMo.SelectMany(x => new[] { x.NguoiDon, x.NguoiPhanCong }).Concat(phongs.Select(p => p.MaTaiKhoan))
            .Concat(phieuXongGanNhat.Values.Select(x => x.NguoiDon)).ToList();
        var ten = await TenNhanVien.TraCuuAsync(_context, tenDNs);

        HousekeepingRoomDto ToRoom(PhongRow p)
        {
            var tt = p.TinhTrang;
            var maPhong = p.MaPhong;
            var phieu = phieuMo.FirstOrDefault(x => x.MaPhong == maPhong);
            var den = sapDen.Where(x => x.MaPhong == maPhong).OrderBy(x => x.NgayDonDuKien).FirstOrDefault();
            var tuLuc = p.ThoiGian;
            var soGio = tuLuc is null ? (double?)null : Math.Round((bayGio - tuLuc.Value).TotalHours, 1);
            var giaiDoan = GiaiDoanCua(tt, phieu);
            // Nhận phòng được khi phòng đã dọn sạch (VC) hoặc đã kiểm tra (VI): VC chỉ còn thiếu bước kiểm tra chất lượng.
            var khongNhanDuoc = giaiDoan is "BAN" or "DA_GIAO" or "DANG_DON" or "BAO_TRI";
            var choKiem = giaiDoan == "CHO_KIEM_TRA";
            var denHomNay = den is not null && den.NgayDonDuKien <= homNay;
            string uuTien = "BINH_THUONG"; string? lyDo = null;
            if (denHomNay && khongNhanDuoc) { uuTien = "KHAN"; lyDo = giaiDoan == "BAO_TRI" ? "Khách đến hôm nay nhưng phòng đang bảo trì" : "Khách đến hôm nay, phòng chưa dọn xong"; }
            else if (denHomNay && choKiem) { uuTien = "CAO"; lyDo = "Khách đến hôm nay, phòng đã dọn - cần kiểm tra trước khi nhận"; }
            else if (den is not null && khongNhanDuoc && giaiDoan != "BAO_TRI") { uuTien = "CAO"; lyDo = "Khách đến ngày mai"; }
            else if (tt == TinhTrangPhong.VD && soGio >= _alert.DirtyRoomHours) { uuTien = "CAO"; lyDo = $"Chờ dọn quá {_alert.DirtyRoomHours} giờ"; }
            else if (tt == TinhTrangPhong.VC && soGio >= _alert.DirtyRoomHours) { uuTien = "CAO"; lyDo = $"Chờ kiểm tra quá {_alert.DirtyRoomHours} giờ"; }
            string? nguoiDon = phieu?.NguoiDon;
            var xong = giaiDoan is "CHO_KIEM_TRA" or "SAN_SANG" && phieuXongGanNhat.TryGetValue(maPhong, out var px) ? px : null;
            if (giaiDoan == "CHO_KIEM_TRA" && uuTien == "BINH_THUONG") lyDo = "Đã dọn xong, chờ kiểm tra chất lượng";
            return new HousekeepingRoomDto
            {
                MaPhong = maPhong, TenPhong = p.TenPhong, Tang = p.Tang, TenLoaiPhong = p.TenLoai, TinhTrang = tt.ToString(), GiaiDoan = giaiDoan,
                TuLuc = tuLuc, SoGioCho = soGio, MucUuTien = uuTien, LyDoUuTien = lyDo,
                NguoiDon = nguoiDon, HoTenNguoiDon = nguoiDon is null ? null : ten.GetValueOrDefault(nguoiDon),
                BatDauDonLuc = phieu?.ThoiGianBatDau, NguoiPhanCong = phieu?.NguoiPhanCong, GiaoLuc = phieu?.ThoiGianPhanCong,
                NguoiDonXong = xong?.NguoiDon, HoTenNguoiDonXong = xong is null ? null : ten.GetValueOrDefault(xong.NguoiDon), DonXongLuc = xong?.ThoiGianXong,
                KhachSapDen = den?.HoTen, MaPhieuDatSapDen = den?.MaPhieuDat, NgayDonSapDen = den?.NgayDonDuKien,
                SuCoDangMo = suCoMo.Count(x => x.MaPhong == maPhong),
                LyDoKhoa = tt == TinhTrangPhong.OOO ? p.LyDo : null,
                NguoiKhoa = tt != TinhTrangPhong.OOO ? null : p.MaTaiKhoan is { } tk ? (ten.GetValueOrDefault(tk) ?? tk) : "Không rõ (dữ liệu cũ)"
            };
        }

        var rooms = phongs.Select(p => ToRoom(p)).ToList();
        static int Rank(HousekeepingRoomDto r) => r.MucUuTien == "KHAN" ? 0 : r.MucUuTien == "CAO" ? 1 : 2;

        var canXuLy = rooms.Where(r => r.GiaiDoan is "BAN" or "BAN_CO_KHACH" or "DA_GIAO" or "DANG_DON" or "CHO_KIEM_TRA")
            .OrderBy(Rank).ThenByDescending(r => r.SoGioCho ?? 0).ThenBy(r => r.MaPhong, StringComparer.Ordinal).ToList();
        var sapCheckIn = rooms.Where(r => r.MaPhieuDatSapDen is not null).OrderBy(r => r.NgayDonSapDen).ThenBy(Rank).ThenBy(r => r.MaPhong, StringComparer.Ordinal).ToList();
        var phongKhoa = rooms.Where(r => r.GiaiDoan == "BAO_TRI").OrderByDescending(r => r.SoGioCho ?? 0).ToList();

        // Nhân viên có thể được giao dọn: có quyền xác nhận dọn phòng nhưng KHÔNG phải quản lý (quản lý giao việc và kiểm tra, không tự dọn).
        var nhanVienRaw = await _context.TaiKhoans.AsNoTracking()
            .Where(t => t.LoaiTaiKhoan == LoaiTaiKhoan.NHAN_VIEN && t.TrangThai != TrangThaiTaiKhoan.DA_XOA && t.NhanVien != null
                        && t.NhanVien.VaiTro.VaiTroQuyens.Any(vq => vq.Quyen.TenQuyen == Domain.Permissions.XacNhanDonPhong)
                        && !t.NhanVien.VaiTro.VaiTroQuyens.Any(vq => vq.Quyen.TenQuyen == Domain.Permissions.DuyetNghiepVu))
            .Select(t => new { t.TenDN, t.NhanVien!.HoTen, VaiTro = t.NhanVien.VaiTro.TenVaiTro, t.TrangThai }).ToListAsync();
        var xongHomNay = await _context.PhieuDonPhongs.AsNoTracking()
            .Where(x => x.TrangThai == TrangThaiPhieuDon.DA_XONG && x.ThoiGianXong >= dauNgay)
            .Select(x => new { x.NguoiDon, x.ThoiGianBatDau, x.ThoiGianXong }).ToListAsync();

        var nhanVien = nhanVienRaw.Select(n =>
        {
            var cua = phieuMo.Where(x => x.NguoiDon == n.TenDN).ToList();
            var xong = xongHomNay.Where(x => x.NguoiDon == n.TenDN).ToList();
            var phut = xong.Where(x => x.ThoiGianBatDau != null).Select(x => (x.ThoiGianXong!.Value - x.ThoiGianBatDau!.Value).TotalMinutes).ToList();
            return new HousekeepingStaffRowDto
            {
                TenDN = n.TenDN, HoTen = n.HoTen, VaiTro = n.VaiTro, DangHoatDong = n.TrangThai == TrangThaiTaiKhoan.HOAT_DONG,
                DangDon = cua.Where(x => x.ThoiGianBatDau != null).Select(x => x.MaPhong).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                DaGiaoChuaBatDau = cua.Where(x => x.ThoiGianBatDau == null).Select(x => x.MaPhong).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                QuaGio = cua.Count(x => x.ThoiGianBatDau != null && (bayGio - x.ThoiGianBatDau.Value).TotalMinutes > PhutDonQuaGio),
                DaXongHomNay = xong.Count,
                ThoiGianTrungBinhPhut = phut.Count == 0 ? null : Math.Round(phut.Average(), 0)
            };
        }).OrderByDescending(x => x.DangDon.Count + x.DaGiaoChuaBatDau.Count).ThenBy(x => x.HoTen, StringComparer.CurrentCulture).ToList();

        var daKiemTraHomNay = await _context.LichSuTinhTrangPhongs.AsNoTracking()
            .CountAsync(x => x.DenTinhTrang == TinhTrangPhong.VI && x.ThoiGian >= dauNgay);
        var minibar = await _context.ChiTietKiemPhongs.AsNoTracking().CountAsync(x => x.TrangThaiTinhPhi == TrangThaiTinhPhi.CHO_TINH_PHI);
        var doBoQuen = await _context.DoBoQuens.AsNoTracking()
            .CountAsync(x => x.TrangThai == TrangThaiDoBoQuen.DA_TIM_THAY || x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU);

        return new HousekeepingOverviewDto
        {
            CapNhatLuc = bayGio,
            TongPhong = rooms.Count,
            VC = rooms.Count(r => r.TinhTrang == "VC"), VD = rooms.Count(r => r.TinhTrang == "VD"), VI = rooms.Count(r => r.TinhTrang == "VI"),
            OC = rooms.Count(r => r.TinhTrang == "OC"), OD = rooms.Count(r => r.TinhTrang == "OD"), OOO = rooms.Count(r => r.TinhTrang == "OOO"),
            ChuaAiNhan = rooms.Count(r => r.GiaiDoan is "BAN" or "BAN_CO_KHACH"),
            DaGiaoChuaBatDau = rooms.Count(r => r.GiaiDoan == "DA_GIAO"),
            DangDon = rooms.Count(r => r.GiaiDoan == "DANG_DON"),
            ChoKiemTra = rooms.Count(r => r.GiaiDoan == "CHO_KIEM_TRA"),
            SanSang = rooms.Count(r => r.GiaiDoan == "SAN_SANG"),
            PhongKhan = rooms.Count(r => r.MucUuTien == "KHAN"),
            DaDonHomNay = xongHomNay.Count,
            DaKiemTraHomNay = daKiemTraHomNay,
            SuCoDangMo = suCoMo.Count,
            SuCoCaoKhanCap = suCoMo.Count(x => x.MucDo is MucDoSuCo.CAO or MucDoSuCo.KHAN_CAP),
            MinibarChoTinhPhi = minibar,
            DoBoQuenDangLuu = doBoQuen,
            CanXuLy = canXuLy, SapCheckIn = sapCheckIn, PhongKhoa = phongKhoa, NhanVien = nhanVien
        };
    }

    /// <summary>Quản lý giao việc dọn một phòng bẩn cho một nhân viên (hoặc giao lại nếu người đó chưa bắt đầu).</summary>
    public async Task<HousekeepingTaskDto> AssignAsync(string maPhong, string nguoiDon, string tenQuanLy)
    {
        var phong = await _context.Phongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        if (phong.TinhTrang is not (TinhTrangPhong.VD or TinhTrangPhong.OD))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_KHONG_CAN_DON",
                $"Phòng {maPhong} đang ở tình trạng {phong.TinhTrang}, không có việc dọn để giao (chỉ giao được phòng VD hoặc OD).");

        var nguoi = nguoiDon.Trim();
        // Tên đăng nhập không phân biệt hoa thường khi tra cứu, nhưng phiếu dọn phải lưu đúng tên đăng nhập thật (người dọn so sánh bằng tên này khi nhận việc).
        nguoi = await _context.TaiKhoans.AsNoTracking().Where(x => x.TenDN == nguoi).Select(x => x.TenDN).FirstOrDefaultAsync() ?? nguoi;
        if (!await _permissions.CoQuyenAsync(nguoi, Permissions.XacNhanDonPhong))
            throw new ApiException(StatusCodes.Status400BadRequest, "NGUOI_DON_KHONG_HOP_LE", $"\"{nguoi}\" không phải nhân viên buồng phòng đang hoạt động (cần quyền XAC_NHAN_DON_PHONG).", "nguoiDon");

        if (await _permissions.CoQuyenAsync(nguoi, Permissions.DuyetNghiepVu))
            throw new ApiException(StatusCodes.Status400BadRequest, "NGUOI_DON_KHONG_HOP_LE", $"\"{nguoi}\" là quản lý: quản lý giao việc và kiểm tra, không được giao dọn phòng cho quản lý.", "nguoiDon");

        var dangMo = await _context.PhieuDonPhongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiPhieuDon.DANG_DON);
        var bayGio = DateTime.Now;
        if (dangMo is not null)
        {
            if (dangMo.ThoiGianBatDau is not null)
            {
                var ten = (await TenNhanVien.TraCuuAsync(_context, new[] { dangMo.NguoiDon })).GetValueOrDefault(dangMo.NguoiDon) ?? dangMo.NguoiDon;
                throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_DON", $"Phòng {maPhong} đang được {ten} dọn; chờ xong hoặc để họ bỏ nhận trước khi giao lại.");
            }
            var cu = dangMo.NguoiDon;
            dangMo.NguoiDon = nguoi;
            dangMo.NguoiPhanCong = tenQuanLy;
            dangMo.ThoiGianPhanCong = bayGio;
            _audit.Ghi("GIAO_DON_PHONG", maPhong, $"nguoiDon={cu}->{nguoi}; nguoiGiao={tenQuanLy}; tinhTrang={phong.TinhTrang}");
        }
        else
        {
            _context.PhieuDonPhongs.Add(new PhieuDonPhong
            {
                MaPhong = maPhong, NguoiDon = nguoi, TinhTrangBanDau = phong.TinhTrang, TrangThai = TrangThaiPhieuDon.DANG_DON,
                NguoiPhanCong = tenQuanLy, ThoiGianPhanCong = bayGio
            });
            _audit.Ghi("GIAO_DON_PHONG", maPhong, $"nguoiDon={nguoi}; nguoiGiao={tenQuanLy}; tinhTrang={phong.TinhTrang}");
        }
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_DON", $"Phòng {maPhong} vừa được người khác nhận dọn.");
        }
        return await LayViecAsync(maPhong, tenQuanLy);
    }

    /// <summary>
    /// Nhật ký buồng phòng: mọi lần đổi tình trạng phòng (ai, phòng nào, lúc nào, trước → sau, lý do) cùng các thao tác buồng phòng khác trong nhật ký hoạt động
    /// (giao/nhận/bỏ nhận dọn, sự cố, kiểm đồ dùng/minibar, đồ bỏ quên...), mới nhất trước.
    /// </summary>
    public async Task<List<HousekeepingActivityDto>> GetActivityAsync(string? maPhong, DateOnly? tuNgay, DateOnly? denNgay, int toiDa)
    {
        toiDa = Math.Clamp(toiDa, 1, 300);
        var den = (denNgay ?? DateOnly.FromDateTime(DateTime.Now)).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var tu = (tuNgay ?? DateOnly.FromDateTime(DateTime.Now).AddDays(-6)).ToDateTime(TimeOnly.MinValue);
        if (tu >= den)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải trước hoặc bằng đến ngày.", "tuNgay");
        var ma = string.IsNullOrWhiteSpace(maPhong) ? null : maPhong.Trim();

        var trangThai = await _context.LichSuTinhTrangPhongs.AsNoTracking()
            .Where(x => x.ThoiGian >= tu && x.ThoiGian < den && (ma == null || x.MaPhong == ma))
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLichSu).Take(toiDa).ToListAsync();

        // Viết tường minh từng điều kiện: dùng mảng + Any(Contains) thì EF dịch thành OPENJSON tương quan từng dòng, rất chậm (25 giây với ~840 dòng nhật ký).
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.ThoiGian >= tu && x.ThoiGian < den
                        && (x.HanhDong.Contains("SU_CO") || x.HanhDong.Contains("KIEM_PHONG") || x.HanhDong.Contains("PHIEU_KIEM") || x.HanhDong.Contains("MINIBAR")
                            || x.HanhDong.Contains("DO_BO_QUEN") || x.HanhDong.Contains("DON_PHONG") || x.HanhDong.Contains("VAT_DUNG"))
                        && (ma == null || x.DoiTuongTacDong == ma || (x.ChiTiet != null && x.ChiTiet.Contains(ma))))
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(toiDa).ToListAsync();

        var ten = await TenNhanVien.TraCuuAsync(_context, trangThai.Select(x => x.MaTaiKhoan).Concat(nhatKy.Select(x => x.MaTaiKhoan)));
        string? HoTen(string? tk) => tk is null ? null : ten.GetValueOrDefault(tk);

        // "DON_PHONG" cũng trùng với DOI_TINH_TRANG... không: DOI_TINH_TRANG_PHONG đã có ở bảng lịch sử tình trạng nên bỏ khỏi nhật ký để khỏi lặp.
        var dong = trangThai.Select(x => new HousekeepingActivityDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan ?? "Hệ thống", HoTenNguoiThucHien = HoTen(x.MaTaiKhoan),
            HanhDong = "Đổi tình trạng phòng", MaPhong = x.MaPhong, Truoc = x.TuTinhTrang.ToString(), Sau = x.DenTinhTrang.ToString(), LyDo = x.LyDo
        }).Concat(nhatKy.Where(x => !x.HanhDong.StartsWith("DOI_TINH_TRANG_PHONG", StringComparison.Ordinal)).Select(x => new HousekeepingActivityDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, HoTenNguoiThucHien = HoTen(x.MaTaiKhoan),
            HanhDong = MoTaHanhDong(x.HanhDong), MaPhong = x.DoiTuongTacDong, ChiTiet = x.ChiTiet,
            ThayDoiChu = DienGiai(x.HanhDong, x.ChiTiet).ThayDoi, ChiTietDeDoc = DienGiai(x.HanhDong, x.ChiTiet).ChiTiet,
            LyDo = DienGiai(x.HanhDong, x.ChiTiet).LyDo
        }));

        return dong.OrderByDescending(x => x.ThoiGian).Take(toiDa).ToList();
    }

    // Mã trạng thái / loại trong nhật ký → chữ tiếng Việt.
    private static readonly Dictionary<string, string> MaChu = new()
    {
        ["MOI"] = "Mới", ["DA_TIEP_NHAN"] = "Đã tiếp nhận", ["DANG_XU_LY"] = "Đang xử lý", ["DA_XU_LY"] = "Đã xử lý", ["DA_HUY"] = "Đã hủy",
        ["THAP"] = "Thấp", ["TRUNG_BINH"] = "Trung bình", ["CAO"] = "Cao", ["KHAN_CAP"] = "Khẩn cấp",
        ["DIEU_HOA"] = "Điều hòa", ["DIEN_CHIEU_SANG"] = "Điện / chiếu sáng", ["NUOC"] = "Nước", ["TV_THIET_BI_DIEN_TU"] = "TV / thiết bị điện tử", ["NOI_THAT"] = "Nội thất",
        ["THIEU_TRANG_THIET_BI"] = "Thiếu trang thiết bị", ["VE_SINH"] = "Vệ sinh", ["GIAY_TO"] = "Giấy tờ", ["DIEN_TU"] = "Điện tử", ["VI_TIEN"] = "Ví tiền",
        ["TRANG_SUC"] = "Trang sức", ["QUAN_AO"] = "Quần áo", ["Y_TE"] = "Y tế", ["KHAC"] = "Khác",
        ["VC"] = "Trống, đã dọn", ["VD"] = "Trống, chưa dọn", ["VI"] = "Trống, đã kiểm tra", ["OC"] = "Đang có khách", ["OD"] = "Có khách, cần dọn", ["OOO"] = "Ngừng phục vụ / bảo trì"
    };

    /// <summary>Chuỗi "khóa=giá trị; ..." của nhật ký → thay đổi trước → sau, thông tin đi kèm và lý do bằng tiếng Việt dễ đọc.</summary>
    private static (string? ThayDoi, string? ChiTiet, string? LyDo) DienGiai(string hanhDong, string? chiTiet)
    {
        var nhanMacDinh = hanhDong == "DOI_TRANG_THAI_SU_CO" ? "Trạng thái sự cố" : hanhDong == "GIAO_DON_PHONG" ? "Người dọn" : "Giá trị";
        var (thayDoi, thongTin, lyDo) = AuditLogService.PhanTichChiTiet(chiTiet, nhanMacDinh);
        // Cùng khóa "soDong" nhưng nghĩa theo từng thao tác; "Người thực hiện" bỏ vì cột "Ai" đã có.
        string Nhan(string truong) => hanhDong == "KIEM_PHONG" && truong == "Số dòng" ? "Số loại đồ dùng đã kiểm" : truong;
        string Tri(string? v) => string.IsNullOrEmpty(v) ? "(trống)" : (MaChu.TryGetValue(v, out var chu) ? chu : v);
        var td = string.Join("\n", thayDoi.Select(t => $"{Nhan(t.Truong)}: {Tri(t.Truoc)} → {Tri(t.Sau)}"));
        var ct = string.Join("\n", thongTin.Where(t => t.Truong != "Người thực hiện").Select(t => $"{Nhan(t.Truong)}: {Tri(t.GiaTri)}"));
        return (td.Length == 0 ? null : td, ct.Length == 0 ? null : ct, string.IsNullOrWhiteSpace(lyDo) ? null : lyDo);
    }

    private static string MoTaHanhDong(string hanhDong) => hanhDong switch
    {
        "GIAO_DON_PHONG" => "Giao việc dọn phòng",
        "NHAN_DON_PHONG" => "Nhận dọn phòng",
        "BO_NHAN_DON_PHONG" => "Bỏ nhận dọn phòng",
        "BAO_SU_CO" => "Báo sự cố trong phòng",
        "DOI_TRANG_THAI_SU_CO" => "Cập nhật sự cố",
        "KIEM_PHONG" => "Kiểm đồ dùng / minibar",
        "HUY_PHIEU_KIEM" => "Hủy phiếu kiểm phòng",
        "TINH_PHI_MINIBAR" => "Tính phí minibar",
        "BO_QUA_MINIBAR" => "Bỏ qua khoản minibar",
        "GHI_NHAN_DO_BO_QUEN" => "Ghi nhận đồ bỏ quên",
        "LUU_KHO_DO_BO_QUEN" => "Lưu kho đồ bỏ quên",
        "TRA_DO_BO_QUEN" => "Trả đồ bỏ quên cho khách",
        "THANH_LY_DO_BO_QUEN" => "Thanh lý đồ bỏ quên",
        "THEM_VAT_DUNG" or "SUA_VAT_DUNG" or "XOA_VAT_DUNG" => "Sửa danh mục đồ dùng",
        _ => hanhDong
    };
}
