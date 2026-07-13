using NSubstitute;
using Reports.Application.Abstractions.Data;
using Reports.Application.Abstractions.Documents;
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Domain.Errors;

namespace Reports.Application.UnitTests;

public class GetInvoicePdfHandlerTests
{
    private static InvoiceReportModel SampleInvoice() => new(
        Id: Guid.NewGuid(),
        Number: "INV-001",
        IssueDate: new DateOnly(2026, 7, 13),
        CustomerName: "Acme Ltda",
        CustomerAddress: "Rua das Flores, 100",
        Items:
        [
            new InvoiceReportItem("Item A", Quantity: 2, UnitPrice: 10.00m),
        ]);

    [Fact]
    public async Task HandleAsync_ReturnsNotFound_WhenInvoiceDoesNotExist()
    {
        var repository = Substitute.For<IInvoiceRepository>();
        repository.GetInvoiceReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((InvoiceReportModel?)null);
        var pdfGenerator = Substitute.For<IPdfGenerator>();

        var handler = new GetInvoicePdfHandler(repository, pdfGenerator);

        var result = await handler.HandleAsync(new GetInvoicePdfQuery(Guid.NewGuid()), default);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Invoice.NotFound, result.Error);
        pdfGenerator.DidNotReceive().GenerateInvoicePdf(Arg.Any<InvoiceReportModel>());
    }

    [Fact]
    public async Task HandleAsync_ReturnsSuccessWithBytes_WhenInvoiceExists()
    {
        var invoice = SampleInvoice();
        var expectedPdf = new byte[] { 1, 2, 3, 4 };

        var repository = Substitute.For<IInvoiceRepository>();
        repository.GetInvoiceReportAsync(invoice.Id, Arg.Any<CancellationToken>())
            .Returns(invoice);
        var pdfGenerator = Substitute.For<IPdfGenerator>();
        pdfGenerator.GenerateInvoicePdf(invoice).Returns(expectedPdf);

        var handler = new GetInvoicePdfHandler(repository, pdfGenerator);

        var result = await handler.HandleAsync(new GetInvoicePdfQuery(invoice.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Same(expectedPdf, result.Value);
        pdfGenerator.Received(1).GenerateInvoicePdf(invoice);
    }
}
