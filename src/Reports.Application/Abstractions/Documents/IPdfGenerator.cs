using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Application.Abstractions.Documents;

public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportModel model);
}
