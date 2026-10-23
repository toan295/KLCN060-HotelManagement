using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Gói dịch vụ (mục 7 của Quản lý: danh sách dịch vụ, giá, trạng thái, promotion, package): gộp vài dịch vụ bán chung một giá gói thấp hơn mua lẻ.
/// Giá gói phải &gt; 0 và không vượt tổng giá lẻ của các dịch vụ trong gói; tắt gói (HoatDong=false) thay vì xóa khi gói đã từng được ghi cho khách.
/// </summary>
public class ServicePackageService : IServicePackageService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public ServicePackageService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<ServicePackageDto>> GetAllAsync(bool gomNgungBan)
    {
        var goi = await _context.GoiDichVus.AsNoTracking()
            .Include(x => x.ChiTietGois).ThenInclude(c => c.DichVu)
            .Where(x => gomNgungBan || x.HoatDong)
            .OrderBy(x => x.MaGoi).ToListAsync();
        var dtos = goi.Select(ToDto).ToList();
        if (gomNgungBan) await BoSungThongKeAsync(dtos);
        return dtos;
    }

    public async Task<ServicePackageDto> CreateAsync(ServicePackageRequest request, string tenDN)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KiemTraAsync(request, null);
        var ma = await MaCodeGenerator.GenerateNextAsync(_context, _context.GoiDichVus.Select(x => x.MaGoi), "GD", 2);

        var goi = new GoiDichVu
        {
            MaGoi = ma,
            TenGoi = request.TenGoi.Trim(),
            MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
            GiaGoi = request.GiaGoi,
            HoatDong = request.HoatDong ?? true,
            NgayTao = DateTime.Now,
            TaoBoi = tenDN
        };
        foreach (var d in request.DichVus)
            goi.ChiTietGois.Add(new ChiTietGoiDichVu { MaGoi = ma, MaDV = d.MaDV.Trim(), SoLuong = d.SoLuong });

        _context.GoiDichVus.Add(goi);
        _audit.Ghi("TAO_GOI_DICH_VU", ma, MoTa(goi));
        await LuuAsync();
        await transaction.CommitAsync();
        return await LayAsync(ma);
    }

    public async Task<ServicePackageDto> UpdateAsync(string maGoi, ServicePackageRequest request)
    {
        var goi = await _context.GoiDichVus.Include(x => x.ChiTietGois).FirstOrDefaultAsync(x => x.MaGoi == maGoi)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_GOI_DICH_VU", "Không tìm thấy gói dịch vụ.");
        await KiemTraAsync(request, maGoi);

        var truoc = MoTa(goi);
        if (goi.GiaGoi != request.GiaGoi)
            _audit.Ghi("DOI_GIA_GOI_DICH_VU", maGoi, $"giaCu={Math.Round(goi.GiaGoi, 0)}; giaMoi={Math.Round(request.GiaGoi, 0)}; ten={goi.TenGoi}");
        if (request.HoatDong is { } bat && bat != goi.HoatDong)
            _audit.Ghi("DOI_TRANG_THAI_GOI_DICH_VU", maGoi, $"{(goi.HoatDong ? "DANG_BAN" : "NGUNG_BAN")} -> {(bat ? "DANG_BAN" : "NGUNG_BAN")}; ten={goi.TenGoi}");
        goi.TenGoi = request.TenGoi.Trim();
        goi.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
        goi.GiaGoi = request.GiaGoi;
        if (request.HoatDong is { } hd) goi.HoatDong = hd;

        _context.ChiTietGoiDichVus.RemoveRange(goi.ChiTietGois);
        goi.ChiTietGois = request.DichVus.Select(d => new ChiTietGoiDichVu { MaGoi = maGoi, MaDV = d.MaDV.Trim(), SoLuong = d.SoLuong }).ToList();

        _audit.Ghi("SUA_GOI_DICH_VU", maGoi, $"truoc[{truoc}] -> sau[{MoTa(goi)}]");
        await LuuAsync();
        return await LayAsync(maGoi);
    }

    public async Task DeleteAsync(string maGoi)
    {
        var goi = await _context.GoiDichVus.FirstOrDefaultAsync(x => x.MaGoi == maGoi)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_GOI_DICH_VU", "Không tìm thấy gói dịch vụ.");
        if (await _context.ChiTietSuDungDVs.AnyAsync(x => x.MaGoi == maGoi))
            throw new ApiException(StatusCodes.Status409Conflict, "GOI_DICH_VU_DA_DUOC_SU_DUNG", "Gói đã được ghi cho khách nên không xóa được - hãy tắt gói (ngừng bán) để giữ nguyên lịch sử.");
        _context.GoiDichVus.Remove(goi);
        _audit.Ghi("XOA_GOI_DICH_VU", maGoi, $"ten={goi.TenGoi}");
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "GOI_DICH_VU_DA_DUOC_SU_DUNG", "Gói đã được ghi cho khách nên không xóa được - hãy tắt gói (ngừng bán).");
        }
    }

    // ------------------------------------------------------------------------------------------------

    /// <summary>Kiểm tra tên/giá/danh sách dịch vụ của gói.</summary>
    private async Task KiemTraAsync(ServicePackageRequest request, string? boQuaMaGoi)
    {
        var ten = request.TenGoi?.Trim() ?? "";
        if (ten.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_GOI_TRONG", "Tên gói không được để trống.", "tenGoi");
        if (request.DichVus is null || request.DichVus.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "GOI_CHUA_CO_DICH_VU", "Gói cần có ít nhất một dịch vụ.", "dichVus");
        if (request.DichVus.Any(x => x.SoLuong < 1))
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", "Số lượng của mỗi dịch vụ trong gói phải lớn hơn 0.", "dichVus");
        var maDVs = request.DichVus.Select(x => x.MaDV?.Trim() ?? "").ToList();
        if (maDVs.Any(x => x.Length == 0) || maDVs.Distinct().Count() != maDVs.Count)
            throw new ApiException(StatusCodes.Status400BadRequest, "DICH_VU_TRONG_GOI_KHONG_HOP_LE", "Mỗi dịch vụ chỉ xuất hiện một lần trong gói.", "dichVus");
        if (request.GiaGoi <= 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_GOI_KHONG_HOP_LE", "Giá gói phải lớn hơn 0.", "giaGoi");

        if (await _context.GoiDichVus.AnyAsync(x => x.TenGoi == ten && x.MaGoi != boQuaMaGoi))
            throw new ApiException(StatusCodes.Status409Conflict, "TEN_GOI_TRUNG", "Đã có gói dịch vụ cùng tên.", "tenGoi");

        var dichVus = await _context.DichVus.AsNoTracking().Where(x => maDVs.Contains(x.MaDV)).ToListAsync();
        var thieu = maDVs.Except(dichVus.Select(x => x.MaDV)).ToList();
        if (thieu.Count > 0)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", $"Không tìm thấy dịch vụ: {string.Join(", ", thieu)}.", "dichVus");

        var giaLe = request.DichVus.Sum(d => dichVus.Single(x => x.MaDV == d.MaDV.Trim()).GiaDV * d.SoLuong);
        if (request.GiaGoi > giaLe)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_GOI_CAO_HON_GIA_LE",
                $"Giá gói ({DichVuService.Vnd(request.GiaGoi)}) không được cao hơn tổng giá lẻ của các dịch vụ trong gói ({DichVuService.Vnd(giaLe)}).", "giaGoi");
    }

    private async Task BoSungThongKeAsync(List<ServicePackageDto> dtos)
    {
        var ban = await _context.ChiTietSuDungDVs.AsNoTracking().Where(x => x.MaGoi != null)
            .GroupBy(x => new { x.MaGoi, x.MaPhieuNhan, x.NgaySuDung }).Select(g => g.Key.MaGoi!).ToListAsync();
        var dem = ban.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => DichVuService.HanhDongDichVu.Contains(x.HanhDong) && x.DoiTuongTacDong != null && x.DoiTuongTacDong.StartsWith("GD"))
            .OrderByDescending(x => x.ThoiGian).Select(x => new { x.DoiTuongTacDong, x.ThoiGian, x.MaTaiKhoan }).Take(2000).ToListAsync();
        var suaCuoi = nhatKy.GroupBy(x => x.DoiTuongTacDong!).ToDictionary(g => g.Key, g => g.First());
        foreach (var d in dtos)
        {
            d.SoLanDaBan = dem.GetValueOrDefault(d.MaGoi);
            d.CoTheXoa = d.SoLanDaBan == 0;
            if (suaCuoi.TryGetValue(d.MaGoi, out var s)) { d.NguoiSuaCuoi = s.MaTaiKhoan; d.ThoiGianSuaCuoi = s.ThoiGian; }
        }
    }

    public async Task<List<ServiceHistoryEntryDto>> GetHistoryAsync(string maGoi)
    {
        var rows = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong == maGoi && DichVuService.HanhDongDichVu.Contains(x.HanhDong))
            .OrderByDescending(x => x.ThoiGian).Take(100).ToListAsync();
        return rows.Select(x => new ServiceHistoryEntryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, HanhDong = x.HanhDong,
            MoTaHanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();
    }

    private async Task LuuAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi lưu gói dịch vụ, vui lòng thử lại.");
        }
    }

    private async Task<ServicePackageDto> LayAsync(string maGoi)
        => ToDto(await _context.GoiDichVus.AsNoTracking().Include(x => x.ChiTietGois).ThenInclude(c => c.DichVu).SingleAsync(x => x.MaGoi == maGoi));

    private static string MoTa(GoiDichVu g)
        => $"ten={g.TenGoi}; gia={Math.Round(g.GiaGoi, 0)}; banHang={g.HoatDong}; dichVu={string.Join(",", g.ChiTietGois.OrderBy(x => x.MaDV, StringComparer.Ordinal).Select(x => $"{x.MaDV}x{x.SoLuong}"))}";

    private static ServicePackageDto ToDto(GoiDichVu g)
    {
        var dichVus = g.ChiTietGois.OrderBy(x => x.MaDV, StringComparer.Ordinal).Select(x => new ServicePackageItemDto
        {
            MaDV = x.MaDV, TenDV = x.DichVu.TenDV, DonViTinh = x.DichVu.DonViTinh, GiaDV = x.DichVu.GiaDV, SoLuong = x.SoLuong, DichVuConPhucVu = x.DichVu.HoatDong
        }).ToList();
        var giaLe = dichVus.Sum(x => x.GiaDV * x.SoLuong);
        // Phân bổ giống hệt StayService.AddServicePackageAsync (1 gói): theo tỷ lệ giá lẻ, dòng cuối nhận phần dư.
        decimal daChia = 0;
        for (var i = 0; i < dichVus.Count; i++)
        {
            var phan = i == dichVus.Count - 1
                ? g.GiaGoi - daChia
                : Math.Round(giaLe > 0 ? g.GiaGoi * dichVus[i].GiaDV * dichVus[i].SoLuong / giaLe : g.GiaGoi / dichVus.Count, 0, MidpointRounding.AwayFromZero);
            dichVus[i].PhanBo = phan;
            daChia += phan;
        }
        return new ServicePackageDto
        {
            MaGoi = g.MaGoi, TenGoi = g.TenGoi, MoTa = g.MoTa, GiaGoi = g.GiaGoi, GiaLe = giaLe, TietKiem = Math.Max(0, giaLe - g.GiaGoi),
            HoatDong = g.HoatDong, CoTheBan = g.HoatDong && dichVus.All(x => x.DichVuConPhucVu), DichVus = dichVus
        };
    }
}
