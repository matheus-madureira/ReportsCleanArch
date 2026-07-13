using Reports.Domain.Entities;

namespace Reports.Domain.UnitTests;

public class InvoiceTests
{
    [Fact]
    public void Total_SumsQuantityTimesUnitPrice_ForEachItem()
    {
        var invoice = new Invoice
        {
            Items =
            [
                new InvoiceItem("Item A", Quantity: 2, UnitPrice: 10.00m),
                new InvoiceItem("Item B", Quantity: 3, UnitPrice: 5.50m),
            ],
        };

        // 2 * 10.00 + 3 * 5.50 = 20.00 + 16.50 = 36.50
        Assert.Equal(36.50m, invoice.Total);
    }

    [Fact]
    public void Total_OfInvoiceWithoutItems_IsZero()
    {
        var invoice = new Invoice();

        Assert.Equal(0m, invoice.Total);
    }
}
