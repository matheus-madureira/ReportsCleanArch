namespace Reports.Domain.Entities;

public sealed class Invoice
{
    public Guid Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public IReadOnlyList<InvoiceItem> Items { get; init; } = [];

    public decimal Total => Items.Sum(i => i.Quantity * i.UnitPrice);
}
