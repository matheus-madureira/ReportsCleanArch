USE ReportsDb;

GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoice_List
@Page INT=1, @PageSize INT=20, @DateFrom DATE=NULL, @DateTo DATE=NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Page < 1
        SET @Page = 1;
    IF @PageSize < 1
       OR @PageSize > 100
        SET @PageSize = 20;
    SELECT   i.Id,
             i.Number,
             i.IssueDate,
             i.Status,
             c.Name AS CustomerName,
             ISNULL(t.Total, 0) AS Total,
             COUNT(*) OVER () AS TotalCount
    FROM     dbo.Invoices AS i
             INNER JOIN
             dbo.Customers AS c
             ON c.Id = i.CustomerId OUTER APPLY (SELECT SUM(it.Quantity * it.UnitPrice) AS Total
                                                 FROM   dbo.InvoiceItems AS it
                                                 WHERE  it.InvoiceId = i.Id) AS t
    WHERE    (@DateFrom IS NULL
              OR i.IssueDate >= @DateFrom)
             AND (@DateTo IS NULL
                  OR i.IssueDate <= @DateTo)
    ORDER BY i.IssueDate DESC, i.Number DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
