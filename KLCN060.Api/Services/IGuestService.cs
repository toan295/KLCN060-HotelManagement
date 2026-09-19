using KLCN060.Api.DTOs.Guests;

namespace KLCN060.Api.Services;

public interface IGuestService
{
    Task<(List<GuestDto> Items, int TotalItems)> SearchAsync(string? q, int page, int pageSize);
    Task<GuestDetailDto> GetDetailAsync(string maKhach);
    Task<GuestDto> CreateAsync(GuestRequest request);
    Task<GuestDto> UpdateAsync(string maKhach, GuestRequest request);
    Task<GuestDto> GetMeAsync(string? maKhach);
    Task<GuestDto> UpdateMeAsync(string? maKhach, UpdateProfileRequest request);
}
