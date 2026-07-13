using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Application.Abstractions.Data;

public interface IInvoiceRepository
{
    Task<InvoiceReportModel?> GetInvoiceReportAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
