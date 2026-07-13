USE ReportsDb;

GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  object_id = OBJECT_ID(N'dbo.InvoiceItems'))
    BEGIN
        CREATE TABLE dbo.InvoiceItems (
            Id          UNIQUEIDENTIFIER CONSTRAINT DF_InvoiceItems_Id DEFAULT NEWSEQUENTIALID() NOT NULL,
            InvoiceId   UNIQUEIDENTIFIER NOT NULL,
            LineNumber  INT              NOT NULL,
            Description NVARCHAR (300)   NOT NULL,
            Quantity    INT              NOT NULL,
            UnitPrice   DECIMAL (18, 2)  NOT NULL,
            CONSTRAINT PK_InvoiceItems PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices (Id) ON DELETE CASCADE,
            CONSTRAINT UQ_InvoiceItems_Invoice_Line UNIQUE (InvoiceId, LineNumber),
            CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0),
            CONSTRAINT CK_InvoiceItems_UnitPrice CHECK (UnitPrice >= 0)
        );
        CREATE NONCLUSTERED INDEX IX_InvoiceItems_InvoiceId
            ON dbo.InvoiceItems(InvoiceId, LineNumber)
            INCLUDE(Description, Quantity, UnitPrice);
    END
