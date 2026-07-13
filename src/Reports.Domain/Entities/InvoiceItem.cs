namespace Reports.Domain.Entities;

public sealed record InvoiceItem(string Description, int Quantity, decimal UnitPrice);
