using KLCN060.Api.DTOs.Invoices;

namespace KLCN060.Api.Services;

public interface IInvoiceService
{
    Task<InvoiceDto> GetAsync(string maHD, CurrentUser user);
    Task<InvoiceDto> AddPaymentAsync(string maHD, PaymentRequest request, CurrentUser user);
    Task<(byte[] Content, string FileName)> ExportPdfAsync(string maHD, CurrentUser user);
}
