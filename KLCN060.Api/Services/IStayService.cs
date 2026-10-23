using KLCN060.Api.DTOs.Stays;

namespace KLCN060.Api.Services;

public interface IStayService
{
    Task<List<StaySummaryDto>> SearchAsync(string? maPhong, string? trangThai);
    Task<StayServiceUsageDto> AddServiceAsync(string maPhieuNhan, string maPhong, AddStayServiceRequest request);
    Task<List<StayServiceUsageDto>> AddServicePackageAsync(string maPhieuNhan, string maPhong, AddStayServicePackageRequest request);
    Task<StayServiceUsageDto> VoidServiceAsync(string maPhieuNhan, string maPhong, int maChiTietDV, string? lyDo);
    Task<List<StayServiceUsageDto>> GetServicesAsync(string maPhieuNhan, string maPhong);
    /// <summary>404 nếu không có lượt lưu trú (phiếu nhận + phòng) này.</summary>
    Task KiemTraLuotTonTaiAsync(string maPhieuNhan, string maPhong);
    Task<ChangeRoomResultDto> ChangeRoomAsync(string maPhieuNhan, string maPhong, ChangeRoomRequest request);
    Task<ChangeRoomPreviewDto> XemTruocDoiPhongAsync(string maPhieuNhan, string maPhong, string? maPhongMoi);
    Task<ExtendPreviewDto> XemTruocGiaHanAsync(string maPhieuNhan, string maPhong, DateOnly ngayTraMoi);
    Task<ExtendStayResultDto> ExtendAsync(string maPhieuNhan, string maPhong, ExtendStayRequest request);
    Task<CheckOutInfoDto> XemTruocTraPhongAsync(string maPhieuNhan, string maPhong, bool apDungPhuThuTreGio);
    Task<CheckOutResultDto> CheckOutAsync(string maPhieuNhan, string maPhong, CheckOutRequest request, CurrentUser user);
    Task<FolioDto> GetFolioAsync(string maPhieuNhan, string maPhong, CurrentUser user);
}
