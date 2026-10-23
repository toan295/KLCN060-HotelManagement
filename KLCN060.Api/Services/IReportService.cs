using KLCN060.Api.DTOs.Reports;

namespace KLCN060.Api.Services;

public interface IReportService
{
    Task<RevenueReportDto> RevenueAsync(DateOnly from, DateOnly to);
    Task<OccupancyReportDto> OccupancyAsync(DateOnly from, DateOnly to);
    Task<GuestReportDto> GuestsAsync(DateOnly from, DateOnly to);
}
