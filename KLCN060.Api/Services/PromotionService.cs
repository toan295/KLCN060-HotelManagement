using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Promotions;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class PromotionService : IPromotionService
{
    private const decimal SoTienGiamToiDa = 100_000_000m;

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public PromotionService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    // =====================================================================================================
    // Đọc
    // =====================================================================================================

    public async Task<List<PromotionDto>> GetActiveAsync()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var ds = await _context.KhuyenMais.AsNoTracking()
            .Where(x => !x.TamNgung && x.NgayBatDau <= homNay && x.NgayKetThuc >= homNay
                     && (x.SoLuongGioiHan == null || x.SoLuongDaSuDung < x.SoLuongGioiHan))
            .OrderBy(x => x.MaKM).ToListAsync();
        return await ToDtosAsync(ds);
    }

    public async Task<List<PromotionDto>> GetAllAsync()
    {
        var ds = await _context.KhuyenMais.AsNoTracking().OrderBy(x => x.MaKM).ToListAsync();
        return await ToDtosAsync(ds);
    }

    public async Task<PromotionDto> GetByIdAsync(string maKM)
        => (await ToDtosAsync(new List<KhuyenMai> { await TimAsync(maKM, theoDoi: false) })).First();

    private async Task<List<PromotionDto>> ToDtosAsync(List<KhuyenMai> ds)
    {
        var loai = await _context.LoaiPhongs.AsNoTracking().ToDictionaryAsync(x => x.MaLoai, x => x.TenLoai);
        var ma = ds.Select(x => x.MaKM).ToList();
        var dung = (await _context.ChiTietKhuyenMais.AsNoTracking().Where(x => ma.Contains(x.MaKM))
                .Select(x => new { x.MaKM, x.TongKhuyenMai, TrangThai = x.PhieuDatPhong.TrangThai }).ToListAsync())
            .GroupBy(x => x.MaKM).ToDictionary(g => g.Key, g => new
            {
                SoPhieu = g.Count(),
                DangDung = g.Count(x => x.TrangThai is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN or TrangThaiPhieuDat.DA_NHAN_PHONG),
                TongGiam = g.Where(x => x.TrangThai is not (TrangThaiPhieuDat.DA_HUY or TrangThaiPhieuDat.KHONG_DEN)).Sum(x => x.TongKhuyenMai)
            });
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        return ds.Select(x =>
        {
            var d = dung.GetValueOrDefault(x.MaKM);
            var dto = ToDto(x);
            dto.TrangThai = TinhTrangThai(x, homNay);
            dto.TenLoaiApDung = x.MaLoaiApDung is null ? null : loai.GetValueOrDefault(x.MaLoaiApDung, x.MaLoaiApDung);
            dto.SoPhieuDaApDung = d?.SoPhieu ?? 0;
            dto.TongTienDaGiam = d?.TongGiam ?? 0;
            var chuaDung = x.SoLuongDaSuDung == 0 && (d?.SoPhieu ?? 0) == 0;
            // Chương trình đã kết thúc (hết hạn) và không còn đặt phòng nào đang dùng thì xóa được dù từng được áp dụng.
            var daKetThucXoaDuoc = x.NgayKetThuc < homNay && (d?.DangDung ?? 0) == 0;
            dto.CoTheXoa = chuaDung || daKetThucXoaDuoc;
            dto.DaSuDung = !chuaDung;
            dto.LyDoKhoa = dto.CoTheXoa
                ? null
                : x.NgayKetThuc < homNay
                    ? $"Chương trình đã kết thúc nhưng còn {d?.DangDung} đặt phòng đang dùng (chưa trả phòng / chưa hủy): chỉ xóa được khi các đặt phòng đó đã hoàn tất."
                    : $"Đã được dùng cho {Math.Max(d?.SoPhieu ?? 0, x.SoLuongDaSuDung)} đặt phòng: không xóa và không đổi loại / giá trị / phạm vi / điều kiện (sẽ làm sai các đặt phòng đã áp dụng). Muốn đổi, hãy tạm ngưng chương trình này và tạo chương trình mới. Chương trình đã kết thúc thì xóa được.";
            return dto;
        }).ToList();
    }

    internal static string TinhTrangThai(KhuyenMai x, DateOnly homNay)
        => x.TamNgung ? "TAM_NGUNG"
         : x.NgayKetThuc < homNay ? "DA_KET_THUC"
         : x.NgayBatDau > homNay ? "CHUA_BAT_DAU"
         : x.SoLuongGioiHan is { } gh && x.SoLuongDaSuDung >= gh ? "HET_LUOT"
         : "DANG_AP_DUNG";

    public async Task<List<PromotionUsageDto>> GetUsageAsync(string maKM)
    {
        await TimAsync(maKM, theoDoi: false);
        return await _context.ChiTietKhuyenMais.AsNoTracking().Where(x => x.MaKM == maKM)
            .OrderByDescending(x => x.PhieuDatPhong.NgayDat)
            .Select(x => new PromotionUsageDto
            {
                MaPhieuDat = x.MaPhieuDat, HoTenKhach = x.PhieuDatPhong.Khach.HoTen, NgayDat = x.PhieuDatPhong.NgayDat,
                TrangThaiPhieu = x.PhieuDatPhong.TrangThai.ToString(), TongKhuyenMai = x.TongKhuyenMai,
                NgayDon = x.PhieuDatPhong.NgayDonDuKien, NgayTra = x.PhieuDatPhong.NgayTraDuKien
            }).Take(200).ToListAsync();
    }

    /// <summary>Giải thích vì sao một booking cụ thể được / không được hưởng khuyến mãi này (từng điều kiện đạt hay không + số tiền giảm).</summary>
    public async Task<PromotionCheckDto> KiemTraBookingAsync(string maKM, string maPhieuDat)
    {
        var km = await TimAsync(maKM, theoDoi: false);
        var phieu = await _context.PhieuDatPhongs.AsNoTracking()
            .Include(x => x.ChiTietPhieuDats).ThenInclude(c => c.Phong)
            .Include(x => x.ChiTietKhuyenMais)
            .FirstOrDefaultAsync(x => x.MaPhieuDat == maPhieuDat)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DAT_PHONG", "Không tìm thấy phiếu đặt phòng.", "maPhieuDat");

        var dong = phieu.ChiTietPhieuDats.Select(c => (c.Phong.MaLoai, c.ThanhTien)).ToList();
        var soDem = Math.Max(phieu.NgayTraDuKien.DayNumber - phieu.NgayDonDuKien.DayNumber, 0);
        var tenLoai = km.MaLoaiApDung is null ? null : await _context.LoaiPhongs.AsNoTracking().Where(x => x.MaLoai == km.MaLoaiApDung).Select(x => x.TenLoai).FirstOrDefaultAsync();
        var daApDung = phieu.ChiTietKhuyenMais.FirstOrDefault(x => x.MaKM == maKM);
        // Nếu booking đã áp dụng chương trình này, "còn lượt" tại thời điểm đặt luôn đúng (lượt đã được tính cho chính nó).
        var checks = PromotionRules.DanhGia(km, DateOnly.FromDateTime(phieu.NgayDat), soDem, dong, tinhLuot: daApDung is null, tenLoai);
        if (daApDung is not null)
            checks.Add(new("LUOT", "Lượt sử dụng", true, "Booking này đã dùng 1 lượt của chương trình."));

        var dto = new PromotionCheckDto { MaKM = km.MaKM, TenKM = km.TenKM, MaPhieuDat = maPhieuDat, DaApDung = daApDung is not null };
        dto.DieuKien = checks.Select(c => new PromotionCheckItemDto { Ma = c.Ma, Ten = c.Ten, Dat = c.Dat, ChiTiet = c.ChiTiet }).ToList();
        dto.DuDieuKien = checks.All(c => c.Dat);
        dto.TienGiamDuKien = dto.DuDieuKien ? PromotionRules.TinhTien(km, dong) : 0;
        dto.TienGiamThucTe = daApDung?.TongKhuyenMai ?? 0;
        var khac = phieu.ChiTietKhuyenMais.FirstOrDefault(x => x.MaKM != maKM);
        dto.KetLuan = daApDung is not null
            ? $"Booking ĐÃ được hưởng khuyến mãi này: giảm {dto.TienGiamThucTe:N0}đ."
            : khac is not null
                ? $"Booking KHÔNG hưởng khuyến mãi này vì mỗi đặt phòng chỉ áp dụng một chương trình (đã dùng {khac.MaKM}, giảm {khac.TongKhuyenMai:N0}đ)."
                : dto.DuDieuKien ? $"Booking đủ điều kiện nhưng chưa áp dụng (khuyến mãi chỉ được chọn lúc tạo đặt phòng). Nếu áp dụng sẽ giảm {dto.TienGiamDuKien:N0}đ."
                : "Booking KHÔNG đủ điều kiện: " + string.Join("; ", checks.Where(c => !c.Dat).Select(c => c.Ten.ToLowerInvariant())) + ".";
        return dto;
    }

    // =====================================================================================================
    // Ghi
    // =====================================================================================================

    public async Task<PromotionDto> CreateAsync(PromotionRequest request)
    {
        var loaiKM = ParseLoaiKM(request.LoaiKM);
        KiemTraTenVaSoLuong(request.TenKM, request.SoLuongGioiHan);
        KiemTraGiaTri(loaiKM, request.GiaTri, request.PhanTramKM);
        KiemTraNgay(request.NgayBatDau, request.NgayKetThuc, chuongTrinhMoi: true);
        await ChuanHoaKiemTraThemAsync(request, loaiKM, null);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // [Khối 8 - P1-4] Transaction + khóa sinh mã theo tiền tố (xem MaCodeGenerator.GenerateNextAsync): 2 request tạo
        // đồng thời không còn sinh trùng mã rồi bị từ chối oan bằng 409 XUNG_DOT_DU_LIEU.
        var maKM = await MaCodeGenerator.GenerateNextAsync(_context, _context.KhuyenMais.Select(x => x.MaKM), "KM", 2);

        var khuyenMai = new KhuyenMai
        {
            MaKM = maKM,
            TenKM = request.TenKM.Trim(),
            PhanTramKM = request.PhanTramKM,
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            DieuKien = request.DieuKien,
            LoaiKM = loaiKM,
            GiaTri = request.GiaTri,
            SoLuongGioiHan = request.SoLuongGioiHan,
            SoLuongDaSuDung = 0,
            MaLoaiApDung = request.MaLoaiApDung,
            SoDemToiThieu = request.SoDemToiThieu,
            TongTienToiThieu = request.TongTienToiThieu
        };

        _context.KhuyenMais.Add(khuyenMai);
        _audit.Ghi("TAO_KHUYEN_MAI", maKM, MoTa(khuyenMai));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo khuyến mãi, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return await GetByIdAsync(maKM);
    }

    public async Task<PromotionDto> UpdateAsync(string maKM, PromotionRequest request)
    {
        var khuyenMai = await TimAsync(maKM, theoDoi: true);

        var loaiKM = ParseLoaiKM(request.LoaiKM);
        KiemTraTenVaSoLuong(request.TenKM, request.SoLuongGioiHan);
        KiemTraGiaTri(loaiKM, request.GiaTri, request.PhanTramKM);
        KiemTraNgay(request.NgayBatDau, request.NgayKetThuc);
        await ChuanHoaKiemTraThemAsync(request, loaiKM, maKM);

        var daDung = khuyenMai.SoLuongDaSuDung > 0 || await _context.ChiTietKhuyenMais.AnyAsync(x => x.MaKM == maKM);
        if (daDung)
        {
            // Các đặt phòng đã áp dụng được tính lại theo chương trình mỗi khi sửa phiếu / nhận sớm: đổi cách tính sẽ làm sai chúng.
            var doiLoi = new List<string>();
            if (loaiKM != khuyenMai.LoaiKM) doiLoi.Add("loại khuyến mãi");
            decimal? HieuLuc(LoaiKhuyenMai l, decimal? gt, decimal? pt) => l == LoaiKhuyenMai.PHAN_TRAM ? (pt ?? gt) : l == LoaiKhuyenMai.SO_TIEN_CO_DINH ? gt : null;
            if (HieuLuc(loaiKM, request.GiaTri, request.PhanTramKM) != HieuLuc(khuyenMai.LoaiKM, khuyenMai.GiaTri, khuyenMai.PhanTramKM)) doiLoi.Add("giá trị giảm");
            if (request.MaLoaiApDung != khuyenMai.MaLoaiApDung) doiLoi.Add("phạm vi loại phòng");
            if (request.SoDemToiThieu != khuyenMai.SoDemToiThieu) doiLoi.Add("số đêm tối thiểu");
            if (request.TongTienToiThieu != khuyenMai.TongTienToiThieu) doiLoi.Add("giá trị tối thiểu");
            if (request.NgayBatDau != khuyenMai.NgayBatDau) doiLoi.Add("ngày bắt đầu");
            if (doiLoi.Count > 0)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DA_SU_DUNG",
                    $"Khuyến mãi {maKM} đã được dùng cho đặt phòng nên không đổi {string.Join(", ", doiLoi)}. Chỉ được sửa tên, ngày kết thúc, số lượt giới hạn và điều kiện mô tả; muốn đổi cách tính hãy tạm ngưng và tạo chương trình mới.");
            if (request.SoLuongGioiHan is { } gh && gh < khuyenMai.SoLuongDaSuDung)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_GIOI_HAN_KHONG_HOP_LE", $"Số lượt giới hạn không được nhỏ hơn số lượt đã dùng ({khuyenMai.SoLuongDaSuDung}).", "soLuongGioiHan");
        }

        var truoc = MoTa(khuyenMai);
        khuyenMai.TenKM = request.TenKM.Trim();
        khuyenMai.PhanTramKM = request.PhanTramKM;
        khuyenMai.NgayBatDau = request.NgayBatDau;
        khuyenMai.NgayKetThuc = request.NgayKetThuc;
        khuyenMai.DieuKien = request.DieuKien;
        khuyenMai.LoaiKM = loaiKM;
        khuyenMai.GiaTri = request.GiaTri;
        khuyenMai.SoLuongGioiHan = request.SoLuongGioiHan;
        khuyenMai.MaLoaiApDung = request.MaLoaiApDung;
        khuyenMai.SoDemToiThieu = request.SoDemToiThieu;
        khuyenMai.TongTienToiThieu = request.TongTienToiThieu;
        var sau = MoTa(khuyenMai);
        if (truoc != sau) _audit.Ghi("SUA_KHUYEN_MAI", maKM, $"truoc=[{truoc}]; sau=[{sau}]");

        await _context.SaveChangesAsync();
        return await GetByIdAsync(maKM);
    }

    public async Task<PromotionDto> TamNgungAsync(string maKM, string lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do tạm ngưng.", "lyDo");
        var km = await TimAsync(maKM, theoDoi: false);
        if (km.TamNgung)
            throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DA_TAM_NGUNG", "Khuyến mãi này đang tạm ngưng.");
        var lyDoCat = lyDo.Trim().Length > 255 ? lyDo.Trim()[..255] : lyDo.Trim();
        // Cập nhật có điều kiện: nhiều người bấm cùng lúc thì chỉ một người thành công (và một dòng nhật ký).
        var soDong = await _context.KhuyenMais.Where(x => x.MaKM == maKM && !x.TamNgung)
            .ExecuteUpdateAsync(x => x.SetProperty(k => k.TamNgung, true).SetProperty(k => k.LyDoTamNgung, lyDoCat));
        if (soDong == 0)
            throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DA_TAM_NGUNG", "Khuyến mãi này đang tạm ngưng.");
        _audit.Ghi("TAM_NGUNG_KHUYEN_MAI", maKM, $"lyDo={lyDoCat}");
        await _context.SaveChangesAsync();
        return await GetByIdAsync(maKM);
    }

    public async Task<PromotionDto> TiepTucAsync(string maKM)
    {
        var km = await TimAsync(maKM, theoDoi: false);
        if (!km.TamNgung)
            throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DANG_HOAT_DONG", "Khuyến mãi này không bị tạm ngưng.");
        var soDong = await _context.KhuyenMais.Where(x => x.MaKM == maKM && x.TamNgung)
            .ExecuteUpdateAsync(x => x.SetProperty(k => k.TamNgung, false).SetProperty(k => k.LyDoTamNgung, (string?)null));
        if (soDong == 0)
            throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DANG_HOAT_DONG", "Khuyến mãi này không bị tạm ngưng.");
        _audit.Ghi("TIEP_TUC_KHUYEN_MAI", maKM, $"lyDoTamNgungTruoc={km.LyDoTamNgung}");
        await _context.SaveChangesAsync();
        return await GetByIdAsync(maKM);
    }

    public async Task DeleteAsync(string maKM)
    {
        var km = await TimAsync(maKM, theoDoi: true);
        var daApDung = await _context.ChiTietKhuyenMais.Include(x => x.PhieuDatPhong).Where(x => x.MaKM == maKM).ToListAsync();
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (km.SoLuongDaSuDung > 0 || daApDung.Count > 0)
        {
            var dangDung = daApDung.Count(x => x.PhieuDatPhong.TrangThai is TrangThaiPhieuDat.CHO_XAC_NHAN or TrangThaiPhieuDat.DA_XAC_NHAN or TrangThaiPhieuDat.DA_NHAN_PHONG);
            if (km.NgayKetThuc >= homNay)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DA_SU_DUNG",
                    $"Khuyến mãi {maKM} đã được dùng cho đặt phòng nên không xóa khi còn hiệu lực (sẽ mất lịch sử khuyến mãi). Hãy tạm ngưng chương trình để ngừng áp dụng; chương trình đã kết thúc thì xóa được.");
            if (dangDung > 0)
                throw new ApiException(StatusCodes.Status409Conflict, "KHUYEN_MAI_DANG_DUOC_DUNG",
                    $"Khuyến mãi {maKM} đã kết thúc nhưng còn {dangDung} đặt phòng đang dùng (chưa trả phòng / chưa hủy) nên chưa xóa được.");
            // Các đặt phòng còn lại đã hoàn tất / hủy: hóa đơn đã lưu dòng khuyến mãi nên chỉ gỡ liên kết; số liệu được ghi vào nhật ký.
            _context.ChiTietKhuyenMais.RemoveRange(daApDung);
        }
        _audit.Ghi("XOA_KHUYEN_MAI", maKM, MoTa(km) + (daApDung.Count > 0 ? $"; daApDung={daApDung.Count}; tongDaGiam={Math.Round(daApDung.Sum(x => x.TongKhuyenMai), 0)}" : ""));
        _context.KhuyenMais.Remove(km);
        await _context.SaveChangesAsync();
    }

    // =====================================================================================================
    // Hỗ trợ
    // =====================================================================================================

    private async Task<KhuyenMai> TimAsync(string maKM, bool theoDoi)
    {
        var q = theoDoi ? _context.KhuyenMais : _context.KhuyenMais.AsNoTracking();
        return await q.FirstOrDefaultAsync(x => x.MaKM == maKM)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHUYEN_MAI", "Không tìm thấy khuyến mãi.");
    }

    /// <summary>Chuẩn hóa + kiểm tra các trường mới: tên không trùng, giá trị tối đa, phạm vi loại phòng, số đêm / giá trị tối thiểu, quà tặng phải mô tả.</summary>
    private async Task ChuanHoaKiemTraThemAsync(PromotionRequest r, LoaiKhuyenMai loai, string? maBoQua)
    {
        r.TenKM = r.TenKM.Trim();
        if (r.TenKM.Length < 3)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_KHUYEN_MAI", "Tên khuyến mãi tối thiểu 3 ký tự.", "tenKM");
        if (await _context.KhuyenMais.AnyAsync(x => x.TenKM == r.TenKM && x.MaKM != maBoQua))
            throw new ApiException(StatusCodes.Status409Conflict, "TEN_KHUYEN_MAI_DA_TON_TAI", "Đã có chương trình khuyến mãi mang tên này.", "tenKM");
        if (loai == LoaiKhuyenMai.SO_TIEN_CO_DINH && r.GiaTri > SoTienGiamToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", $"Số tiền giảm tối đa {SoTienGiamToiDa:N0}đ.", "giaTri");
        r.DieuKien = string.IsNullOrWhiteSpace(r.DieuKien) ? null : r.DieuKien.Trim();
        if (loai == LoaiKhuyenMai.QUA_TANG)
        {
            if (r.DieuKien is null)
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MO_TA_QUA_TANG", "Khuyến mãi quà tặng cần mô tả quà tặng (ô Điều kiện / mô tả).", "dieuKien");
            r.GiaTri = null;
            r.PhanTramKM = null;
        }
        else if (loai == LoaiKhuyenMai.PHAN_TRAM)
            r.PhanTramKM ??= r.GiaTri;
        else
            r.PhanTramKM = null;   // số tiền cố định không dùng phần trăm
        if (r.SoDemToiThieu is { } d && (d < 1 || d > 365))
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_DEM_TOI_THIEU_KHONG_HOP_LE", "Số đêm tối thiểu từ 1 đến 365 (bỏ trống nếu không yêu cầu).", "soDemToiThieu");
        if (r.TongTienToiThieu is { } t && (t <= 0 || t > 1_000_000_000m || t != decimal.Truncate(t)))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_TOI_THIEU_KHONG_HOP_LE", "Giá trị tiền phòng tối thiểu phải là số nguyên đồng lớn hơn 0 (bỏ trống nếu không yêu cầu).", "tongTienToiThieu");
        r.MaLoaiApDung = string.IsNullOrWhiteSpace(r.MaLoaiApDung) ? null : r.MaLoaiApDung.Trim();
        if (r.MaLoaiApDung is not null && !await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == r.MaLoaiApDung))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng áp dụng.", "maLoaiApDung");
    }

    private static string MoTa(KhuyenMai x)
        => $"ten={x.TenKM}; loai={x.LoaiKM}; giaTri={x.GiaTri ?? x.PhanTramKM}; tu={x.NgayBatDau:yyyy-MM-dd}; den={x.NgayKetThuc:yyyy-MM-dd}; luot={x.SoLuongGioiHan?.ToString() ?? "khong gioi han"}; " +
           $"loaiPhong={x.MaLoaiApDung ?? "tat ca"}; soDemToiThieu={x.SoDemToiThieu}; tienToiThieu={x.TongTienToiThieu}; dieuKien={x.DieuKien}";

    internal static void KiemTraNgay(DateOnly batDau, DateOnly ketThuc, bool chuongTrinhMoi = false)
    {
        // Thiếu ngày trong JSON sẽ thành 0001-01-01: chương trình ra đời đã "kết thúc" mà không báo lỗi.
        if (batDau == default)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_NGAY_KHUYEN_MAI", "Ngày bắt đầu là bắt buộc.", "ngayBatDau");
        if (ketThuc == default)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_NGAY_KHUYEN_MAI", "Ngày kết thúc là bắt buộc.", "ngayKetThuc");
        if (ketThuc < batDau)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        // Chỉ chặn khi tạo mới; sửa chương trình cũ (đã hết hạn) vẫn được phép.
        if (chuongTrinhMoi && ketThuc < DateOnly.FromDateTime(DateTime.Now))
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày kết thúc đã qua: không thể tạo chương trình khuyến mãi đã hết hạn.", "ngayKetThuc");
    }

    internal static void KiemTraGiaTri(LoaiKhuyenMai loaiKM, decimal? giaTri, decimal? phanTramKM = null)
    {
        if (loaiKM != LoaiKhuyenMai.QUA_TANG && giaTri is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_GIA_TRI_KHUYEN_MAI", "GiaTri là bắt buộc khi LoaiKM khác QUA_TANG.", "giaTri");

        // [Khối 7b - K7-7] Trước đây không chặn giá trị âm/0/>100%. BookingService.TinhKhuyenMai chỉ chặn TRẦN
        // (Math.Min(giam, tongTien)) nên SO_TIEN_CO_DINH âm → "giảm" âm → TĂNG tiền khách phải trả.
        // PHAN_TRAM tính theo (PhanTramKM ?? GiaTri) nên phải kiểm cả 2 trường, báo đúng trường sai.
        if (phanTramKM is not null && (phanTramKM <= 0 || phanTramKM > 100))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "PhanTramKM phải lớn hơn 0 và không vượt quá 100.", "phanTramKM");
        if (loaiKM == LoaiKhuyenMai.PHAN_TRAM && giaTri is not null && (giaTri <= 0 || giaTri > 100))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "Khuyến mãi phần trăm: GiaTri phải lớn hơn 0 và không vượt quá 100.", "giaTri");
        if (loaiKM == LoaiKhuyenMai.SO_TIEN_CO_DINH && giaTri <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "Khuyến mãi số tiền cố định: GiaTri phải lớn hơn 0.", "giaTri");
        // VND là số nguyên; phần trăm tối đa 2 chữ số thập phân (cột lưu 2 chữ số, không để làm tròn âm thầm).
        if (loaiKM == LoaiKhuyenMai.SO_TIEN_CO_DINH && giaTri is { } st && st != decimal.Truncate(st))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "Số tiền giảm phải là số nguyên đồng (không có phần lẻ).", "giaTri");
        if (loaiKM == LoaiKhuyenMai.PHAN_TRAM && giaTri is { } g && g != decimal.Round(g, 2))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "Phần trăm tối đa 2 chữ số thập phân.", "giaTri");
        if (phanTramKM is { } pt && pt != decimal.Round(pt, 2))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_KHONG_HOP_LE", "Phần trăm tối đa 2 chữ số thập phân.", "phanTramKM");
        if (loaiKM == LoaiKhuyenMai.PHAN_TRAM && phanTramKM is not null && giaTri is not null && phanTramKM != giaTri)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHUYEN_MAI_MAU_THUAN", "PhanTramKM và GiaTri đang khác nhau: khuyến mãi phần trăm chỉ cần một giá trị.", "phanTramKM");
    }

    internal static void KiemTraTenVaSoLuong(string? tenKM, int? soLuongGioiHan)
    {
        if (string.IsNullOrWhiteSpace(tenKM))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_KHUYEN_MAI", "Tên khuyến mãi là bắt buộc.", "tenKM");
        if (soLuongGioiHan is not null && soLuongGioiHan < 1)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_GIOI_HAN_KHONG_HOP_LE", "SoLuongGioiHan phải từ 1 trở lên (bỏ trống nếu không giới hạn).", "soLuongGioiHan");
    }

    internal static LoaiKhuyenMai ParseLoaiKM(string loaiKM)
    {
        // Enum.TryParse nhận cả số ("1", "-1", "99") và danh sách "A,B": chỉ chấp nhận đúng tên loại.
        if (string.IsNullOrWhiteSpace(loaiKM) || int.TryParse(loaiKM.Trim(), out _)
            || !Enum.TryParse<LoaiKhuyenMai>(loaiKM.Trim(), ignoreCase: true, out var ketQua) || !Enum.IsDefined(ketQua))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_KHUYEN_MAI_KHONG_HOP_LE", "LoaiKM không hợp lệ. Chỉ chấp nhận: PHAN_TRAM, SO_TIEN_CO_DINH, QUA_TANG.", "loaiKM");
        return ketQua;
    }

    private static PromotionDto ToDto(KhuyenMai x) => new()
    {
        MaKM = x.MaKM,
        TenKM = x.TenKM,
        PhanTramKM = x.PhanTramKM,
        NgayBatDau = x.NgayBatDau,
        NgayKetThuc = x.NgayKetThuc,
        DieuKien = x.DieuKien,
        LoaiKM = x.LoaiKM.ToString(),
        GiaTri = x.GiaTri,
        SoLuongGioiHan = x.SoLuongGioiHan,
        SoLuongDaSuDung = x.SoLuongDaSuDung,
        TamNgung = x.TamNgung,
        LyDoTamNgung = x.LyDoTamNgung,
        MaLoaiApDung = x.MaLoaiApDung,
        SoDemToiThieu = x.SoDemToiThieu,
        TongTienToiThieu = x.TongTienToiThieu
    };
}
