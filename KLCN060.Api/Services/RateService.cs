using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Rates;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IRateService
{
    Task<List<RateRuleDto>> GetAllAsync();
    Task<RateRuleDto> CreateAsync(RateRuleRequest request);
    Task<RateRuleDto> UpdateAsync(int maQuyTac, RateRuleRequest request);
    Task DeleteAsync(int maQuyTac);
    Task<RateQuoteDto> QuoteAsync(string maLoai, DateOnly checkin, DateOnly checkout, string? loaiDatPhong);
    /// <summary>Lịch sử thay đổi quy tắc giá (ai · lúc nào · trước → sau); <paramref name="maQuyTac"/> = null lấy toàn bộ bảng giá.</summary>
    Task<List<RateHistoryEntryDto>> GetHistoryAsync(int? maQuyTac);
    /// <summary>Giá đã chốt từng đêm của một phiếu đặt, kèm quy tắc đã áp dụng và giá nếu tính theo bảng giá hiện hành.</summary>
    Task<BookingPriceBreakdownDto> GetBookingPriceAsync(string maPhieuDat);
}

/// <summary>
/// Quản lý bảng giá phòng (quy tắc giá theo ngày/thứ/loại phòng/loại đặt phòng) và báo giá cho một khoảng đêm.
/// Công thức duy nhất: mỗi đêm, trong các quy tắc đang bật khớp (loại phòng, ngày, thứ, loại đặt phòng) lấy quy tắc có độ ưu tiên cao nhất (bằng nhau thì quy tắc tạo sau);
/// các quy tắc KHÔNG cộng dồn. Giá từng đêm được chốt vào phiếu đặt (<see cref="GiaDemDatPhong"/>) và chỉ tính lại khi đổi ngày ở, đổi phòng hoặc nhận phòng sớm.
/// </summary>
public class RateService : IRateService
{
    private const int SoDemToiDa = 366;
    public const string QuyTacTinhLaiGia = "Giá từng đêm được CHỐT khi đặt phòng; thêm/sửa/bật/tắt/xóa quy tắc giá sau đó không làm đổi giá phiếu đã đặt. Giá chỉ được tính lại theo bảng giá hiện hành khi phiếu đổi ngày ở, đổi phòng hoặc nhận phòng sớm hơn dự kiến.";

    private static readonly string[] HanhDongGia = { "TAO_QUY_TAC_GIA", "SUA_QUY_TAC_GIA", "XOA_QUY_TAC_GIA" };

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public RateService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<RateRuleDto>> GetAllAsync()
    {
        var ds = await _context.QuyTacGias.AsNoTracking().Include(x => x.LoaiPhong)
            .OrderByDescending(x => x.HoatDong).ThenByDescending(x => x.UuTien).ThenBy(x => x.MaQuyTac).ToListAsync();
        var daChot = (await _context.GiaDemDatPhongs.AsNoTracking().Where(x => x.MaQuyTac != null)
            .GroupBy(x => x.MaQuyTac!.Value)
            .Select(g => new { MaQuyTac = g.Key, SoDem = g.Count(), SoPhieu = g.Select(x => x.MaPhieuDat).Distinct().Count() }).ToListAsync())
            .ToDictionary(x => x.MaQuyTac);
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        return ds.Select(x =>
        {
            var dto = ToDto(x);
            dto.HieuLuc = !x.HoatDong ? "TAT" : x.TuNgay > homNay ? "CHUA_BAT_DAU" : x.DenNgay < homNay ? "DA_HET_HAN" : "DANG_AP_DUNG";
            if (daChot.TryGetValue(x.MaQuyTac, out var c)) { dto.SoDemDaChot = c.SoDem; dto.SoPhieuDaChot = c.SoPhieu; }
            if (x.HoatDong)
                dto.CanhBao = ds.Where(o => o.MaQuyTac != x.MaQuyTac && o.HoatDong && o.UuTien == x.UuTien && GiaoNhau(x, o))
                    .Select(o => $"Cùng độ ưu tiên {x.UuTien} và phạm vi giao nhau với \"{o.TenQuyTac}\" - đêm trùng sẽ lấy quy tắc tạo sau ({(o.MaQuyTac > x.MaQuyTac ? o.TenQuyTac : x.TenQuyTac)}). Hãy đặt độ ưu tiên khác nhau.").ToList();
            return dto;
        }).ToList();
    }

    public async Task<RateRuleDto> CreateAsync(RateRuleRequest request)
    {
        var entity = new QuyTacGia { NgayTao = DateTime.Now, TaoBoi = _audit.TenDN };
        await GanAsync(entity, request, null);
        _context.QuyTacGias.Add(entity);
        await _context.SaveChangesAsync();
        // Ghi nhật ký sau khi có mã quy tắc để lịch sử tra theo mã (QTxx) luôn đủ từ lúc tạo.
        _audit.Ghi("TAO_QUY_TAC_GIA", $"QT{entity.MaQuyTac}", $"ten={entity.TenQuyTac}; {MoTaQuyTac(entity)}");
        await _context.SaveChangesAsync();
        return (await GetAllAsync()).First(x => x.MaQuyTac == entity.MaQuyTac);
    }

    public async Task<RateRuleDto> UpdateAsync(int maQuyTac, RateRuleRequest request)
    {
        var entity = await _context.QuyTacGias.FirstOrDefaultAsync(x => x.MaQuyTac == maQuyTac)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_QUY_TAC_GIA", "Không tìm thấy quy tắc giá.");
        var truoc = $"ten={entity.TenQuyTac}; {MoTaQuyTac(entity)}";
        await GanAsync(entity, request, maQuyTac);
        _audit.Ghi("SUA_QUY_TAC_GIA", $"QT{maQuyTac}", $"truoc=[{truoc}]; sau=[ten={entity.TenQuyTac}; {MoTaQuyTac(entity)}]");
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            // Quy tắc vừa bị người khác xóa giữa lúc đọc và lưu.
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_QUY_TAC_GIA", "Không tìm thấy quy tắc giá (có thể vừa bị xóa).");
        }
        return (await GetAllAsync()).First(x => x.MaQuyTac == maQuyTac);
    }

    public async Task DeleteAsync(int maQuyTac)
    {
        var entity = await _context.QuyTacGias.FirstOrDefaultAsync(x => x.MaQuyTac == maQuyTac)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_QUY_TAC_GIA", "Không tìm thấy quy tắc giá.");
        var daChot = await _context.GiaDemDatPhongs.CountAsync(x => x.MaQuyTac == maQuyTac);
        _audit.Ghi("XOA_QUY_TAC_GIA", $"QT{maQuyTac}", $"ten={entity.TenQuyTac}; {MoTaQuyTac(entity)}; demDaChot={daChot}");
        // Phiếu đặt/lượt lưu trú đã tạo giữ nguyên giá đã chốt (kể cả tên quy tắc đã áp dụng) nên xóa quy tắc không ảnh hưởng dữ liệu cũ.
        _context.QuyTacGias.Remove(entity);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            // Nhiều yêu cầu xóa cùng một quy tắc: chỉ một yêu cầu xóa được, các yêu cầu còn lại coi như "không còn quy tắc".
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_QUY_TAC_GIA", "Không tìm thấy quy tắc giá (có thể vừa bị xóa).");
        }
    }

    public async Task<RateQuoteDto> QuoteAsync(string maLoai, DateOnly checkin, DateOnly checkout, string? loaiDatPhong)
    {
        if (checkout <= checkin)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày nhận.", "checkout");
        if (checkout.DayNumber - checkin.DayNumber > SoDemToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Báo giá tối đa {SoDemToiDa} đêm.", "checkout");
        var loai = LoaiDatPhongHopLe(loaiDatPhong, "loaiDatPhong") ?? LoaiDatPhong.CA_NHAN;
        var loaiPhong = await _context.LoaiPhongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaLoai == maLoai)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.", "maLoai");

        var quyTacs = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
        var ketQua = GiaPhongCalculator.Tinh(loaiPhong, checkin, checkout, loai, quyTacs);
        return new RateQuoteDto
        {
            MaLoai = loaiPhong.MaLoai,
            TenLoai = loaiPhong.TenLoai,
            LoaiDatPhong = loai.ToString(),
            DonGiaCoBan = loaiPhong.DonGia,
            SoDem = ketQua.TheoDem.Count,
            DonGiaBinhQuan = ketQua.DonGiaBinhQuan,
            TongTien = ketQua.TongTien,
            TongCoBan = loaiPhong.DonGia * ketQua.TheoDem.Count,
            TheoDem = ketQua.TheoDem.Select(x =>
            {
                var khop = quyTacs.Where(q => GiaPhongCalculator.ApDungChoDem(q, loaiPhong.MaLoai, x.Dem, loai))
                    .OrderByDescending(q => q.UuTien).ThenByDescending(q => q.MaQuyTac).ToList();
                return new RateNightDto
                {
                    Dem = x.Dem, Gia = x.Gia, QuyTac = x.QuyTac?.TenQuyTac, GiaCoBan = loaiPhong.DonGia,
                    DieuChinh = x.QuyTac is null ? null : GiaPhongCalculator.MoTaDieuChinh(x.QuyTac), UuTien = x.QuyTac?.UuTien,
                    CacQuyTacKhop = khop.Select(q => new RateMatchedRuleDto
                    {
                        MaQuyTac = q.MaQuyTac, TenQuyTac = q.TenQuyTac, UuTien = q.UuTien, DieuChinh = GiaPhongCalculator.MoTaDieuChinh(q), DuocApDung = q.MaQuyTac == x.QuyTac?.MaQuyTac
                    }).ToList()
                };
            }).ToList()
        };
    }

    public async Task<List<RateHistoryEntryDto>> GetHistoryAsync(int? maQuyTac)
    {
        var q = _context.NhatKyThaoTacs.AsNoTracking().Where(x => HanhDongGia.Contains(x.HanhDong));
        if (maQuyTac is { } ma)
        {
            var doiTuong = $"QT{ma}";
            // Nhật ký cũ (trước khi có mã QTxx) ghi theo "#id tên" hoặc tên quy tắc: vẫn lấy theo "#id".
            q = q.Where(x => x.DoiTuongTacDong == doiTuong || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.StartsWith("#" + ma + " ")));
        }
        var rows = await q.OrderByDescending(x => x.ThoiGian).Take(maQuyTac is null ? 200 : 100).ToListAsync();
        return rows.Select(x => new RateHistoryEntryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, HanhDong = x.HanhDong, MoTaHanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), DoiTuong = x.DoiTuongTacDong, ChiTiet = x.ChiTiet
        }).ToList();
    }

    public async Task<BookingPriceBreakdownDto> GetBookingPriceAsync(string maPhieuDat)
    {
        var ma = maPhieuDat?.Trim() ?? "";
        var p = await _context.PhieuDatPhongs.AsNoTracking()
            .Include(x => x.ChiTietPhieuDats).ThenInclude(c => c.Phong).ThenInclude(ph => ph.LoaiPhong)
            .Include(x => x.ChiTietPhieuDats).ThenInclude(c => c.GiaTheoDems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.MaPhieuDat == ma)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.");

        var quyTacs = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
        var soDem = Math.Max(p.NgayTraDuKien.DayNumber - p.NgayDonDuKien.DayNumber, 1);
        var dto = new BookingPriceBreakdownDto
        {
            MaPhieuDat = p.MaPhieuDat, LoaiDatPhong = p.LoaiDatPhong.ToString(), NgayDon = p.NgayDonDuKien, NgayTra = p.NgayTraDuKien, SoDem = soDem,
            TongTienPhong = p.ChiTietPhieuDats.Sum(c => c.ThanhTien), QuyTacTinhLai = QuyTacTinhLaiGia
        };
        foreach (var ct in p.ChiTietPhieuDats.OrderBy(c => c.MaPhong, StringComparer.Ordinal))
        {
            var loai = ct.Phong.LoaiPhong;
            var hienHanh = GiaPhongCalculator.Tinh(loai, p.NgayDonDuKien, p.NgayTraDuKien, p.LoaiDatPhong, quyTacs).TheoDem.ToDictionary(x => x.Dem, x => x.Gia);
            var theoDem = ct.GiaTheoDems.OrderBy(x => x.Dem).Select(x => new BookingNightPriceDto
            {
                Dem = x.Dem, GiaCoBan = x.GiaCoBan, Gia = x.Gia, MaQuyTac = x.MaQuyTac, TenQuyTac = x.TenQuyTac, DieuChinh = x.DieuChinh,
                GiaHienHanh = hienHanh.TryGetValue(x.Dem, out var g) ? g : x.Gia
            }).ToList();
            dto.Phong.Add(new BookingRoomPriceDto
            {
                MaPhong = ct.MaPhong, TenPhong = ct.Phong.TenPhong, TenLoaiPhong = loai.TenLoai, DonGiaApDung = ct.DonGiaApDung, ThanhTien = ct.ThanhTien,
                CoGiaTungDem = theoDem.Count > 0, TongTheoDem = theoDem.Sum(x => x.Gia), TongHienHanh = theoDem.Sum(x => x.GiaHienHanh), TheoDem = theoDem
            });
        }
        return dto;
    }

    // ------------------------------------------------------------------------------------------------

    /// <summary>Hai quy tắc có thể cùng khớp một đêm (phạm vi loại phòng, loại đặt, khoảng ngày và thứ trong tuần đều giao nhau).</summary>
    private static bool GiaoNhau(QuyTacGia a, QuyTacGia b)
    {
        if (a.MaLoai is not null && b.MaLoai is not null && a.MaLoai != b.MaLoai) return false;
        if (a.LoaiDatPhongApDung is not null && b.LoaiDatPhongApDung is not null && a.LoaiDatPhongApDung != b.LoaiDatPhongApDung) return false;
        var tuA = a.TuNgay ?? DateOnly.MinValue; var denA = a.DenNgay ?? DateOnly.MaxValue;
        var tuB = b.TuNgay ?? DateOnly.MinValue; var denB = b.DenNgay ?? DateOnly.MaxValue;
        if (denA < tuB || denB < tuA) return false;
        if (!string.IsNullOrWhiteSpace(a.CacThuApDung) && !string.IsNullOrWhiteSpace(b.CacThuApDung))
        {
            var thuA = a.CacThuApDung.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var thuB = b.CacThuApDung.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!thuA.Intersect(thuB).Any()) return false;
        }
        return true;
    }

    private async Task GanAsync(QuyTacGia entity, RateRuleRequest r, int? boQuaMa)
    {
        var ten = r.TenQuyTac?.Trim();
        if (string.IsNullOrEmpty(ten))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_QUY_TAC", "Tên quy tắc giá là bắt buộc.", "tenQuyTac");
        // Chỉ nhận tên liệt kê; Enum.TryParse còn nhận chuỗi số ("2") nên phải chặn riêng.
        if (r.LoaiQuyTac is null || LaChuoiSo(r.LoaiQuyTac) || !Enum.TryParse<LoaiQuyTacGia>(r.LoaiQuyTac, ignoreCase: true, out var loaiQuyTac) || !Enum.IsDefined(loaiQuyTac))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_QUY_TAC_KHONG_HOP_LE", "LoaiQuyTac chỉ nhận: CUOI_TUAN, MUA_CAO_DIEM, LE_TET, DOANH_NGHIEP, KHAC.", "loaiQuyTac");
        if (r.KieuDieuChinh is null || LaChuoiSo(r.KieuDieuChinh) || !Enum.TryParse<KieuDieuChinhGia>(r.KieuDieuChinh, ignoreCase: true, out var kieu) || !Enum.IsDefined(kieu))
            throw new ApiException(StatusCodes.Status400BadRequest, "KIEU_DIEU_CHINH_KHONG_HOP_LE", "KieuDieuChinh chỉ nhận: PHAN_TRAM, GIA_CO_DINH.", "kieuDieuChinh");
        // Không làm tròn thầm lặng: giá cố định phải là số đồng nguyên; phần trăm tối đa 2 chữ số thập phân (cột decimal(18,2)).
        if (kieu == KieuDieuChinhGia.GIA_CO_DINH && decimal.Truncate(r.GiaTri) != r.GiaTri)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "Giá cố định phải là số đồng nguyên (không có phần lẻ).", "giaTri");
        if (kieu == KieuDieuChinhGia.PHAN_TRAM && decimal.Round(r.GiaTri, 2) != r.GiaTri)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "Phần trăm điều chỉnh tối đa 2 chữ số thập phân.", "giaTri");

        if (kieu == KieuDieuChinhGia.PHAN_TRAM && (r.GiaTri is < -90m or > 300m || r.GiaTri == 0m))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "Phần trăm điều chỉnh phải khác 0 và nằm trong khoảng -90 đến 300.", "giaTri");
        if (kieu == KieuDieuChinhGia.GIA_CO_DINH && (r.GiaTri <= 0m || r.GiaTri > 100_000_000m))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "Giá cố định mỗi đêm phải lớn hơn 0 và không quá 100.000.000đ.", "giaTri");
        if (r.TuNgay is { } tu && r.DenNgay is { } den && den < tu)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Đến ngày phải sau hoặc bằng từ ngày.", "denNgay");
        if (r.UuTien is < 0 or > 1000)
            throw new ApiException(StatusCodes.Status400BadRequest, "UU_TIEN_KHONG_HOP_LE", "Độ ưu tiên nằm trong khoảng 0-1000.", "uuTien");

        var maLoai = string.IsNullOrWhiteSpace(r.MaLoai) ? null : r.MaLoai.Trim();
        if (maLoai is not null && !await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == maLoai))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.", "maLoai");

        var cacThu = ChuanHoaCacThu(r.CacThuApDung);
        var loaiDat = LoaiDatPhongHopLe(r.LoaiDatPhongApDung, "loaiDatPhongApDung");
        if (r.TuNgay is null && r.DenNgay is null && cacThu is null && loaiDat is null && maLoai is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUY_TAC_THIEU_DIEU_KIEN",
                "Quy tắc phải giới hạn phạm vi áp dụng (khoảng ngày, thứ trong tuần, loại phòng hoặc loại đặt phòng) - nếu không sẽ đổi giá mọi đêm của mọi phòng; hãy sửa đơn giá loại phòng.");

        entity.TenQuyTac = ten;
        entity.LoaiQuyTac = loaiQuyTac;
        entity.MaLoai = maLoai;
        entity.KieuDieuChinh = kieu;
        entity.GiaTri = r.GiaTri;
        entity.TuNgay = r.TuNgay;
        entity.DenNgay = r.DenNgay;
        entity.CacThuApDung = cacThu;
        entity.LoaiDatPhongApDung = loaiDat;
        entity.UuTien = r.UuTien;
        entity.HoatDong = r.HoatDong;
        entity.GhiChu = string.IsNullOrWhiteSpace(r.GhiChu) ? null : r.GhiChu.Trim();
    }

    private static string? ChuanHoaCacThu(string? cacThu)
    {
        if (string.IsNullOrWhiteSpace(cacThu)) return null;
        var so = new SortedSet<int>();
        foreach (var phan in cacThu.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(phan, out var thu) || thu is < 0 or > 6)
                throw new ApiException(StatusCodes.Status400BadRequest, "THU_KHONG_HOP_LE", "CacThuApDung gồm các số từ 0 (Chủ nhật) đến 6 (Thứ bảy), cách nhau bằng dấu phẩy.", "cacThuApDung");
            so.Add(thu);
        }
        return so.Count == 0 ? null : string.Join(",", so);
    }

    private static LoaiDatPhong? LoaiDatPhongHopLe(string? loai, string field)
    {
        if (string.IsNullOrWhiteSpace(loai)) return null;
        if (LaChuoiSo(loai) || !Enum.TryParse<LoaiDatPhong>(loai, ignoreCase: true, out var kq) || !Enum.IsDefined(kq))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DAT_PHONG_KHONG_HOP_LE", "LoaiDatPhong chỉ chấp nhận: CA_NHAN, DOAN.", field);
        return kq;
    }

    private static bool LaChuoiSo(string s) => decimal.TryParse(s.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static string MoTaQuyTac(QuyTacGia q)
    {
        static string TenThu(string s) => s switch { "0" => "CN", "1" => "T2", "2" => "T3", "3" => "T4", "4" => "T5", "5" => "T6", "6" => "T7", _ => s };
        var loaiQuyTac = q.LoaiQuyTac switch
        {
            LoaiQuyTacGia.CUOI_TUAN => "Cuối tuần", LoaiQuyTacGia.MUA_CAO_DIEM => "Mùa cao điểm", LoaiQuyTacGia.LE_TET => "Lễ / Tết", LoaiQuyTacGia.DOANH_NGHIEP => "Đoàn", _ => "Khác"
        };
        var ngay = q.TuNgay is null && q.DenNgay is null ? "không giới hạn"
            : $"{(q.TuNgay is null ? "…" : q.TuNgay.Value.ToString("dd/MM/yyyy"))} → {(q.DenNgay is null ? "…" : q.DenNgay.Value.ToString("dd/MM/yyyy"))}";
        var thu = string.IsNullOrEmpty(q.CacThuApDung) ? "tất cả" : string.Join(", ", q.CacThuApDung.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(TenThu));
        var doiTuong = q.LoaiDatPhongApDung is null ? "tất cả" : q.LoaiDatPhongApDung == LoaiDatPhong.DOAN ? "Đoàn" : "Cá nhân";
        return $"loaiQuyTac={loaiQuyTac}; kieu={(q.KieuDieuChinh == KieuDieuChinhGia.PHAN_TRAM ? "Tăng/giảm theo %" : "Giá cố định mỗi đêm")}; " +
               $"mucDieuChinh={(q.KieuDieuChinh == KieuDieuChinhGia.PHAN_TRAM ? $"{q.GiaTri:+0.##;-0.##}%" : $"{q.GiaTri:N0}đ/đêm")}; loaiPhong={q.MaLoai ?? "tất cả"}; " +
               $"ngay={ngay}; thu={thu}; doiTuong={doiTuong}; uuTien={q.UuTien}; bat={(q.HoatDong ? "Có" : "Không")}";
    }

    private static RateRuleDto ToDto(QuyTacGia x) => new()
    {
        MaQuyTac = x.MaQuyTac,
        TenQuyTac = x.TenQuyTac,
        LoaiQuyTac = x.LoaiQuyTac.ToString(),
        MaLoai = x.MaLoai,
        TenLoai = x.LoaiPhong?.TenLoai,
        KieuDieuChinh = x.KieuDieuChinh.ToString(),
        GiaTri = x.GiaTri,
        TuNgay = x.TuNgay,
        DenNgay = x.DenNgay,
        CacThuApDung = x.CacThuApDung,
        LoaiDatPhongApDung = x.LoaiDatPhongApDung?.ToString(),
        UuTien = x.UuTien,
        HoatDong = x.HoatDong,
        GhiChu = x.GhiChu,
        NgayTao = x.NgayTao,
        TaoBoi = x.TaoBoi
    };
}
