USE ReportsDb;

GO
DECLARE @CustomerId AS UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @InvoiceId AS UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';

IF NOT EXISTS (SELECT 1
               FROM   dbo.Customers
               WHERE  Id = @CustomerId)
    BEGIN
        INSERT  INTO dbo.Customers (Id, Name, DocumentNumber, AddressLine, City, State, ZipCode)
        VALUES                    (@CustomerId, N'ACME Ltda', N'12345678000199', N'Av. Paulista, 1000 — Cj. 101', N'São Paulo', N'SP', N'01310-100');
    END

IF NOT EXISTS (SELECT 1
               FROM   dbo.Invoices
               WHERE  Id = @InvoiceId)
    BEGIN
        INSERT  INTO dbo.Invoices (Id, Number, CustomerId, IssueDate, Status)
        VALUES                   (@InvoiceId, N'INV-2026-000001', @CustomerId, '2026-07-01', 2);
        INSERT  INTO dbo.InvoiceItems (InvoiceId, LineNumber, Description, Quantity, UnitPrice)
        VALUES                       (@InvoiceId, 1, N'Licença de software — plano anual', 2, 1200.00),
        (@InvoiceId, 2, N'Horas de consultoria', 10, 350.00),
        (@InvoiceId, 3, N'Suporte premium (mensal)', 1, 499.90);
    END
