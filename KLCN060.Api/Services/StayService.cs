using KLCN060.Api.DTOs.Stays;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class StayService : IStayService
{
    private readonly KLCN060DbContext _context;

    public StayService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<StayServiceUsageDto> AddServiceAsync(string maPhieuNhan, string maPhong, AddStayServiceRequest request)
    {
        if (request.SoLuong < 1)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", "Số lượng phải lớn hơn 0.", "soLuong");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);

        var luotO = await TimLuotLuuTruAsync(maPhieuNhan, maPhong);
        if (luotO.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng, không thể thêm dịch vụ.");

        var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.MaDV == request.MaDV);
        if (dichVu is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ.", "maDV");

        // Chốt giá tại thời điểm gọi: ThanhTien lưu cứng, không đổi khi DichVu.GiaDV thay đổi về sau.
        var chiTiet = new ChiTietSuDungDV
        {
            MaPhieuNhan = maPhieuNhan,
            MaDV = dichVu.MaDV,
            SoLuong = request.SoLuong,
            NgaySuDung = DateTime.Now,
            ThanhTien = dichVu.GiaDV * request.SoLuong
        };

        _context.ChiTietSuDungDVs.Add(chiTiet);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(chiTiet, dichVu);
    }

    public async Task<List<StayServiceUsageDto>> GetServicesAsync(string maPhieuNhan, string maPhong)
    {
        await TimLuotLuuTruAsync(maPhieuNhan, maPhong);

        // ChiTietSuDungDV chỉ gắn với MaPhieuNhan (không có MaPhong) nên trả toàn bộ dịch vụ của phiếu nhận.
        var danhSach = await _context.ChiTietSuDungDVs
            .Include(x => x.DichVu)
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .OrderBy(x => x.NgaySuDung)
            .ToListAsync();

        return danhSach.Select(x => ToDto(x, x.DichVu)).ToList();
    }

    /// <summary>Khóa phiếu nhận tới hết transaction để thêm dịch vụ, đổi phòng và trả phòng của cùng 1 phiếu không chạy chồng nhau (tránh dịch vụ lọt khỏi hóa đơn).</summary>
    private async Task KhoaPhieuNhanAsync(string maPhieuNhan)
    {
        var tonTai = await _context.PhieuNhanPhongs
            .FromSql($"SELECT * FROM PhieuNhanPhongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhieuNhan = {maPhieuNhan}")
            .AsNoTracking()
            .AnyAsync();
        if (!tonTai)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
    }

    private async Task<ChiTietPhieuNhan> TimLuotLuuTruAsync(string maPhieuNhan, string maPhong)
    {
        var luotO = await _context.ChiTietPhieuNhans.FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong);
        if (luotO is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        return luotO;
    }

    private static StayServiceUsageDto ToDto(ChiTietSuDungDV x, DichVu dichVu) => new()
    {
        MaChiTietDV = x.MaChiTietDV,
        MaPhieuNhan = x.MaPhieuNhan,
        MaDV = x.MaDV,
        TenDV = dichVu.TenDV,
        DonViTinh = dichVu.DonViTinh,
        SoLuong = x.SoLuong,
        NgaySuDung = x.NgaySuDung,
        ThanhTien = x.ThanhTien
    };
}
