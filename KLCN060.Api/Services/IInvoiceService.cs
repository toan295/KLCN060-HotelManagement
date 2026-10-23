using KLCN060.Api.DTOs.Invoices;

namespace KLCN060.Api.Services;

public interface IInvoiceService
{
    Task<(List<InvoiceSummaryDto> Items, int TotalItems)> SearchAsync(InvoiceListQuery query);
    Task<InvoiceDto> GetAsync(string maHD, CurrentUser user);
    Task<InvoiceStatsDto> GetStatsAsync(InvoiceListQuery query);
    Task<InvoiceOverviewDto> GetOverviewAsync(string maHD, CurrentUser user);
    Task<InvoiceDto> AddPaymentAsync(string maHD, PaymentRequest request, CurrentUser user);
    Task<(byte[] Content, string FileName)> ExportPdfAsync(string maHD, CurrentUser user);
}
