using KLCN060.Api.DTOs.Rooms;

namespace KLCN060.Api.Services;

public interface IRoomService
{
    Task<List<RoomDto>> GetAllAsync();
    Task<RoomCatalogDto> GetCatalogAsync(RoomCatalogQuery query);
    Task<BulkRoomResultDto> CreateBulkAsync(BulkRoomRequest request);
    Task<RoomDto> CreateAsync(RoomRequest request);
    Task<RoomDto> UpdateAsync(string maPhong, RoomRequest request, string? tenDN = null);
    Task DeleteAsync(string maPhong);
    Task<RoomDto> UpdateStatusAsync(string maPhong, string tinhTrang, string? ghiChu, string tenDN, string? tinhTrangCu = null);
    Task<RoomDetailDto> GetDetailAsync(string maPhong);
    Task<List<RoomStatusHistoryDto>> GetHistoryAsync(string maPhong);
}
