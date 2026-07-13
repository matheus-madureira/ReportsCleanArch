USE ReportsDb;

GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoice_GetReport
@InvoiceId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.Id,
           i.Number,
           i.IssueDate,
           c.Name AS CustomerName,
           CONCAT(c.AddressLine, N' — ', c.City, N'/', c.State, N' — CEP ', c.ZipCode) AS CustomerAddress
    FROM   dbo.Invoices AS i
           INNER JOIN
           dbo.Customers AS c
           ON c.Id = i.CustomerId
    WHERE  i.Id = @InvoiceId;
    SELECT   it.Description,
             it.Quantity,
             it.UnitPrice
    FROM     dbo.InvoiceItems AS it
    WHERE    it.InvoiceId = @InvoiceId
    ORDER BY it.LineNumber;
END
