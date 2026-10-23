using KLCN060.Api.DTOs.Media;

namespace KLCN060.Api.Services;

public interface IRoomImageService
{
    List<RoomImageDto> GetAll();
    Task<RoomImageDto> SaveAsync(string slot, IFormFile? file);
    Task DeleteAsync(string slot);
}
