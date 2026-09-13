using KLCN060.Api.DTOs.Services;

namespace KLCN060.Api.Services;

public interface IDichVuService
{
    Task<List<ServiceDto>> GetAllAsync();
    Task<ServiceDto> CreateAsync(ServiceRequest request);
    Task<ServiceDto> UpdateAsync(string maDV, ServiceRequest request);
    Task DeleteAsync(string maDV);
}
