using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class DichVuService
{
    internal static string Vnd(decimal x) => Math.Round(x, 0).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    internal static readonly string[] HanhDongDichVu =
    {
        "TAO_DICH_VU", "SUA_DICH_VU", "XOA_DICH_VU", "DOI_GIA_DICH_VU", "DOI_NHOM_DOANH_THU_DICH_VU", "DOI_TRANG_THAI_DICH_VU",
        "TAO_CHI_TIET_DICH_VU", "SUA_CHI_TIET_DICH_VU", "XOA_CHI_TIET_DICH_VU",
        "TAO_GOI_DICH_VU", "SUA_GOI_DICH_VU", "XOA_GOI_DICH_VU", "DOI_GIA_GOI_DICH_VU", "DOI_TRANG_THAI_GOI_DICH_VU"
    };

    /// <summary>Toàn bộ danh mục dịch vụ cho người quản lý: kèm thống kê sử dụng, các gói liên quan và việc còn được phép làm (xóa...).</summary>
    public async Task<ServiceCatalogDto> GetCatalogAsync()
    {
        var dichVus = await _context.DichVus.AsNoTracking().OrderBy(x => x.MaDV).ToListAsync();
        var dung = await _context.ChiTietSuDungDVs.AsNoTracking().GroupBy(x => x.MaDV)
            .Select(g => new { MaDV = g.Key, Luot = g.Count(), SoLuong = g.Sum(x => x.SoLuong), DoanhThu = g.Sum(x => x.ThanhTien), Cuoi = g.Max(x => x.NgaySuDung) })
            .ToDictionaryAsync(x => x.MaDV);
        var chiTiet = await _context.ChiTietDichVus.AsNoTracking().GroupBy(x => x.MaDV)
            .Select(g => new { MaDV = g.Key, Tong = g.Count(), HienThi = g.Count(x => x.ConHienThi) }).ToDictionaryAsync(x => x.MaDV);
        var goi = await _context.GoiDichVus.AsNoTracking().Include(x => x.ChiTietGois).ThenInclude(c => c.DichVu).ToListAsync();
        var minibar = (await _context.VatDungs.AsNoTracking().Where(x => x.MaDV != null).Select(x => x.MaDV!).Distinct().ToListAsync()).ToHashSet();

        // Người/lúc sửa gần nhất: lấy từ nhật ký (một truy vấn cho cả danh mục).
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => HanhDongDichVu.Contains(x.HanhDong) && x.DoiTuongTacDong != null && x.DoiTuongTacDong.StartsWith("DV"))
            .OrderByDescending(x => x.ThoiGian).Select(x => new { x.DoiTuongTacDong, x.ThoiGian, x.MaTaiKhoan }).Take(3000).ToListAsync();
        var suaCuoi = nhatKy.GroupBy(x => x.DoiTuongTacDong!).ToDictionary(g => g.Key, g => g.First());

        var items = dichVus.Select(d =>
        {
            dung.TryGetValue(d.MaDV, out var u);
            chiTiet.TryGetValue(d.MaDV, out var ct);
            var cacGoi = goi.Where(g => g.ChiTietGois.Any(c => c.MaDV == d.MaDV)).Select(g => g.TenGoi).OrderBy(x => x).ToList();
            var (xoaDuoc, lyDo) = LyDoKhongXoa(u?.Luot ?? 0, cacGoi.Count, minibar.Contains(d.MaDV));
            suaCuoi.TryGetValue(d.MaDV, out var s);
            return new ServiceCatalogItemDto
            {
                MaDV = d.MaDV, TenDV = d.TenDV, GiaDV = d.GiaDV, DonViTinh = d.DonViTinh, HoatDong = d.HoatDong, LoaiDoanhThu = d.LoaiDoanhThu.ToString(),
                SoLuotDung = u?.Luot ?? 0, TongSoLuong = u?.SoLuong ?? 0, DoanhThu = u?.DoanhThu ?? 0, LanDungCuoi = u?.Cuoi,
                SoMucChiTiet = ct?.Tong ?? 0, SoMucChiTietHienThi = ct?.HienThi ?? 0, CacGoiChua = cacGoi, DangGanMinibar = minibar.Contains(d.MaDV),
                CoTheXoa = xoaDuoc, LyDoKhongXoa = lyDo,
                NguoiSuaCuoi = s is null ? null : s.MaTaiKhoan, ThoiGianSuaCuoi = s?.ThoiGian
            };
        }).ToList();

        var goiKhongBan = goi.Count(g => g.HoatDong && g.ChiTietGois.Any(c => !c.DichVu.HoatDong));
        return new ServiceCatalogDto
        {
            CapNhatLuc = DateTime.Now,
            DichVus = items,
            TongHop = new ServiceCatalogSummaryDto
            {
                TongDichVu = items.Count, DangPhucVu = items.Count(x => x.HoatDong), NgungPhucVu = items.Count(x => !x.HoatDong),
                DichVuChuaCoChiTiet = items.Count(x => x.SoMucChiTietHienThi == 0), DichVuChuaTungDung = items.Count(x => x.SoLuotDung == 0),
                TongGoi = goi.Count, GoiDangBan = goi.Count(g => g.HoatDong), GoiKhongBanDuoc = goiKhongBan,
                DoanhThuDichVu = items.Sum(x => x.DoanhThu)
            }
        };
    }

    public async Task<List<ServiceHistoryEntryDto>> GetHistoryAsync(string ma)
    {
        var rows = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong == ma && HanhDongDichVu.Contains(x.HanhDong))
            .OrderByDescending(x => x.ThoiGian).Take(100).ToListAsync();
        // Dịch vụ đã xóa vẫn còn lịch sử; chỉ khi không có cả dịch vụ lẫn lịch sử thì mã mới là sai.
        if (rows.Count == 0 && !await _context.DichVus.AnyAsync(x => x.MaDV == ma))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ.");
        return rows.Select(x => new ServiceHistoryEntryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, HanhDong = x.HanhDong,
            MoTaHanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();
    }

    private static (bool Duoc, string? LyDo) LyDoKhongXoa(int soLuot, int soGoi, bool minibar)
    {
        var ly = new List<string>();
        if (soLuot > 0) ly.Add($"đã được ghi cho khách {soLuot} lần (hóa đơn cũ còn dùng)");
        if (soGoi > 0) ly.Add($"đang nằm trong {soGoi} gói dịch vụ");
        if (minibar) ly.Add("đang gắn với vật dụng minibar tính phí");
        return ly.Count == 0 ? (true, null) : (false, "Không xóa được vì dịch vụ " + string.Join(", ", ly) + ". Hãy chuyển sang \"Ngừng phục vụ\" để giữ nguyên lịch sử.");
    }

    /// <summary>Giá mới của dịch vụ không được làm tổng giá lẻ của một gói thấp hơn giá gói (gói sẽ thành vô lý).</summary>
    private async Task KiemTraGiaMoiVoiGoiAsync(string maDV, decimal giaMoi)
    {
        var goi = await _context.GoiDichVus.AsNoTracking().Include(x => x.ChiTietGois).ThenInclude(c => c.DichVu)
            .Where(x => x.ChiTietGois.Any(c => c.MaDV == maDV)).ToListAsync();
        var loi = new List<string>();
        foreach (var g in goi)
        {
            var giaLe = g.ChiTietGois.Sum(c => (c.MaDV == maDV ? giaMoi : c.DichVu.GiaDV) * c.SoLuong);
            if (giaLe < g.GiaGoi) loi.Add($"\"{g.TenGoi}\" (giá gói {DichVuService.Vnd(g.GiaGoi)} > tổng giá lẻ mới {DichVuService.Vnd(giaLe)})");
        }
        if (loi.Count > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "GIA_MOI_LAM_SAI_GOI_DICH_VU",
                "Giá mới làm giá gói cao hơn tổng giá lẻ ở: " + string.Join("; ", loi) + ". Hãy giảm giá các gói này trước rồi mới đổi giá dịch vụ.", "giaDV");
    }

    private static (string Ten, string DonVi) ChuanHoaDichVu(ServiceRequest r)
    {
        var ten = r.TenDV?.Trim() ?? "";
        var donVi = r.DonViTinh?.Trim() ?? "";
        if (ten.Length == 0) throw new ApiException(StatusCodes.Status400BadRequest, "TEN_DICH_VU_TRONG", "Tên dịch vụ không được để trống.", "tenDV");
        if (donVi.Length == 0) throw new ApiException(StatusCodes.Status400BadRequest, "DON_VI_TINH_TRONG", "Đơn vị tính không được để trống (VD: lượt, chai, kg, suất).", "donViTinh");
        if (r.GiaDV <= 0) throw new ApiException(StatusCodes.Status400BadRequest, "GIA_DICH_VU_KHONG_HOP_LE", "Đơn giá phải lớn hơn 0.", "giaDV");
        if (r.GiaDV > 100_000_000) throw new ApiException(StatusCodes.Status400BadRequest, "GIA_DICH_VU_KHONG_HOP_LE", "Đơn giá tối đa 100.000.000đ.", "giaDV");
        return (ten, donVi);
    }

    private async Task KiemTraTrungTenAsync(string ten, string? boQuaMaDV)
    {
        if (await _context.DichVus.AnyAsync(x => x.TenDV == ten && x.MaDV != boQuaMaDV))
            throw new ApiException(StatusCodes.Status409Conflict, "TEN_DICH_VU_TRUNG", $"Đã có dịch vụ tên \"{ten}\".", "tenDV");
    }
}
