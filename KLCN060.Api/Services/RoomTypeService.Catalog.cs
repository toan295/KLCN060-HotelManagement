using KLCN060.Api.DTOs.RoomTypes;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Kiểm tra dữ liệu loại phòng và số liệu vận hành cho màn quản lý danh mục.</summary>
public partial class RoomTypeService
{
    private const decimal DonGiaToiDa = 100_000_000m;
    private const decimal PhuThuToiDa = 50_000_000m;

    /// <summary>Chuẩn hóa (cắt khoảng trắng, tiện nghi gọn dấu phẩy) và kiểm tra: tên bắt buộc + không trùng, sức chứa, đơn giá, phụ thu, diện tích, độ dài mô tả.</summary>
    private async Task ChuanHoaVaKiemTraAsync(RoomTypeRequest request, string? maLoaiBoQua)
    {
        request.TenLoai = (request.TenLoai ?? "").Trim();
        if (request.TenLoai.Length < 2)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_LOAI", "Tên loại phòng bắt buộc, tối thiểu 2 ký tự.", "tenLoai");
        if (request.SoNguoiTieuChuan is < 1 or > 20)
            throw new ApiException(StatusCodes.Status400BadRequest, "SUC_CHUA_KHONG_HOP_LE", "Sức chứa phải từ 1 đến 20 người.", "soNguoiTieuChuan");
        if (request.DonGia <= 0 || request.DonGia > DonGiaToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "DON_GIA_KHONG_HOP_LE", $"Đơn giá phải lớn hơn 0 và không quá {DonGiaToiDa:N0}đ.", "donGia");
        if (request.PhuThu < 0 || request.PhuThu > PhuThuToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "PHU_THU_KHONG_HOP_LE", $"Phụ thu từ 0 đến {PhuThuToiDa:N0}đ.", "phuThu");
        if (request.DienTich is { } dt && (dt < 5 || dt > 1000))
            throw new ApiException(StatusCodes.Status400BadRequest, "DIEN_TICH_KHONG_HOP_LE", "Diện tích phải từ 5 đến 1000 m².", "dienTich");

        request.LoaiGiuong = string.IsNullOrWhiteSpace(request.LoaiGiuong) ? null : request.LoaiGiuong.Trim();
        request.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
        var tienNghi = (request.TienNghi ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (tienNghi.Any(x => x.Length > 50))
            throw new ApiException(StatusCodes.Status400BadRequest, "TIEN_NGHI_KHONG_HOP_LE", "Mỗi tiện nghi tối đa 50 ký tự.", "tienNghi");
        request.TienNghi = tienNghi.Count == 0 ? null : string.Join(", ", tienNghi);
        if (request.TienNghi is { Length: > 500 })
            throw new ApiException(StatusCodes.Status400BadRequest, "TIEN_NGHI_KHONG_HOP_LE", "Danh sách tiện nghi tối đa 500 ký tự.", "tienNghi");

        if (await _context.LoaiPhongs.AnyAsync(x => EF.Functions.Collate(x.TenLoai, "Latin1_General_100_CI_AI") == request.TenLoai && x.MaLoai != maLoaiBoQua))
            throw new ApiException(StatusCodes.Status409Conflict, "TEN_LOAI_DA_TON_TAI", "Đã có loại phòng mang tên này.", "tenLoai");
    }

    private async Task<(bool Duoc, string? LyDo)> CoTheXoaLoaiAsync(string maLoai)
    {
        var soPhong = await _context.Phongs.CountAsync(x => x.MaLoai == maLoai);
        if (soPhong > 0)
            return (false, $"Còn {soPhong} phòng thuộc loại này - hãy chuyển các phòng sang loại khác (hoặc xóa phòng) trước. Loại phòng không bao giờ bị xóa khi còn phòng để giữ nguyên lịch sử và giá.");
        var soQuyTac = await _context.QuyTacGias.CountAsync(x => x.MaLoai == maLoai);
        if (soQuyTac > 0)
            return (false, $"Còn {soQuyTac} quy tắc giá gắn với loại phòng này - hãy xử lý ở màn hình Bảng giá phòng trước.");
        // Khuyến mãi trỏ MaLoaiApDung không có khóa ngoại: xóa loại phòng sẽ để lại khuyến mãi "mồ côi".
        var tenKm = await _context.KhuyenMais.AsNoTracking().Where(x => x.MaLoaiApDung == maLoai).OrderBy(x => x.MaKM).Select(x => x.TenKM).Take(3).ToListAsync();
        if (tenKm.Count > 0)
            return (false, $"Loại phòng đang là phạm vi áp dụng của khuyến mãi: {string.Join(", ", tenKm)} - hãy sửa hoặc xóa khuyến mãi đó trước.");
        return (true, null);
    }

    public async Task<List<RoomTypeCatalogDto>> GetCatalogAsync()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var loais = await _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.MaLoai).ToListAsync();
        var phongs = await _context.Phongs.AsNoTracking().Select(p => new { p.MaPhong, p.MaLoai, p.TinhTrang }).ToListAsync();
        var datTuongLai = (await _context.ChiTietPhieuDats.AsNoTracking()
                .Where(c => (c.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN || c.PhieuDatPhong.TrangThai == TrangThaiPhieuDat.DA_XAC_NHAN) && c.PhieuDatPhong.NgayTraDuKien >= homNay)
                .Select(c => new { c.Phong.MaLoai, c.MaPhieuDat }).ToListAsync())
            .GroupBy(x => x.MaLoai).ToDictionary(g => g.Key, g => g.Select(x => x.MaPhieuDat).Distinct().Count());
        var quyTac = (await _context.QuyTacGias.AsNoTracking().Where(x => x.MaLoai != null).Select(x => x.MaLoai!).ToListAsync())
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var kq = new List<RoomTypeCatalogDto>();
        foreach (var l in loais)
        {
            var cuaLoai = phongs.Where(p => p.MaLoai == l.MaLoai).ToList();
            var soQuyTac = quyTac.GetValueOrDefault(l.MaLoai);
            var lyDo = cuaLoai.Count > 0
                ? $"Còn {cuaLoai.Count} phòng thuộc loại này - chuyển hoặc xóa các phòng trước."
                : soQuyTac > 0 ? $"Còn {soQuyTac} quy tắc giá gắn với loại phòng này." : null;
            kq.Add(new RoomTypeCatalogDto
            {
                MaLoai = l.MaLoai, TenLoai = l.TenLoai, SoNguoiTieuChuan = l.SoNguoiTieuChuan, DonGia = l.DonGia, PhuThu = l.PhuThu,
                DienTich = l.DienTich, LoaiGiuong = l.LoaiGiuong, TienNghi = l.TienNghi, MoTa = l.MoTa,
                SoPhong = cuaLoai.Count, SoPhongBaoTri = cuaLoai.Count(p => p.TinhTrang == TinhTrangPhong.OOO),
                SoPhongCoKhach = cuaLoai.Count(p => p.TinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD),
                SoDatPhongTuongLai = datTuongLai.GetValueOrDefault(l.MaLoai), SoQuyTacGia = soQuyTac,
                CoTheXoa = lyDo is null, LyDoKhongXoa = lyDo
            });
        }
        return kq;
    }
}
