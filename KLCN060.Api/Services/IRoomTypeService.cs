using KLCN060.Api.DTOs.RoomTypes;

namespace KLCN060.Api.Services;

public interface IRoomTypeService
{
    Task<List<RoomTypeDto>> GetAllAsync();
    Task<RoomTypeDto> GetByIdAsync(string maLoai);
    Task<RoomTypeDto> CreateAsync(RoomTypeRequest request);
    Task<RoomTypeDto> UpdateAsync(string maLoai, RoomTypeRequest request);
    Task DeleteAsync(string maLoai);
    Task<List<RoomTypeAvailabilityDto>> GetAvailabilityAsync(DateOnly checkin, DateOnly checkout, int guests);
}
