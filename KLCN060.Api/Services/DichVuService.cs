using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class DichVuService : IDichVuService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public DichVuService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<ServiceDto>> GetAllAsync(bool gomNgungPhucVu = false)
    {
        var dichVus = await _context.DichVus.AsNoTracking().Where(x => gomNgungPhucVu || x.HoatDong).OrderBy(x => x.MaDV).ToListAsync();
        var chiTiets = await _context.ChiTietDichVus.AsNoTracking()
            .Where(x => x.ConHienThi)
            .OrderBy(x => x.ThuTu).ThenBy(x => x.MaCTDV)
            .ToListAsync();

        return dichVus.Select(x => ToDto(x, chiTiets.Where(c => c.MaDV == x.MaDV))).ToList();
    }

    public async Task<ServiceDto> CreateAsync(ServiceRequest request)
    {
        var (ten, donVi) = ChuanHoaDichVu(request);
        await KiemTraTrungTenAsync(ten, null);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // [Khối 8 - P1-4] Transaction + khóa sinh mã theo tiền tố (xem MaCodeGenerator.GenerateNextAsync): 2 request tạo
        // đồng thời không còn sinh trùng mã rồi bị từ chối oan bằng 409 XUNG_DOT_DU_LIEU.
        var maDV = await MaCodeGenerator.GenerateNextAsync(_context, _context.DichVus.Select(x => x.MaDV), "DV", 2);

        var dichVu = new DichVu
        {
            MaDV = maDV,
            TenDV = ten,
            GiaDV = request.GiaDV,
            DonViTinh = donVi,
            HoatDong = request.HoatDong ?? true,
            LoaiDoanhThu = string.IsNullOrWhiteSpace(request.LoaiDoanhThu) ? LoaiDoanhThuDichVu.DICH_VU : ParseLoaiDoanhThu(request.LoaiDoanhThu)
        };

        _context.DichVus.Add(dichVu);
        _audit.Ghi("TAO_DICH_VU", maDV, $"ten={dichVu.TenDV}; gia={Math.Round(dichVu.GiaDV, 0)}; donVi={dichVu.DonViTinh}; phucVu={dichVu.HoatDong}; nhomDoanhThu={dichVu.LoaiDoanhThu}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo dịch vụ, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return ToDto(dichVu);
    }

    public async Task<ServiceDto> UpdateAsync(string maDV, ServiceRequest request)
    {
        var (ten, donVi) = ChuanHoaDichVu(request);
        var dichVu = await TimDichVuAsync(maDV);
        await KiemTraTrungTenAsync(ten, maDV);
        if (dichVu.GiaDV != request.GiaDV) await KiemTraGiaMoiVoiGoiAsync(maDV, request.GiaDV);

        // Nhật ký riêng cho đổi giá và đổi trạng thái phục vụ (mục 7 của Quản lý: giá dịch vụ, trạng thái dịch vụ).
        if (dichVu.GiaDV != request.GiaDV)
            _audit.Ghi("DOI_GIA_DICH_VU", maDV, $"giaCu={Math.Round(dichVu.GiaDV, 0)}; giaMoi={Math.Round(request.GiaDV, 0)}; ten={dichVu.TenDV}");
        if (request.HoatDong is { } phucVu && phucVu != dichVu.HoatDong)
            _audit.Ghi("DOI_TRANG_THAI_DICH_VU", maDV, $"{(dichVu.HoatDong ? "PHUC_VU" : "NGUNG")} -> {(phucVu ? "PHUC_VU" : "NGUNG")}; ten={dichVu.TenDV}");
        var loaiDoanhThuMoi = string.IsNullOrWhiteSpace(request.LoaiDoanhThu) ? dichVu.LoaiDoanhThu : ParseLoaiDoanhThu(request.LoaiDoanhThu);
        if (loaiDoanhThuMoi != dichVu.LoaiDoanhThu)
            _audit.Ghi("DOI_NHOM_DOANH_THU_DICH_VU", maDV, $"{dichVu.LoaiDoanhThu} -> {loaiDoanhThuMoi}; ten={dichVu.TenDV}");
        _audit.Ghi("SUA_DICH_VU", maDV, $"ten: {dichVu.TenDV} -> {ten}; donVi: {dichVu.DonViTinh} -> {donVi}");
        dichVu.LoaiDoanhThu = loaiDoanhThuMoi;

        dichVu.TenDV = ten;
        dichVu.GiaDV = request.GiaDV;
        dichVu.DonViTinh = donVi;
        if (request.HoatDong is { } hd) dichVu.HoatDong = hd;

        await _context.SaveChangesAsync();
        return ToDto(dichVu, await LayChiTietHienThiAsync(maDV));
    }

    public async Task DeleteAsync(string maDV)
    {
        var dichVu = await TimDichVuAsync(maDV);
        var catalog = (await GetCatalogAsync()).DichVus.First(x => x.MaDV == maDV);
        if (!catalog.CoTheXoa)
            throw new ApiException(StatusCodes.Status409Conflict, "DICH_VU_DANG_DUOC_SU_DUNG", catalog.LyDoKhongXoa!);
        _context.DichVus.Remove(dichVu);
        _audit.Ghi("XOA_DICH_VU", maDV, $"ten={dichVu.TenDV}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "DICH_VU_DANG_DUOC_SU_DUNG", "Không thể xóa dịch vụ vì đã có lượt sử dụng hoặc đang nằm trong một gói dịch vụ - hãy chuyển sang trạng thái ngừng phục vụ.");
        }
    }

    // ---- Chi tiết dịch vụ ----

    public async Task<List<ServiceDetailDto>> GetDetailsAsync(string maDV)
    {
        await TimDichVuAsync(maDV);
        return (await _context.ChiTietDichVus.AsNoTracking()
                .Where(x => x.MaDV == maDV)
                .OrderBy(x => x.ThuTu).ThenBy(x => x.MaCTDV)
                .ToListAsync())
            .Select(ToDetailDto).ToList();
    }

    public async Task<ServiceDetailDto> CreateDetailAsync(string maDV, ServiceDetailRequest request)
    {
        var (tieuDe, noiDung) = ChuanHoa(request);
        await TimDichVuAsync(maDV);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var maCTDV = await MaCodeGenerator.GenerateNextAsync(_context, _context.ChiTietDichVus.Select(x => x.MaCTDV), "CT", 3);
        await KiemTraTrungTieuDeAsync(maDV, tieuDe, null);

        var chiTiet = new ChiTietDichVu
        {
            MaCTDV = maCTDV,
            MaDV = maDV,
            TieuDe = tieuDe,
            NoiDung = noiDung,
            ThuTu = request.ThuTu,
            ConHienThi = request.ConHienThi
        };
        _context.ChiTietDichVus.Add(chiTiet);
        _audit.Ghi("TAO_CHI_TIET_DICH_VU", maDV, $"muc={maCTDV}; tieuDe={tieuDe}; thuTu={request.ThuTu}; hienThi={request.ConHienThi}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi thêm chi tiết dịch vụ, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return ToDetailDto(chiTiet);
    }

    public async Task<ServiceDetailDto> UpdateDetailAsync(string maDV, string maCTDV, ServiceDetailRequest request)
    {
        var (tieuDe, noiDung) = ChuanHoa(request);
        var chiTiet = await TimChiTietAsync(maDV, maCTDV);
        await KiemTraTrungTieuDeAsync(maDV, tieuDe, maCTDV);

        _audit.Ghi("SUA_CHI_TIET_DICH_VU", maDV, $"muc={maCTDV}; tieuDe: {chiTiet.TieuDe} -> {tieuDe}; noiDungDoi={chiTiet.NoiDung != noiDung}; hienThi: {chiTiet.ConHienThi} -> {request.ConHienThi}; thuTu: {chiTiet.ThuTu} -> {request.ThuTu}");
        chiTiet.TieuDe = tieuDe;
        chiTiet.NoiDung = noiDung;
        chiTiet.ThuTu = request.ThuTu;
        chiTiet.ConHienThi = request.ConHienThi;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "TIEU_DE_CHI_TIET_TRUNG", "Dịch vụ đã có một mục chi tiết cùng tiêu đề.", "tieuDe");
        }
        return ToDetailDto(chiTiet);
    }

    public async Task DeleteDetailAsync(string maDV, string maCTDV)
    {
        var chiTiet = await TimChiTietAsync(maDV, maCTDV);
        _context.ChiTietDichVus.Remove(chiTiet);
        _audit.Ghi("XOA_CHI_TIET_DICH_VU", maDV, $"muc={maCTDV}; tieuDe={chiTiet.TieuDe}");
        await _context.SaveChangesAsync();
    }

    private static (string TieuDe, string NoiDung) ChuanHoa(ServiceDetailRequest request)
    {
        var tieuDe = request.TieuDe?.Trim() ?? "";
        var noiDung = request.NoiDung?.Trim() ?? "";
        if (tieuDe.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "TIEU_DE_TRONG", "Tiêu đề chi tiết không được để trống.", "tieuDe");
        if (noiDung.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "NOI_DUNG_TRONG", "Nội dung chi tiết không được để trống.", "noiDung");
        return (tieuDe, noiDung);
    }

    private async Task KiemTraTrungTieuDeAsync(string maDV, string tieuDe, string? boQuaMaCTDV)
    {
        var trung = await _context.ChiTietDichVus.AnyAsync(x => x.MaDV == maDV && x.TieuDe == tieuDe && x.MaCTDV != boQuaMaCTDV);
        if (trung)
            throw new ApiException(StatusCodes.Status409Conflict, "TIEU_DE_CHI_TIET_TRUNG", "Dịch vụ đã có một mục chi tiết cùng tiêu đề.", "tieuDe");
    }

    private async Task<ChiTietDichVu> TimChiTietAsync(string maDV, string maCTDV)
        => await _context.ChiTietDichVus.FirstOrDefaultAsync(x => x.MaDV == maDV && x.MaCTDV == maCTDV)
           ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_CHI_TIET_DICH_VU", "Không tìm thấy chi tiết dịch vụ.");

    private async Task<List<ChiTietDichVu>> LayChiTietHienThiAsync(string maDV)
        => await _context.ChiTietDichVus.AsNoTracking()
            .Where(x => x.MaDV == maDV && x.ConHienThi)
            .OrderBy(x => x.ThuTu).ThenBy(x => x.MaCTDV)
            .ToListAsync();

    private async Task<DichVu> TimDichVuAsync(string maDV)
    {
        var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.MaDV == maDV);
        if (dichVu is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ.");
        return dichVu;
    }

    private static ServiceDetailDto ToDetailDto(ChiTietDichVu x) => new()
    {
        MaCTDV = x.MaCTDV,
        MaDV = x.MaDV,
        TieuDe = x.TieuDe,
        NoiDung = x.NoiDung,
        ThuTu = x.ThuTu,
        ConHienThi = x.ConHienThi
    };

    private static LoaiDoanhThuDichVu ParseLoaiDoanhThu(string giaTri)
    {
        var s = giaTri.Trim();
        if (int.TryParse(s, out _) || !Enum.TryParse<LoaiDoanhThuDichVu>(s, ignoreCase: true, out var kq) || !Enum.IsDefined(kq))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DOANH_THU_KHONG_HOP_LE", "LoaiDoanhThu chỉ nhận: DICH_VU, MINIBAR, GIAT_LA, KHAC.", "loaiDoanhThu");
        return kq;
    }

    private static ServiceDto ToDto(DichVu x, IEnumerable<ChiTietDichVu>? chiTiets = null) => new()
    {
        MaDV = x.MaDV,
        TenDV = x.TenDV,
        GiaDV = x.GiaDV,
        DonViTinh = x.DonViTinh,
        HoatDong = x.HoatDong,
        LoaiDoanhThu = x.LoaiDoanhThu.ToString(),
        ChiTiets = (chiTiets ?? Enumerable.Empty<ChiTietDichVu>()).Select(ToDetailDto).ToList()
    };
}
