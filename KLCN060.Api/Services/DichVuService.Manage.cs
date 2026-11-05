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

        // Người/lúc sửa gần nhất: lấy từ nhật ký (một truy vấn cho cả danh mục).
        var nhatKy = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => HanhDongDichVu.Contains(x.HanhDong) && x.DoiTuongTacDong != null && x.DoiTuongTacDong.StartsWith("DV"))
            .OrderByDescending(x => x.ThoiGian).Select(x => new { x.DoiTuongTacDong, x.ThoiGian, x.MaTaiKhoan }).Take(3000).ToListAsync();
        var suaCuoi = nhatKy.GroupBy(x => x.DoiTuongTacDong!).ToDictionary(g => g.Key, g => g.First());

        var items = dichVus.Select(d =>
        {
            dung.TryGetValue(d.MaDV, out var u);
            chiTiet.TryGetValue(d.MaDV, out var ct);
            var (xoaDuoc, lyDo) = LyDoKhongXoa(u?.Luot ?? 0);
            suaCuoi.TryGetValue(d.MaDV, out var s);
            return new ServiceCatalogItemDto
            {
                MaDV = d.MaDV, TenDV = d.TenDV, GiaDV = d.GiaDV, DonViTinh = d.DonViTinh, HoatDong = d.HoatDong, LoaiDoanhThu = d.LoaiDoanhThu.ToString(),
                SoLuotDung = u?.Luot ?? 0, TongSoLuong = u?.SoLuong ?? 0, DoanhThu = u?.DoanhThu ?? 0, LanDungCuoi = u?.Cuoi,
                SoMucChiTiet = ct?.Tong ?? 0, SoMucChiTietHienThi = ct?.HienThi ?? 0,
                CoTheXoa = xoaDuoc, LyDoKhongXoa = lyDo,
                NguoiSuaCuoi = s is null ? null : s.MaTaiKhoan, ThoiGianSuaCuoi = s?.ThoiGian
            };
        }).ToList();

        return new ServiceCatalogDto
        {
            CapNhatLuc = DateTime.Now,
            DichVus = items,
            TongHop = new ServiceCatalogSummaryDto
            {
                TongDichVu = items.Count, DangPhucVu = items.Count(x => x.HoatDong), NgungPhucVu = items.Count(x => !x.HoatDong),
                DichVuChuaCoChiTiet = items.Count(x => x.SoMucChiTietHienThi == 0), DichVuChuaTungDung = items.Count(x => x.SoLuotDung == 0),
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
            MoTaHanhDong = NhatKyMoTa.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();
    }

    private static (bool Duoc, string? LyDo) LyDoKhongXoa(int soLuot)
    {
        var ly = new List<string>();
        if (soLuot > 0) ly.Add($"đã được ghi cho khách {soLuot} lần (hóa đơn cũ còn dùng)");
        return ly.Count == 0 ? (true, null) : (false, "Không xóa được vì dịch vụ " + string.Join(", ", ly) + ". Hãy chuyển sang \"Ngừng phục vụ\" để giữ nguyên lịch sử.");
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
