USE ReportsDb;

GO
IF TYPE_ID(N'dbo.InvoiceItemTableType') IS NULL
    BEGIN
        CREATE TYPE dbo.InvoiceItemTableType AS TABLE (
            LineNumber  INT             NOT NULL,
            Description NVARCHAR (300)  NOT NULL,
            Quantity    INT             NOT NULL,
            UnitPrice   DECIMAL (18, 2) NOT NULL);
    END


GO
CREATE OR ALTER PROCEDURE dbo.usp_Invoice_Create
@Id UNIQUEIDENTIFIER, @Number NVARCHAR (20), @CustomerId UNIQUEIDENTIFIER, @IssueDate DATE, @Items dbo.InvoiceItemTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    INSERT  INTO dbo.Invoices (Id, Number, CustomerId, IssueDate, Status)
    VALUES                   (@Id, @Number, @CustomerId, @IssueDate, 2);
    INSERT INTO dbo.InvoiceItems (InvoiceId, LineNumber, Description, Quantity, UnitPrice)
    SELECT @Id,
           LineNumber,
           Description,
           Quantity,
           UnitPrice
    FROM   @Items;
    COMMIT TRANSACTION;
END
