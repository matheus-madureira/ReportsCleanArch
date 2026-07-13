using Reports.Application.Abstractions.Data;
using Reports.Application.Abstractions.Documents;
using Reports.Application.Common;
using Reports.Domain.Errors;

namespace Reports.Application.Invoices.GetInvoicePdf;

public sealed class GetInvoicePdfHandler(IInvoiceRepository repository, IPdfGenerator pdfGenerator)
{
    public async Task<Result<byte[]>> HandleAsync(
        GetInvoicePdfQuery query,
        CancellationToken cancellationToken)
    {
        var invoice = await repository.GetInvoiceReportAsync(query.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result<byte[]>.Failure(DomainErrors.Invoice.NotFound);
        }

        var pdf = pdfGenerator.GenerateInvoicePdf(invoice);

        return Result<byte[]>.Success(pdf);
    }
}
