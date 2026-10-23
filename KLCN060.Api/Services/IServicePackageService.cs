using KLCN060.Api.DTOs.Services;

namespace KLCN060.Api.Services;

public interface IServicePackageService
{
    /// <summary>Gói đang bán; <paramref name="gomNgungBan"/> = true (quyền QUAN_LY_DICH_VU) thì gồm cả gói đã tắt.</summary>
    Task<List<ServicePackageDto>> GetAllAsync(bool gomNgungBan);
    Task<ServicePackageDto> CreateAsync(ServicePackageRequest request, string tenDN);
    Task<ServicePackageDto> UpdateAsync(string maGoi, ServicePackageRequest request);
    Task DeleteAsync(string maGoi);
    Task<List<ServiceHistoryEntryDto>> GetHistoryAsync(string maGoi);
}
