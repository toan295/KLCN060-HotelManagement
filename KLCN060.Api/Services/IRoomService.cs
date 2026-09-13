using KLCN060.Api.DTOs.Rooms;

namespace KLCN060.Api.Services;

public interface IRoomService
{
    Task<List<RoomDto>> GetAllAsync();
    Task<RoomDto> CreateAsync(RoomRequest request);
    Task<RoomDto> UpdateAsync(string maPhong, RoomRequest request);
    Task DeleteAsync(string maPhong);
    Task<RoomDto> UpdateStatusAsync(string maPhong, string tinhTrang);
}
