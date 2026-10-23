using KLCN060.Api.DTOs.Services;

namespace KLCN060.Api.Services;

public interface IDichVuService
{
    /// <summary>Danh sách dịch vụ; <paramref name="gomNgungPhucVu"/> = true (chỉ dành cho người quản lý dịch vụ) thì gồm cả dịch vụ đã ngừng phục vụ.</summary>
    Task<List<ServiceDto>> GetAllAsync(bool gomNgungPhucVu = false);
    Task<ServiceDto> CreateAsync(ServiceRequest request);
    Task<ServiceDto> UpdateAsync(string maDV, ServiceRequest request);
    Task DeleteAsync(string maDV);

    Task<ServiceCatalogDto> GetCatalogAsync();
    Task<List<ServiceHistoryEntryDto>> GetHistoryAsync(string ma);

    // Chi tiết dịch vụ (bảng ChiTietDichVus)
    Task<List<ServiceDetailDto>> GetDetailsAsync(string maDV);
    Task<ServiceDetailDto> CreateDetailAsync(string maDV, ServiceDetailRequest request);
    Task<ServiceDetailDto> UpdateDetailAsync(string maDV, string maCTDV, ServiceDetailRequest request);
    Task DeleteDetailAsync(string maDV, string maCTDV);
}
