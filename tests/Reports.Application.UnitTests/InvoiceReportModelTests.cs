using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Application.UnitTests;

public class InvoiceReportModelTests
{
    [Fact]
    public void LineTotal_IsQuantityTimesUnitPrice()
    {
        var item = new InvoiceReportItem("Item A", Quantity: 3, UnitPrice: 5.50m);

        Assert.Equal(16.50m, item.LineTotal);
    }

    [Fact]
    public void Total_SumsLineTotals()
    {
        var model = new InvoiceReportModel(
            Id: Guid.NewGuid(),
            Number: "INV-001",
            IssueDate: new DateOnly(2026, 7, 13),
            CustomerName: "Acme Ltda",
            CustomerAddress: "Rua das Flores, 100",
            Items:
            [
                new InvoiceReportItem("Item A", Quantity: 2, UnitPrice: 10.00m),
                new InvoiceReportItem("Item B", Quantity: 3, UnitPrice: 5.50m),
            ]);

        // 2 * 10.00 + 3 * 5.50 = 20.00 + 16.50 = 36.50
        Assert.Equal(36.50m, model.Total);
    }

    [Fact]
    public void Total_OfModelWithoutItems_IsZero()
    {
        var model = new InvoiceReportModel(
            Id: Guid.NewGuid(),
            Number: "INV-000",
            IssueDate: new DateOnly(2026, 7, 13),
            CustomerName: "Acme Ltda",
            CustomerAddress: "Rua das Flores, 100",
            Items: []);

        Assert.Equal(0m, model.Total);
    }
}
