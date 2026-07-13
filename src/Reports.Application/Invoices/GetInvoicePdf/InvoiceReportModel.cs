namespace Reports.Application.Invoices.GetInvoicePdf;

public sealed record InvoiceReportModel(
    Guid Id,
    string Number,
    DateOnly IssueDate,
    string CustomerName,
    string CustomerAddress,
    IReadOnlyList<InvoiceReportItem> Items)
{
    public decimal Total => Items.Sum(i => i.LineTotal);
}

public sealed record InvoiceReportItem(string Description, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}
