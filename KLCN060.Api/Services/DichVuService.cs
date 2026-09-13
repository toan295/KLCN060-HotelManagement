using KLCN060.Api.DTOs.Services;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class DichVuService : IDichVuService
{
    private readonly KLCN060DbContext _context;

    public DichVuService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<ServiceDto>> GetAllAsync()
    {
        return await _context.DichVus
            .OrderBy(x => x.MaDV)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<ServiceDto> CreateAsync(ServiceRequest request)
    {
        var maHienCo = await _context.DichVus.Select(x => x.MaDV).ToListAsync();
        var maDV = MaCodeGenerator.GenerateNext(maHienCo, "DV", 2);

        var dichVu = new DichVu
        {
            MaDV = maDV,
            TenDV = request.TenDV,
            GiaDV = request.GiaDV,
            DonViTinh = request.DonViTinh
        };

        _context.DichVus.Add(dichVu);
        await _context.SaveChangesAsync();

        return ToDto(dichVu);
    }

    public async Task<ServiceDto> UpdateAsync(string maDV, ServiceRequest request)
    {
        var dichVu = await TimDichVuAsync(maDV);

        dichVu.TenDV = request.TenDV;
        dichVu.GiaDV = request.GiaDV;
        dichVu.DonViTinh = request.DonViTinh;

        await _context.SaveChangesAsync();
        return ToDto(dichVu);
    }

    public async Task DeleteAsync(string maDV)
    {
        var dichVu = await TimDichVuAsync(maDV);
        _context.DichVus.Remove(dichVu);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "DICH_VU_DANG_DUOC_SU_DUNG", "Không thể xóa dịch vụ vì đã có lượt sử dụng liên quan.");
        }
    }

    private async Task<DichVu> TimDichVuAsync(string maDV)
    {
        var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.MaDV == maDV);
        if (dichVu is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ.");
        return dichVu;
    }

    private static ServiceDto ToDto(DichVu x) => new()
    {
        MaDV = x.MaDV,
        TenDV = x.TenDV,
        GiaDV = x.GiaDV,
        DonViTinh = x.DonViTinh
    };
}
