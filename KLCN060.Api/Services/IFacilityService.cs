using KLCN060.Api.DTOs.Facilities;

namespace KLCN060.Api.Services;

public interface IFacilityService
{
    Task<List<FacilityDto>> GetByRoomAsync(string maPhong);
    Task<FacilityDto> CreateForRoomAsync(string maPhong, FacilityRequest request);
    /// <summary>Thêm thiết bị vào phòng (<c>request.MaPhong</c>) hoặc vào kho/dùng chung khi bỏ trống.</summary>
    Task<FacilityDto> CreateAsync(FacilityRequest request);
    Task<FacilityDto> UpdateAsync(string maSo, FacilityRequest request);
    Task DeleteAsync(string maSo, string? lyDo = null);
    /// <summary>Điều chuyển thiết bị (toàn bộ hoặc một phần số lượng) sang phòng khác / về kho, bắt buộc có lý do.</summary>
    Task<FacilityDto> TransferAsync(string maSo, FacilityTransferRequest request);
    Task<FacilityOverviewDto> GetOverviewAsync();
    Task<List<FacilityHistoryEntryDto>> GetHistoryAsync(string maSo);
}
