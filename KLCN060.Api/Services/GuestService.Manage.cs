using System.Globalization;
using System.Text;
using KLCN060.Api.DTOs.Guests;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Tìm khách (không phân biệt hoa-thường, có dấu hay không dấu, nhiều từ), tổng hợp lưu trú / công nợ ngay trên danh sách,
/// hồ sơ chi tiết với lịch sử lưu trú đầy đủ, nhận diện khách trùng và lịch sử sửa hồ sơ.
/// Lịch sử luôn lấy theo ĐÚNG mã khách (booking của khách đó, lượt vãng lai của khách đó) - không gộp theo tên hay SĐT.
/// </summary>
public partial class GuestService
{
    private const string CollateKhongDau = "Latin1_General_100_CI_AI";

    private sealed class ThongKeKhach
    {
        public int SoLuot { get; set; }
        public int SoHuy { get; set; }
        public int SoKhongDen { get; set; }
        public DateOnly? LanCuoi { get; set; }
        public bool DangO { get; set; }
        public bool LaDoan { get; set; }
        public decimal TongChiTieu { get; set; }
        public decimal ConNo { get; set; }
    }

    private static readonly HashSet<string> LocHopLe = new() { "tat_ca", "dang_o", "con_no", "doanh_nghiep", "co_tai_khoan" };

    public async Task<(List<GuestDto> Items, int TotalItems)> SearchAsync(string? q, int page, int pageSize, string? loc = null, string? sapXep = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, PageSizeToiDa);
        if (q is not null && q.Trim().Length > 100)
            throw new ApiException(StatusCodes.Status400BadRequest, "TU_KHOA_QUA_DAI", "Từ khóa tìm kiếm tối đa 100 ký tự.", "q");
        if (!string.IsNullOrWhiteSpace(loc) && !LocHopLe.Contains(loc.Trim().ToLowerInvariant()))
            throw new ApiException(StatusCodes.Status400BadRequest, "BO_LOC_KHONG_HOP_LE", "loc chỉ nhận: dang_o, con_no, doanh_nghiep, co_tai_khoan.", "loc");

        var query = _context.Khachs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            // Mỗi từ phải khớp ít nhất một trường (mã, họ tên, SĐT, CCCD, email); so khớp không dấu: "dang van" tìm được "Đặng Văn".
            foreach (var tu in q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
            {
                var t = tu;
                query = query.Where(x => EF.Functions.Collate(x.HoTen, CollateKhongDau).Contains(t)
                    || x.MaKhach.Contains(t) || x.SoDT.Contains(t)
                    || (x.CCCD != null && x.CCCD.Contains(t))
                    || (x.Email != null && EF.Functions.Collate(x.Email, CollateKhongDau).Contains(t)));
            }
        }

        switch ((loc ?? "").ToLowerInvariant())
        {
            case "dang_o":
                query = query.Where(x => _context.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O
                    && (c.PhieuNhanPhong.MaKhach == x.MaKhach || (c.PhieuNhanPhong.MaKhach == null && c.PhieuNhanPhong.PhieuDatPhong!.MaKhach == x.MaKhach))));
                break;
            case "con_no":
                query = query.Where(x => _context.HoaDons.Any(h => (h.PhieuNhanPhong.MaKhach == x.MaKhach || (h.PhieuNhanPhong.MaKhach == null && h.PhieuNhanPhong.PhieuDatPhong!.MaKhach == x.MaKhach))
                    && h.TongTien > (h.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0)));
                break;
            case "doanh_nghiep":
                query = query.Where(x => x.PhieuDatPhongs.Any(p => p.LoaiDatPhong == LoaiDatPhong.DOAN)
                    || EF.Functions.Collate(x.HoTen, CollateKhongDau).Contains("cong ty"));
                break;
            case "co_tai_khoan":
                query = query.Where(x => x.TaiKhoan != null);
                break;
        }

        var total = await query.CountAsync();
        query = (sapXep ?? "").ToLowerInvariant() switch
        {
            "ten" => query.OrderBy(x => EF.Functions.Collate(x.HoTen, CollateKhongDau)).ThenBy(x => x.MaKhach),
            "moi" => query.OrderByDescending(x => x.MaKhach),
            _ => query.OrderBy(x => x.MaKhach)
        };
        var khachs = await query.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToListAsync();

        var thongKe = await LayThongKeAsync(khachs.Select(x => x.MaKhach).ToList());
        var items = khachs.Select(k =>
        {
            var dto = ToDto(k);
            // Danh sách tìm kiếm chỉ hiện đuôi giấy tờ tùy thân; số đầy đủ chỉ có ở hồ sơ chi tiết (GET /guests/{ma}) khi cần làm thủ tục.
            dto.CCCD = CheCccd(dto.CCCD);
            GanThongKe(dto, thongKe.GetValueOrDefault(k.MaKhach));
            return dto;
        }).ToList();
        return (items, total);
    }


    private static void GanThongKe(GuestDto dto, ThongKeKhach? tk)
    {
        var laDN = dto.HoTen.Contains("công ty", StringComparison.OrdinalIgnoreCase) || dto.HoTen.Contains("cty", StringComparison.OrdinalIgnoreCase);
        dto.LoaiKhach = laDN || tk?.LaDoan == true ? "DOANH_NGHIEP" : "CA_NHAN";
        if (tk is null) return;
        dto.SoLuotO = tk.SoLuot;
        dto.LanOCuoi = tk.LanCuoi;
        dto.DangO = tk.DangO;
        dto.TongChiTieu = tk.TongChiTieu;
        dto.ConNo = tk.ConNo;
    }

    /// <summary>Tổng hợp lưu trú / công nợ cho một nhóm khách bằng vài truy vấn gộp (không truy vấn từng khách).</summary>
    private async Task<Dictionary<string, ThongKeKhach>> LayThongKeAsync(List<string> ids)
    {
        var kq = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(x => x, _ => new ThongKeKhach(), StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0) return kq;

        var dat = await _context.PhieuDatPhongs.AsNoTracking().Where(x => ids.Contains(x.MaKhach))
            .Select(x => new { x.MaKhach, x.TrangThai, x.NgayDonDuKien, x.LoaiDatPhong }).ToListAsync();
        foreach (var d in dat)
        {
            var tk = kq[d.MaKhach];
            if (d.TrangThai == TrangThaiPhieuDat.DA_HUY) { tk.SoHuy++; continue; }
            if (d.TrangThai == TrangThaiPhieuDat.KHONG_DEN) { tk.SoKhongDen++; continue; }
            tk.SoLuot++;
            if (d.LoaiDatPhong == LoaiDatPhong.DOAN) tk.LaDoan = true;
            if (tk.LanCuoi is null || d.NgayDonDuKien > tk.LanCuoi) tk.LanCuoi = d.NgayDonDuKien;
        }

        var vangLai = await _context.PhieuNhanPhongs.AsNoTracking().Where(x => x.MaPhieuDat == null && x.MaKhach != null && ids.Contains(x.MaKhach))
            .Select(x => new { x.MaKhach, NgayNhan = x.ChiTietPhieuNhans.Min(c => (DateOnly?)c.NgayNhan) }).ToListAsync();
        foreach (var v in vangLai)
        {
            var tk = kq[v.MaKhach!];
            tk.SoLuot++;
            if (v.NgayNhan is { } n && (tk.LanCuoi is null || n > tk.LanCuoi)) tk.LanCuoi = n;
        }

        var dangO = await _context.ChiTietPhieuNhans.AsNoTracking().Where(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(c => c.PhieuNhanPhong.MaKhach ?? c.PhieuNhanPhong.PhieuDatPhong!.MaKhach).Distinct().ToListAsync();
        foreach (var m in dangO) if (m is not null && kq.TryGetValue(m, out var tk)) tk.DangO = true;

        var hd = await _context.HoaDons.AsNoTracking()
            .Select(h => new
            {
                MaKhach = h.PhieuNhanPhong.MaKhach ?? h.PhieuNhanPhong.PhieuDatPhong!.MaKhach,
                h.TongTien, h.TienDaCoc, DaThu = h.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0
            })
            .Where(h => h.MaKhach != null && ids.Contains(h.MaKhach)).ToListAsync();
        foreach (var h in hd)
        {
            var tk = kq[h.MaKhach!];
            tk.TongChiTieu += h.TongTien + h.TienDaCoc;
            tk.ConNo += Math.Max(0, h.TongTien - h.DaThu);
        }
        return kq;
    }

    public async Task<GuestDetailDto> GetDetailAsync(string maKhach)
    {
        var khach = await TimKhachAsync(maKhach);
        var thongKe = (await LayThongKeAsync(new List<string> { khach.MaKhach }))[khach.MaKhach];

        // Hóa đơn của khách (theo phiếu nhận) - truy vấn riêng, mỗi hóa đơn một dòng, để không lồng hàm tổng.
        var hoaDons = (await _context.HoaDons.AsNoTracking()
            .Where(h => h.PhieuNhanPhong.MaKhach == maKhach || (h.PhieuNhanPhong.MaKhach == null && h.PhieuNhanPhong.PhieuDatPhong!.MaKhach == maKhach))
            .Select(h => new { h.MaPhieuNhan, h.MaHD, h.TongTien, h.TienDaCoc, DaThu = h.ChiTietThanhToans.Sum(t => (decimal?)t.SoTien) ?? 0 })
            .ToListAsync())
            .GroupBy(h => h.MaPhieuNhan)
            .ToDictionary(g => g.Key, g => new { MaHD = g.OrderBy(h => h.MaHD).First().MaHD, Tong = g.Sum(h => h.TongTien), Coc = g.Sum(h => h.TienDaCoc), DaThu = g.Sum(h => h.DaThu) });

        var dat = await _context.PhieuDatPhongs.AsNoTracking().Where(x => x.MaKhach == maKhach)
            .Select(x => new
            {
                x.MaPhieuDat, x.TrangThai, x.NgayDonDuKien, x.NgayTraDuKien, x.NguonDat, x.TienCoc,
                DaThuCoc = x.NgayNhanCoc != null,
                Tong = x.ChiTietPhieuDats.Sum(ct => (decimal?)ct.ThanhTien) ?? 0,
                Phong = x.ChiTietPhieuDats.Select(ct => ct.MaPhong).ToList(),
                MaPhieuNhan = x.PhieuNhanPhong != null ? x.PhieuNhanPhong.MaPhieuNhan : null,
                NhanThucTe = x.PhieuNhanPhong != null ? (DateTime?)x.PhieuNhanPhong.NgayNhan : null,
                TraThucTe = x.PhieuNhanPhong != null ? x.PhieuNhanPhong.ChiTietPhieuNhans.Max(c => c.NgayTra) : null,
                ConO = x.PhieuNhanPhong != null && x.PhieuNhanPhong.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O),
                PhongThucTe = x.PhieuNhanPhong != null ? x.PhieuNhanPhong.ChiTietPhieuNhans.Select(c => c.MaPhong).ToList() : null
            }).ToListAsync();

        var lichSu = dat.Select(x =>
        {
            var phong = x.PhongThucTe is { Count: > 0 } ? x.PhongThucTe : x.Phong;
            var hd = x.MaPhieuNhan is not null ? hoaDons.GetValueOrDefault(x.MaPhieuNhan) : null;
            return new GuestStayHistoryDto
            {
                MaPhieuDat = x.MaPhieuDat, MaPhieuNhan = x.MaPhieuNhan, TrangThai = x.TrangThai.ToString(),
                NgayDonDuKien = x.NgayDonDuKien, NgayTraDuKien = x.NgayTraDuKien, TongTien = x.Tong,
                DanhSachPhong = string.Join(", ", phong.Distinct().OrderBy(p => p, StringComparer.Ordinal)),
                NgayNhanThucTe = x.NhanThucTe, NgayTraThucTe = x.TraThucTe, NguonDat = x.NguonDat.ToString(),
                TienCoc = x.TienCoc, DaThuCoc = x.DaThuCoc, MaHoaDon = hd?.MaHD, TongHoaDon = hd?.Tong ?? 0, DaThanhToan = hd?.DaThu ?? 0,
                ConNo = Math.Max(0, (hd?.Tong ?? 0) - (hd?.DaThu ?? 0))
            };
        }).ToList();

        // Lượt ở vãng lai (nhận phòng trực tiếp, không có phiếu đặt) cũng là lịch sử lưu trú của khách.
        var vangLai = await _context.PhieuNhanPhongs.AsNoTracking()
            .Where(x => x.MaKhach == maKhach && x.MaPhieuDat == null)
            .Select(x => new
            {
                x.MaPhieuNhan, x.NgayNhan,
                Phongs = x.ChiTietPhieuNhans.Select(c => c.MaPhong).ToList(),
                NgayNhanPhong = x.ChiTietPhieuNhans.Min(c => c.NgayNhan),
                NgayTra = x.ChiTietPhieuNhans.Max(c => c.NgayTra ?? c.NgayTraDuKien ?? c.NgayNhan),
                TraThucTe = x.ChiTietPhieuNhans.Max(c => c.NgayTra),
                ConO = x.ChiTietPhieuNhans.Any(c => c.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            }).ToListAsync();
        lichSu.AddRange(vangLai.Select(x =>
        {
            var hd = hoaDons.GetValueOrDefault(x.MaPhieuNhan);
            return new GuestStayHistoryDto
        {
            MaPhieuDat = null, MaPhieuNhan = x.MaPhieuNhan, LoaiLuuTru = "VANG_LAI",
            Phongs = string.Join(", ", x.Phongs.OrderBy(p => p, StringComparer.Ordinal)),
            DanhSachPhong = string.Join(", ", x.Phongs.OrderBy(p => p, StringComparer.Ordinal)),
            TrangThai = x.ConO ? "DANG_O" : "DA_TRA_PHONG",
            NgayDonDuKien = x.NgayNhanPhong, NgayTraDuKien = x.NgayTra, TongTien = (hd?.Tong ?? 0) + (hd?.Coc ?? 0),
            NgayNhanThucTe = x.NgayNhan, NgayTraThucTe = x.TraThucTe, NguonDat = "QUAY_LE_TAN",
            TienCoc = hd?.Coc ?? 0, DaThuCoc = (hd?.Coc ?? 0) > 0, MaHoaDon = hd?.MaHD, TongHoaDon = hd?.Tong ?? 0, DaThanhToan = hd?.DaThu ?? 0,
            ConNo = Math.Max(0, (hd?.Tong ?? 0) - (hd?.DaThu ?? 0))
        };
        }));
        lichSu = lichSu.OrderByDescending(x => x.NgayDonDuKien).ThenByDescending(x => x.MaPhieuDat).ToList();

        var dto = new GuestDetailDto
        {
            MaKhach = khach.MaKhach, HoTen = khach.HoTen, SoDT = khach.SoDT, CCCD = khach.CCCD, Email = khach.Email,
            DiaChi = khach.DiaChi, LichSuLuuTru = lichSu,
            SoLuotHuy = thongKe.SoHuy, SoLuotKhongDen = thongKe.SoKhongDen
        };
        GanThongKe(dto, thongKe);

        dto.NghiTrung = await TimNghiTrungAsync(new GuestRequest { HoTen = khach.HoTen, SoDT = khach.SoDT, CCCD = khach.CCCD, Email = khach.Email }, maKhach);

        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong == maKhach && (x.HanhDong == "TAO_KHACH" || x.HanhDong == "CAP_NHAT_KHACH"))
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(15).ToListAsync();
        var tenDN = nhatKy.Select(x => x.MaTaiKhoan).Distinct().ToList();
        var ten = await _context.TaiKhoans.AsNoTracking().Where(x => tenDN.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : null }).ToDictionaryAsync(x => x.TenDN);
        dto.LichSuHoSo = nhatKy.Select(x => new GuestProfileChangeDto
        {
            ThoiGian = x.ThoiGian,
            NguoiThucHien = ten.TryGetValue(x.MaTaiKhoan, out var n) && !string.IsNullOrEmpty(n.HoTen) ? $"{n.HoTen} ({x.MaTaiKhoan})" : x.MaTaiKhoan,
            HanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();
        return dto;
    }

    public async Task<List<GuestDuplicateDto>> TimNghiTrungAsync(GuestRequest request, string? loaiTru)
    {
        var hoTen = (request.HoTen ?? "").Trim();
        var soDT = (request.SoDT ?? "").Trim();
        var cccd = string.IsNullOrWhiteSpace(request.CCCD) || request.CCCD.Contains('*') ? null : request.CCCD.Trim();
        var email = ContactValidator.ChuanHoaEmail(request.Email);
        if (hoTen.Length == 0 && soDT.Length == 0 && cccd is null && email is null) return new();

        var ma = loaiTru ?? string.Empty;
        var ungVien = await _context.Khachs.AsNoTracking()
            .Where(x => x.MaKhach != ma
                && ((soDT != "" && x.SoDT == soDT)
                    || (cccd != null && x.CCCD == cccd)
                    || (email != null && x.Email == email)
                    || (hoTen != "" && EF.Functions.Collate(x.HoTen, CollateKhongDau) == hoTen)))
            .OrderBy(x => x.MaKhach).Take(10).ToListAsync();

        return ungVien.Select(x =>
        {
            var lyDo = new List<string>();
            if (soDT != "" && x.SoDT == soDT) lyDo.Add("Trùng số điện thoại");
            if (cccd is not null && x.CCCD == cccd) lyDo.Add("Trùng số CCCD/giấy tờ");
            if (email is not null && string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)) lyDo.Add("Trùng email");
            if (hoTen != "" && string.Equals(KhongDau(x.HoTen), KhongDau(hoTen), StringComparison.OrdinalIgnoreCase)) lyDo.Add("Trùng họ tên");
            return new GuestDuplicateDto
            {
                MaKhach = x.MaKhach, HoTen = x.HoTen, SoDT = x.SoDT, CCCD = CheCccd(x.CCCD), LyDo = lyDo,
                Muc = lyDo.Any(l => l.Contains("điện thoại") || l.Contains("CCCD")) ? "CHAC_CHAN" : "NGHI_NGO"
            };
        }).ToList();
    }

    /// <summary>Chuẩn hóa số giấy tờ: bỏ khoảng trắng hai đầu, rỗng = không có; chỉ chữ và số, 6-20 ký tự (CCCD/CMND/hộ chiếu).</summary>
    private static string? ChuanHoaCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return null;
        var s = cccd.Trim();
        if (s.Length is < 6 or > 20 || !s.All(char.IsLetterOrDigit))
            throw new ApiException(StatusCodes.Status400BadRequest, "CCCD_KHONG_HOP_LE", "Số giấy tờ chỉ gồm chữ và số (6-20 ký tự).", "cccd");
        return s;
    }

    private static string MoTaThayDoi(Khach cu, GuestRequest moi, string? emailMoi)
    {
        var ds = new List<string>();
        void So(string ten, string? a, string? b)
        {
            if (!string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.Ordinal)) ds.Add($"{ten}: {(string.IsNullOrWhiteSpace(a) ? "(trống)" : a)} -> {(string.IsNullOrWhiteSpace(b) ? "(trống)" : b)}");
        }
        So("hoTen", cu.HoTen, moi.HoTen);
        So("soDT", cu.SoDT, moi.SoDT);
        So("cccd", CheCccd(cu.CCCD), CheCccd(moi.CCCD));
        So("email", cu.Email, emailMoi);
        So("diaChi", cu.DiaChi, moi.DiaChi);
        return string.Join("; ", ds);
    }

    private static string KhongDau(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D').Trim();
    }
}
