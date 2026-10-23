using KLCN060.Api.DTOs.Bookings;

namespace KLCN060.Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CurrentUser user);
    Task<BookingQuoteDto> QuoteAsync(CreateBookingRequest request);
    Task<(List<BookingSummaryDto> Items, int TotalItems)> SearchAsync(BookingListQuery query);
    Task<List<BookingSummaryDto>> GetMineAsync(string? maKhach);
    Task<BookingDto> GetByIdAsync(string maPhieuDat, CurrentUser user);
    Task<BookingOverviewDto> GetOverviewAsync(string maPhieuDat, CurrentUser user);
    Task<BookingDto> ConfirmDepositAsync(string maPhieuDat, CurrentUser user);
    Task<CancelBookingResultDto> CancelAsync(string maPhieuDat, CancelBookingRequest request, CurrentUser user);
    Task<BookingDto> UpdateAsync(string maPhieuDat, UpdateBookingRequest request, CurrentUser user);
    Task<BookingChangePreviewDto> PreviewUpdateAsync(string maPhieuDat, UpdateBookingRequest request, CurrentUser user);
    Task<List<KLCN060.Api.DTOs.FrontDesk.VacantRoomDto>> GetSwapRoomsAsync(string maPhieuDat, string maPhongCu, DateOnly? nhan, DateOnly? tra);
    Task<BookingDto> MarkNoShowAsync(string maPhieuDat, NoShowRequest request, CurrentUser user);
    Task<List<CompanionDto>> GetCompanionsAsync(string maPhieuDat, CurrentUser user);
    Task<CompanionDto> AddCompanionAsync(string maPhieuDat, AddCompanionRequest request, CurrentUser user);
    Task RemoveCompanionAsync(string maPhieuDat, int maKhachDiCung, CurrentUser user);
}
