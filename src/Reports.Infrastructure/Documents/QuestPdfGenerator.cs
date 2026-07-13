using QuestPDF.Fluent;
using Reports.Application.Abstractions.Documents;
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Infrastructure.Documents.Templates;

namespace Reports.Infrastructure.Documents;

public sealed class QuestPdfGenerator : IPdfGenerator
{
    public byte[] GenerateInvoicePdf(InvoiceReportModel model)
        => new InvoiceDocument(model).GeneratePdf();
}
