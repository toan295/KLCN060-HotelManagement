using KLCN060.Api.DTOs.Promotions;

namespace KLCN060.Api.Services;

public interface IPromotionService
{
    Task<List<PromotionDto>> GetActiveAsync();
    Task<List<PromotionDto>> GetAllAsync();
    Task<PromotionDto> CreateAsync(PromotionRequest request);
    Task<PromotionDto> UpdateAsync(string maKM, PromotionRequest request);
}
