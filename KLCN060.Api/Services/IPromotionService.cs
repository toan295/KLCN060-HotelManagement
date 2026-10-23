using KLCN060.Api.DTOs.Promotions;

namespace KLCN060.Api.Services;

public interface IPromotionService
{
    Task<List<PromotionDto>> GetActiveAsync();
    Task<List<PromotionDto>> GetAllAsync();
    Task<PromotionDto> CreateAsync(PromotionRequest request);
    Task<PromotionDto> UpdateAsync(string maKM, PromotionRequest request);
    Task<PromotionDto> GetByIdAsync(string maKM);
    Task<List<PromotionUsageDto>> GetUsageAsync(string maKM);
    Task<PromotionCheckDto> KiemTraBookingAsync(string maKM, string maPhieuDat);
    Task<PromotionDto> TamNgungAsync(string maKM, string lyDo);
    Task<PromotionDto> TiepTucAsync(string maKM);
    Task DeleteAsync(string maKM);
}
