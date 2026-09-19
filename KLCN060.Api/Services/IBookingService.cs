using KLCN060.Api.DTOs.Bookings;

namespace KLCN060.Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CurrentUser user);
    Task<(List<BookingSummaryDto> Items, int TotalItems)> SearchAsync(BookingListQuery query);
    Task<List<BookingSummaryDto>> GetMineAsync(string? maKhach);
    Task<BookingDto> GetByIdAsync(string maPhieuDat, CurrentUser user);
    Task<BookingDto> ConfirmDepositAsync(string maPhieuDat, CurrentUser user);
    Task<CancelBookingResultDto> CancelAsync(string maPhieuDat, CancelBookingRequest request, CurrentUser user);
}
