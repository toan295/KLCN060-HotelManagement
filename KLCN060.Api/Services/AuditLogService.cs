using KLCN060.Api.DTOs.Audit;
using KLCN060.Api.Middlewares;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IAuditLogService
{
    Task<(List<AuditLogDto> Items, int Total)> SearchAsync(AuditLogQuery query);
}

/// <summary>Xem nhật ký hệ thống (bảng NhatKyThaoTacs). Chỉ đọc: nhật ký do hệ thống tự ghi, không có thao tác sửa / xóa.</summary>
public class AuditLogService : IAuditLogService
{
    private static readonly string[] KetQuaHopLe = { "THANH_CONG", "THAT_BAI", "TU_CHOI" };
    private readonly KLCN060DbContext _context;

    public AuditLogService(KLCN060DbContext context) => _context = context;

    public async Task<(List<AuditLogDto> Items, int Total)> SearchAsync(AuditLogQuery query)
    {
        if (query.TuNgay is not null && query.DenNgay is not null && query.TuNgay > query.DenNgay)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải nhỏ hơn hoặc bằng đến ngày.", "tuNgay");
        var ketQua = query.KetQua?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(ketQua) && !KetQuaHopLe.Contains(ketQua))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "KetQua chỉ nhận: THANH_CONG, THAT_BAI, TU_CHOI.", "ketQua");

        var q = _context.NhatKyThaoTacs.AsNoTracking().AsQueryable();
        if (query.TuNgay is { } tu) { var d = tu.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGian >= d); }
        if (query.DenNgay is { } den) { var d = den.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGian < d); }
        if (!string.IsNullOrWhiteSpace(query.TaiKhoan)) { var tk = query.TaiKhoan.Trim(); q = q.Where(x => x.MaTaiKhoan == tk); }
        if (!string.IsNullOrEmpty(ketQua)) q = q.Where(x => x.KetQua == ketQua);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tuKhoa = query.Q.Trim();
            // Tìm cả theo mô tả tiếng Việt: đổi các mã hành động có mô tả khớp thành điều kiện theo mã.
            var maKhop = NhatKyMoTa.TimMaTheoMoTa(tuKhoa);
            q = q.Where(x => x.HanhDong.Contains(tuKhoa) || maKhop.Contains(x.HanhDong)
                || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.Contains(tuKhoa))
                || (x.ChiTiet != null && x.ChiTiet.Contains(tuKhoa)));
        }

        var total = await q.CountAsync();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ten = await TenNguoiDung.HienThiAsync(_context, rows.Select(x => x.MaTaiKhoan));
        var items = rows.Select(x => new AuditLogDto
        {
            MaNhatKy = x.MaNhatKy,
            ThoiGian = x.ThoiGian,
            TaiKhoan = x.MaTaiKhoan,
            NguoiThucHien = ten.TryGetValue(x.MaTaiKhoan, out var t) && t != x.MaTaiKhoan ? t : null,
            HanhDong = x.HanhDong,
            MoTaHanhDong = NhatKyMoTa.MoTaHanhDong(x.HanhDong),
            DoiTuongTacDong = x.DoiTuongTacDong,
            ChiTiet = x.ChiTiet,
            KetQua = x.KetQua,
            MaLoi = x.MaLoi,
            DiaChiIP = x.DiaChiIP
        }).ToList();
        return (items, total);
    }
}
