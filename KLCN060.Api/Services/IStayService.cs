using KLCN060.Api.DTOs.Stays;

namespace KLCN060.Api.Services;

public interface IStayService
{
    Task<List<StaySummaryDto>> SearchAsync(string? maPhong, string? trangThai);
    Task<StayServiceUsageDto> AddServiceAsync(string maPhieuNhan, string maPhong, AddStayServiceRequest request);
    Task<List<StayServiceUsageDto>> GetServicesAsync(string maPhieuNhan, string maPhong);
    Task<ChangeRoomResultDto> ChangeRoomAsync(string maPhieuNhan, string maPhong, ChangeRoomRequest request);
    Task<CheckOutResultDto> CheckOutAsync(string maPhieuNhan, string maPhong, CheckOutRequest request, CurrentUser user);
    Task<FolioDto> GetFolioAsync(string maPhieuNhan, string maPhong, CurrentUser user);
}
