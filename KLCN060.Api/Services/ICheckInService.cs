using KLCN060.Api.DTOs.Bookings;

namespace KLCN060.Api.Services;

public interface ICheckInService
{
    Task<CheckInResultDto> CheckInBookingAsync(string maPhieuDat, BookingCheckInRequest request);
    Task<CheckInResultDto> WalkInAsync(WalkInCheckInRequest request);
}
