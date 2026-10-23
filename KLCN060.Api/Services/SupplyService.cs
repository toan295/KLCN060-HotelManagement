using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Đọc giá trị enum từ chuỗi client gửi: chỉ nhận đúng tên (không nhận số), sai thì 400 kèm gợi ý.</summary>
public static class EnumParser
{
    /// <summary>Như <see cref="Parse{T}"/> nhưng không ném lỗi: chỉ nhận đúng tên (không phân biệt hoa thường), không nhận chuỗi số ("2").</summary>
    public static bool TryParse<T>(string? giaTri, out T ketQua) where T : struct, Enum
    {
        ketQua = default;
        var s = giaTri?.Trim() ?? "";
        return s.Length > 0 && !int.TryParse(s, out _) && Enum.TryParse(s, ignoreCase: true, out ketQua) && Enum.IsDefined(ketQua);
    }

    public static T Parse<T>(string? giaTri, string truong, string thongDiep) where T : struct, Enum
    {
        var s = giaTri?.Trim() ?? "";
        if (s.Length == 0 || int.TryParse(s, out _) || !Enum.TryParse<T>(s, ignoreCase: true, out var kq) || !Enum.IsDefined(kq))
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", thongDiep, truong);
        return kq;
    }
}

public interface ISupplyService
{
    Task<List<SupplyDto>> GetAllAsync(bool chiHoatDong);
    Task<List<SupplyRoomTypeDto>> GetRoomTypesAsync();
    Task<SupplyDto> CreateAsync(SupplyRequest request);
    Task<SupplyDto> UpdateAsync(string maVatDung, SupplyRequest request);
    Task DeleteAsync(string maVatDung);
}

/// <summary>Danh mục đồ dùng trong phòng (khăn, ga, gối, nước, minibar, amenities...) - quản lý (QUAN_LY_PHONG) thêm/sửa; buồng phòng chỉ xem để kiểm phòng.</summary>
public class SupplyService : ISupplyService
{
    private const int SoLuongToiDa = 1000;
    private const string LoiNhom = "Nhóm không hợp lệ (KHAN_GA_GOI, AMENITIES, NUOC_UONG, MINIBAR, KHAC).";

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public SupplyService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<SupplyDto>> GetAllAsync(bool chiHoatDong)
    {
        var q = _context.VatDungs.AsNoTracking().AsQueryable();
        if (chiHoatDong) q = q.Where(x => x.HoatDong);
        var dong = await q.Include(x => x.LoaiPhongApDungs)
            .Select(x => new { VatDung = x, TenDV = x.DichVu != null ? x.DichVu.TenDV : null, Gia = x.DichVu != null ? (decimal?)x.DichVu.GiaDV : null }).ToListAsync();
        var loais = await _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.MaLoai).ToListAsync();
        // Sắp theo mã (VT01, VT02, ... VT10) - mã được cấp theo thứ tự thêm nên danh mục đọc từ trên xuống luôn khớp mã.
        return dong.OrderBy(x => x.VatDung.MaVatDung.Length).ThenBy(x => x.VatDung.MaVatDung, StringComparer.Ordinal)
            .Select(x => ToDto(x.VatDung, x.TenDV, x.Gia, loais)).ToList();
    }

    public async Task<List<SupplyRoomTypeDto>> GetRoomTypesAsync()
        => (await _context.LoaiPhongs.AsNoTracking().OrderBy(x => x.MaLoai).ToListAsync())
            .Select(x => new SupplyRoomTypeDto { MaLoai = x.MaLoai, TenLoai = x.TenLoai, LoaiGiuong = x.LoaiGiuong, SoGiuong = x.SoGiuong, SoNguoiTieuChuan = x.SoNguoiTieuChuan }).ToList();

    /// <summary>Kiểm tra cách tính + phạm vi loại phòng của yêu cầu; trả về (cách tính, danh sách mã loại phòng không trùng).</summary>
    private async Task<(CachTinhDinhMuc Cach, List<string> Loais)> KiemTraPhamViAsync(SupplyRequest request)
    {
        var cach = string.IsNullOrWhiteSpace(request.CachTinh)
            ? CachTinhDinhMuc.MOI_PHONG
            : EnumParser.Parse<CachTinhDinhMuc>(request.CachTinh, "cachTinh", "Cách tính định mức không hợp lệ (MOI_PHONG, MOI_GIUONG, MOI_NGUOI).");
        var loais = (request.MaLoaiPhongs ?? new List<string>()).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (loais.Count > 0)
        {
            var coThat = (await _context.LoaiPhongs.Select(x => x.MaLoai).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var la = loais.FirstOrDefault(x => !coThat.Contains(x));
            if (la is not null)
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", $"Không tìm thấy loại phòng {la}.", "maLoaiPhongs");
            loais = (await _context.LoaiPhongs.Where(x => loais.Contains(x.MaLoai)).Select(x => x.MaLoai).ToListAsync());
            // Chọn đủ mọi loại phòng thì coi như "áp dụng mọi loại" (loại phòng thêm sau cũng có đồ dùng này).
            if (loais.Count == coThat.Count) loais = new List<string>();
        }
        return (cach, loais);
    }

    public async Task<SupplyDto> CreateAsync(SupplyRequest request)
    {
        var (ten, nhom, donVi) = await KiemTraAsync(request, null);
        var (cachTinh, loaiPhongs) = await KiemTraPhamViAsync(request);
        var ma = MaCodeGenerator.GenerateNext(await _context.VatDungs.Select(x => x.MaVatDung).ToListAsync(), "VT", 2);
        var vatDung = new VatDung
        {
            MaVatDung = ma, TenVatDung = ten, Nhom = nhom, DonViTinh = donVi, SoLuongChuan = request.SoLuongChuan, CachTinh = cachTinh,
            LoaiPhongApDungs = loaiPhongs.Select(l => new VatDungLoaiPhong { MaVatDung = ma, MaLoai = l }).ToList(),
            MaDV = string.IsNullOrWhiteSpace(request.MaDV) ? null : request.MaDV.Trim(), HoatDong = request.HoatDong
        };
        _context.VatDungs.Add(vatDung);
        _audit.Ghi("THEM_VAT_DUNG", ma, $"ten={ten}; nhom={nhom}; dinhMuc={request.SoLuongChuan}/{cachTinh}; loaiPhong={(loaiPhongs.Count == 0 ? "tat ca" : string.Join(",", loaiPhongs))}; dichVu={vatDung.MaDV ?? "khong"}");
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "VAT_DUNG_DA_TON_TAI", "Đồ dùng này vừa được thêm bởi người khác (trùng tên hoặc mã).", "tenVatDung");
        }
        await DongBoNhomDoanhThuAsync(vatDung);
        return await LayAsync(ma);
    }

    public async Task<SupplyDto> UpdateAsync(string maVatDung, SupplyRequest request)
    {
        var vatDung = await _context.VatDungs.Include(x => x.LoaiPhongApDungs).FirstOrDefaultAsync(x => x.MaVatDung == maVatDung)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAT_DUNG", "Không tìm thấy đồ dùng.");
        var (ten, nhom, donVi) = await KiemTraAsync(request, maVatDung);
        var (cachTinh, loaiPhongs) = await KiemTraPhamViAsync(request);

        var phamViTruoc = vatDung.LoaiPhongApDungs.Count == 0 ? "tat ca" : string.Join(",", vatDung.LoaiPhongApDungs.Select(x => x.MaLoai).OrderBy(x => x));
        var truoc = $"ten={vatDung.TenVatDung}; nhom={vatDung.Nhom}; dinhMuc={vatDung.SoLuongChuan}/{vatDung.CachTinh}; loaiPhong={phamViTruoc}; dichVu={vatDung.MaDV ?? "khong"}; hoatDong={vatDung.HoatDong}";
        vatDung.TenVatDung = ten;
        vatDung.Nhom = nhom;
        vatDung.DonViTinh = donVi;
        vatDung.SoLuongChuan = request.SoLuongChuan;
        vatDung.CachTinh = cachTinh;
        _context.VatDungLoaiPhongs.RemoveRange(vatDung.LoaiPhongApDungs);
        foreach (var l in loaiPhongs) _context.VatDungLoaiPhongs.Add(new VatDungLoaiPhong { MaVatDung = maVatDung, MaLoai = l });
        vatDung.MaDV = string.IsNullOrWhiteSpace(request.MaDV) ? null : request.MaDV.Trim();
        vatDung.HoatDong = request.HoatDong;
        _audit.Ghi("SUA_VAT_DUNG", maVatDung, $"truoc: {truoc} | sau: ten={ten}; nhom={nhom}; dinhMuc={request.SoLuongChuan}/{cachTinh}; loaiPhong={(loaiPhongs.Count == 0 ? "tat ca" : string.Join(",", loaiPhongs.OrderBy(x => x)))}; dichVu={vatDung.MaDV ?? "khong"}; hoatDong={request.HoatDong}");
        await _context.SaveChangesAsync();
        await DongBoNhomDoanhThuAsync(vatDung);
        return await LayAsync(maVatDung);
    }

    /// <summary>
    /// Đồ minibar gắn với dịch vụ nào thì doanh thu của dịch vụ đó là "minibar" trong báo cáo kế toán (chỉ nâng dịch vụ đang ở nhóm chung DICH_VU lên MINIBAR;
    /// không ghi đè nhóm khác mà quản lý đã chọn ở danh mục dịch vụ).
    /// </summary>
    private async Task DongBoNhomDoanhThuAsync(VatDung vatDung)
    {
        if (vatDung.Nhom != NhomVatDung.MINIBAR || vatDung.MaDV is null) return;
        var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.MaDV == vatDung.MaDV);
        if (dichVu is null || dichVu.LoaiDoanhThu != LoaiDoanhThuDichVu.DICH_VU) return;
        dichVu.LoaiDoanhThu = LoaiDoanhThuDichVu.MINIBAR;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string maVatDung)
    {
        var vatDung = await _context.VatDungs.FirstOrDefaultAsync(x => x.MaVatDung == maVatDung)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAT_DUNG", "Không tìm thấy đồ dùng.");
        if (await _context.ChiTietKiemPhongs.AnyAsync(x => x.MaVatDung == maVatDung))
            throw new ApiException(StatusCodes.Status409Conflict, "VAT_DUNG_DA_CO_LICH_SU_KIEM",
                "Đồ dùng đã có lịch sử kiểm phòng nên không xóa được. Hãy tắt \"đang sử dụng\" để buồng phòng không kiểm nữa mà vẫn giữ lịch sử.");
        _context.VatDungs.Remove(vatDung);
        _audit.Ghi("XOA_VAT_DUNG", maVatDung, $"ten={vatDung.TenVatDung}");
        await _context.SaveChangesAsync();
    }

    private async Task<(string Ten, NhomVatDung Nhom, string DonVi)> KiemTraAsync(SupplyRequest request, string? maHienTai)
    {
        var ten = request.TenVatDung.Trim();
        var donVi = request.DonViTinh.Trim();
        if (ten.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_VAT_DUNG", "Chưa nhập tên đồ dùng.", "tenVatDung");
        if (donVi.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_DON_VI_TINH", "Chưa nhập đơn vị tính.", "donViTinh");
        var nhom = EnumParser.Parse<NhomVatDung>(request.Nhom, "nhom", LoiNhom);
        if (request.SoLuongChuan < 0 || request.SoLuongChuan > SoLuongToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", $"Định mức mỗi phòng phải từ 0 đến {SoLuongToiDa}.", "soLuongChuan");

        var tenThuong = ten.ToLower();
        if (await _context.VatDungs.AnyAsync(x => x.TenVatDung.ToLower() == tenThuong && x.MaVatDung != maHienTai))
            throw new ApiException(StatusCodes.Status409Conflict, "VAT_DUNG_DA_TON_TAI", $"Đã có đồ dùng tên \"{ten}\".", "tenVatDung");

        if (!string.IsNullOrWhiteSpace(request.MaDV))
        {
            var maDV = request.MaDV.Trim();
            if (!await _context.DichVus.AnyAsync(x => x.MaDV == maDV))
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ dùng để tính phí.", "maDV");
        }
        return (ten, nhom, donVi);
    }

    private async Task<SupplyDto> LayAsync(string ma)
        => (await GetAllAsync(false)).First(x => x.MaVatDung == ma);

    private static SupplyDto ToDto(VatDung x, string? tenDV, decimal? gia, List<LoaiPhong> loais) => new()
    {
        MaVatDung = x.MaVatDung, TenVatDung = x.TenVatDung, Nhom = x.Nhom.ToString(), DonViTinh = x.DonViTinh, SoLuongChuan = x.SoLuongChuan,
        CachTinh = x.CachTinh.ToString(),
        MaLoaiPhongs = x.LoaiPhongApDungs.Select(l => l.MaLoai).OrderBy(l => l, StringComparer.Ordinal).ToList(),
        DinhMucTheoLoai = loais.Where(l => x.ApDungChoLoai(l.MaLoai)).Select(l => new SupplyRoomTypeQuantityDto
            { MaLoai = l.MaLoai, TenLoai = l.TenLoai, SoGiuong = l.SoGiuong, SoNguoiTieuChuan = l.SoNguoiTieuChuan, DinhMuc = x.DinhMucCho(l) }).ToList(),
        MaDV = x.MaDV, TenDV = tenDV, GiaTinhPhi = gia, HoatDong = x.HoatDong
    };
}
