using KLCN060.Api.DTOs.Rooms;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Danh mục phòng cho Quản lý: danh sách + thống kê (theo tầng / loại / tình trạng), mức độ đã phát sinh dữ liệu của từng phòng (xóa / đổi loại được không và vì sao),
/// tạo hàng loạt, kiểm tra tên / tầng. Tình trạng phòng KHÔNG sửa tự do ở đây: chỉ được chuyển theo sơ đồ hợp lệ và nguồn của từng tình trạng được nêu rõ.
/// </summary>
public partial class RoomService
{
    private const int TangToiDa = 100;
    private static readonly System.Text.RegularExpressions.Regex TenPhongPattern = new(@"^[A-Za-z0-9\-]{1,9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Tên phòng: 1-9 ký tự chữ-số-gạch ngang, không khoảng trắng (mã phòng = "P" + tên). Trả về tên đã cắt khoảng trắng.</summary>
    internal static string ChuanHoaTenPhong(string? tenPhong)
    {
        var s = (tenPhong ?? "").Trim();
        if (s.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_PHONG", "Tên phòng là bắt buộc.", "tenPhong");
        if (!TenPhongPattern.IsMatch(s))
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_PHONG_KHONG_HOP_LE", "Tên phòng chỉ gồm chữ không dấu, số hoặc gạch ngang (tối đa 9 ký tự, không khoảng trắng).", "tenPhong");
        return s;
    }

    internal static void KiemTraTang(int tang)
    {
        if (tang < 0 || tang > TangToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "TANG_KHONG_HOP_LE", $"Tầng phải từ 0 đến {TangToiDa}.", "tang");
    }

    /// <summary>Số liệu "phát sinh" theo từng phòng, lấy bằng vài truy vấn gộp.</summary>
    private sealed class PhatSinh
    {
        public int DatPhongTuongLai { get; set; }
        public int DatPhongTong { get; set; }
        public int LuuTru { get; set; }
        public bool DangO { get; set; }
        public int CoSoVatChat { get; set; }
        public int KhacLienQuan { get; set; }   // việc dọn, sự cố, đổi phòng, đồ bỏ quên
    }

    private async Task<Dictionary<string, PhatSinh>> LayPhatSinhAsync(List<string> maPhongs)
    {
        var kq = maPhongs.ToDictionary(x => x, _ => new PhatSinh());
        if (maPhongs.Count == 0) return kq;
        var homNay = DateOnly.FromDateTime(DateTime.Now);

        foreach (var d in await _context.ChiTietPhieuDats.AsNoTracking().Where(c => maPhongs.Contains(c.MaPhong))
                     .Select(c => new { c.MaPhong, c.PhieuDatPhong.TrangThai, c.PhieuDatPhong.NgayTraDuKien }).ToListAsync())
        {
            var p = kq[d.MaPhong];
            p.DatPhongTong++;
            if (d.TrangThai is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN && d.NgayTraDuKien >= homNay) p.DatPhongTuongLai++;
        }
        foreach (var l in await _context.ChiTietPhieuNhans.AsNoTracking().Where(c => maPhongs.Contains(c.MaPhong)).Select(c => new { c.MaPhong, c.TrangThai }).ToListAsync())
        {
            var p = kq[l.MaPhong];
            p.LuuTru++;
            if (l.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O) p.DangO = true;
        }
        foreach (var c in await _context.CoSoVatChats.AsNoTracking().Where(c => c.MaPhong != null && maPhongs.Contains(c.MaPhong)).GroupBy(c => c.MaPhong!)
                     .Select(g => new { MaPhong = g.Key, N = g.Count() }).ToListAsync())
            kq[c.MaPhong].CoSoVatChat = c.N;
        foreach (var c in await _context.PhieuDonPhongs.AsNoTracking().Where(c => maPhongs.Contains(c.MaPhong)).GroupBy(c => c.MaPhong).Select(g => new { MaPhong = g.Key, N = g.Count() }).ToListAsync())
            kq[c.MaPhong].KhacLienQuan += c.N;
        foreach (var c in await _context.SuCoPhongs.AsNoTracking().Where(c => maPhongs.Contains(c.MaPhong)).GroupBy(c => c.MaPhong).Select(g => new { MaPhong = g.Key, N = g.Count() }).ToListAsync())
            kq[c.MaPhong].KhacLienQuan += c.N;
        foreach (var c in await _context.DoBoQuens.AsNoTracking().Where(c => c.MaPhong != null && maPhongs.Contains(c.MaPhong)).GroupBy(c => c.MaPhong!).Select(g => new { MaPhong = g.Key, N = g.Count() }).ToListAsync())
            kq[c.MaPhong].KhacLienQuan += c.N;
        foreach (var c in await _context.PhieuDoiPhongs.AsNoTracking().Where(c => maPhongs.Contains(c.MaPhongCu) || maPhongs.Contains(c.MaPhongMoi))
                     .Select(c => new { c.MaPhongCu, c.MaPhongMoi }).ToListAsync())
        {
            if (kq.TryGetValue(c.MaPhongCu, out var a)) a.KhacLienQuan++;
            if (kq.TryGetValue(c.MaPhongMoi, out var b)) b.KhacLienQuan++;
        }
        return kq;
    }

    private static (bool Duoc, string? LyDo) CoTheXoaPhong(PhatSinh p)
    {
        if (p.DangO) return (false, "Phòng đang có khách lưu trú.");
        var lyDo = new List<string>();
        if (p.DatPhongTong > 0) lyDo.Add($"{p.DatPhongTong} lượt đặt phòng");
        if (p.LuuTru > 0) lyDo.Add($"{p.LuuTru} lượt lưu trú");
        if (p.CoSoVatChat > 0) lyDo.Add($"{p.CoSoVatChat} thiết bị cơ sở vật chất");
        if (p.KhacLienQuan > 0) lyDo.Add($"{p.KhacLienQuan} bản ghi dọn phòng / sự cố / đổi phòng");
        return lyDo.Count == 0 ? (true, null)
            : (false, "Đã phát sinh dữ liệu (" + string.Join(", ", lyDo) + "): xóa sẽ mất lịch sử. Hãy chuyển phòng sang \"Ngừng phục vụ / bảo trì\" thay vì xóa.");
    }

    private static (bool Duoc, string? LyDo) CoTheDoiLoaiPhong(Phong ph, PhatSinh p)
    {
        if (ph.TinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD || p.DangO) return (false, "Phòng đang có khách - đổi loại sẽ làm sai giá và hóa đơn đang chạy.");
        if (p.DatPhongTuongLai > 0) return (false, $"Phòng đang được giữ cho {p.DatPhongTuongLai} đặt phòng sắp tới - đổi loại sẽ làm sai loại phòng/giá khách đã đặt.");
        return (true, null);
    }

    public async Task<RoomCatalogDto> GetCatalogAsync(RoomCatalogQuery query)
    {
        var phongs = await _context.Phongs.AsNoTracking().Include(x => x.LoaiPhong).OrderBy(x => x.MaPhong).ToListAsync();
        var dto = new RoomCatalogDto
        {
            CapNhatLuc = DateTime.Now, TongPhong = phongs.Count, SoTang = phongs.Select(x => x.Tang).Distinct().Count(),
            SoLoaiPhong = await _context.LoaiPhongs.CountAsync(),
            TongSucChua = phongs.Sum(x => x.LoaiPhong.SoNguoiTieuChuan),
            SoPhongBaoTri = phongs.Count(x => x.TinhTrang == TinhTrangPhong.OOO),
            SoPhongCoKhach = phongs.Count(x => x.TinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD)
        };
        dto.SoPhongKhaDung = dto.TongPhong - dto.SoPhongBaoTri;
        dto.TheoTang = phongs.GroupBy(x => x.Tang).OrderBy(g => g.Key).Select(g => new RoomCatalogCountDto { Ma = g.Key.ToString(), Ten = $"Tầng {g.Key}", SoPhong = g.Count() }).ToList();
        dto.TheoLoai = phongs.GroupBy(x => new { x.MaLoai, x.LoaiPhong.TenLoai }).OrderBy(g => g.Key.MaLoai).Select(g => new RoomCatalogCountDto { Ma = g.Key.MaLoai, Ten = g.Key.TenLoai, SoPhong = g.Count() }).ToList();
        dto.TheoTinhTrang = phongs.GroupBy(x => x.TinhTrang).OrderBy(g => g.Key).Select(g => new RoomCatalogCountDto { Ma = g.Key.ToString(), Ten = TenTinhTrang(g.Key), SoPhong = g.Count() }).ToList();

        IEnumerable<Phong> loc = phongs;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            foreach (var tu in query.Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
            {
                var t = tu;
                loc = loc.Where(x => x.TenPhong.Contains(t, StringComparison.OrdinalIgnoreCase) || x.MaPhong.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || x.LoaiPhong.TenLoai.Contains(t, StringComparison.OrdinalIgnoreCase) || KhongDau(x.LoaiPhong.TenLoai).Contains(KhongDau(t), StringComparison.OrdinalIgnoreCase));
            }
        }
        if (query.Tang is { } tang) loc = loc.Where(x => x.Tang == tang);
        if (!string.IsNullOrWhiteSpace(query.MaLoai)) loc = loc.Where(x => x.MaLoai == query.MaLoai);
        if (!string.IsNullOrWhiteSpace(query.TinhTrang)) loc = loc.Where(x => x.TinhTrang == ParseTinhTrang(query.TinhTrang));
        loc = (query.SapXep ?? "").ToLowerInvariant() switch
        {
            "ten" => loc.OrderBy(x => x.TenPhong, StringComparer.OrdinalIgnoreCase),
            "tang" => loc.OrderBy(x => x.Tang).ThenBy(x => x.TenPhong, StringComparer.OrdinalIgnoreCase),
            "loai" => loc.OrderBy(x => x.MaLoai).ThenBy(x => x.TenPhong, StringComparer.OrdinalIgnoreCase),
            "tinh_trang" => loc.OrderBy(x => x.TinhTrang).ThenBy(x => x.TenPhong, StringComparer.OrdinalIgnoreCase),
            _ => loc.OrderBy(x => x.MaPhong, StringComparer.Ordinal)
        };
        var khop = loc.ToList();
        dto.TongKhopLoc = khop.Count;
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 300);
        var trang = khop.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToList();

        var ma = trang.Select(x => x.MaPhong).ToList();
        var phatSinh = await LayPhatSinhAsync(ma);
        var lichSu = (await _context.LichSuTinhTrangPhongs.AsNoTracking().Where(h => ma.Contains(h.MaPhong))
                .Select(h => new { h.MaPhong, h.DenTinhTrang, h.ThoiGian, h.MaLichSu, h.LyDo }).ToListAsync())
            .GroupBy(h => h.MaPhong).ToDictionary(g => g.Key, g => g.ToList());

        dto.Phong = trang.Select(ph =>
        {
            var p = phatSinh[ph.MaPhong];
            var (xoa, lyDoXoa) = CoTheXoaPhong(p);
            var (doi, lyDoDoi) = CoTheDoiLoaiPhong(ph, p);
            var cuoi = lichSu.GetValueOrDefault(ph.MaPhong)?.Where(h => h.DenTinhTrang == ph.TinhTrang).OrderByDescending(h => h.ThoiGian).ThenByDescending(h => h.MaLichSu).FirstOrDefault();
            return new RoomCatalogRowDto
            {
                MaPhong = ph.MaPhong, TenPhong = ph.TenPhong, Tang = ph.Tang, MaLoai = ph.MaLoai, TenLoai = ph.LoaiPhong.TenLoai,
                SucChua = ph.LoaiPhong.SoNguoiTieuChuan, DonGia = ph.LoaiPhong.DonGia, PhuThu = ph.LoaiPhong.PhuThu, TinhTrang = ph.TinhTrang.ToString(),
                TinhTrangTuLuc = cuoi?.ThoiGian, LyDoTinhTrang = cuoi?.LyDo, SoDatPhongTuongLai = p.DatPhongTuongLai, SoLuotLuuTru = p.LuuTru, SoCoSoVatChat = p.CoSoVatChat,
                CoTheXoa = xoa, LyDoKhongXoa = lyDoXoa, CoTheDoiLoai = doi, LyDoKhongDoiLoai = lyDoDoi,
                TinhTrangChoPhep = ChuyenTrangThaiHopLe.GetValueOrDefault(ph.TinhTrang, Array.Empty<TinhTrangPhong>()).Select(x => x.ToString()).ToList()
            };
        }).ToList();
        return dto;
    }

    public async Task<BulkRoomResultDto> CreateBulkAsync(BulkRoomRequest request)
    {
        if (request.TenPhongs is null || request.TenPhongs.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_DANH_SACH_PHONG", "Cần ít nhất một tên phòng.", "tenPhongs");
        if (request.TenPhongs.Count > 100)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUA_NHIEU_PHONG", "Mỗi lần chỉ tạo tối đa 100 phòng.", "tenPhongs");
        KiemTraTang(request.Tang);
        await KiemTraLoaiPhongTonTaiAsync(request.MaLoai);

        var ten = request.TenPhongs.Select(ChuanHoaTenPhong).ToList();
        var trung = ten.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (trung.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_PHONG_TRUNG_TRONG_DANH_SACH", $"Danh sách có tên phòng bị lặp: {string.Join(", ", trung)}.", "tenPhongs");
        var maMoi = ten.Select(x => $"P{x}").ToList();
        var daCo = await _context.Phongs.AsNoTracking().Where(x => maMoi.Contains(x.MaPhong) || ten.Contains(x.TenPhong)).Select(x => x.TenPhong).ToListAsync();
        if (daCo.Count > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TON_TAI", $"Các phòng đã tồn tại: {string.Join(", ", daCo)}. Không phòng nào được tạo.", "tenPhongs");

        var tinhTrang = ParseTinhTrang(request.TinhTrang ?? nameof(TinhTrangPhong.VC));
        if (tinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD)
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS_TRANSITION", "Phòng mới tạo chưa có khách nên chỉ được ở trạng thái VC, VD, VI hoặc OOO.", "tinhTrang");
        if (tinhTrang == TinhTrangPhong.OOO)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Muốn tạo phòng ở trạng thái bảo trì (OOO) hãy tạo từng phòng và ghi lý do.", "tinhTrang");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        foreach (var t in ten)
            _context.Phongs.Add(new Phong { MaPhong = $"P{t}", TenPhong = t, Tang = request.Tang, MaLoai = request.MaLoai, TinhTrang = tinhTrang, NgayTao = DateTime.Now });
        _audit.Ghi("TAO_PHONG_HANG_LOAT", $"Tang{request.Tang}", $"soPhong={ten.Count}; phong={string.Join(",", ten)}; tang={request.Tang}; loai={request.MaLoai}; tinhTrang={tinhTrang}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new BulkRoomResultDto { SoPhongDaTao = ten.Count, MaPhongs = maMoi };
    }

    private static string TenTinhTrang(TinhTrangPhong t) => t switch
    {
        TinhTrangPhong.VC => "Trống, đã dọn", TinhTrangPhong.VD => "Trống, chưa dọn", TinhTrangPhong.VI => "Trống, đã kiểm tra",
        TinhTrangPhong.OC => "Đang có khách", TinhTrangPhong.OD => "Có khách, cần dọn", _ => "Ngừng phục vụ / bảo trì"
    };

    private static string KhongDau(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
