using KLCN060.Api.DTOs.Facilities;

namespace KLCN060.Api.Services;

public interface IFacilityService
{
    Task<List<FacilityDto>> GetByRoomAsync(string maPhong);
    Task<FacilityDto> CreateForRoomAsync(string maPhong, FacilityRequest request);
    Task<FacilityDto> UpdateAsync(string maSo, FacilityRequest request);
    Task DeleteAsync(string maSo);
}
