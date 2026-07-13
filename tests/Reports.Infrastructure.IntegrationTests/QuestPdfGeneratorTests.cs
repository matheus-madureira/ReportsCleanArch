using QuestPDF.Infrastructure;
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Infrastructure.Documents;

namespace Reports.Infrastructure.IntegrationTests;

public sealed class QuestPdfGeneratorTests
{
    public QuestPdfGeneratorTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void GenerateInvoicePdf_produz_bytes_com_header_PDF()
    {
        var model = new InvoiceReportModel(
            Guid.NewGuid(),
            "INV-2026-000042",
            new DateOnly(2026, 7, 13),
            "ACME Ltda",
            "Av. Paulista, 1000 — São Paulo/SP",
            [
                new InvoiceReportItem("Consultoria", 3, 250.00m),
                new InvoiceReportItem("Licença", 1, 1200.00m)
            ]);

        var pdf = new QuestPdfGenerator().GenerateInvoicePdf(model);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);

        // Assinatura de um arquivo PDF: "%PDF"
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, pdf[..4]);
    }
}
