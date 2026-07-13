USE ReportsDb;

GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  object_id = OBJECT_ID(N'dbo.Invoices'))
    BEGIN
        CREATE TABLE dbo.Invoices (
            Id           UNIQUEIDENTIFIER CONSTRAINT DF_Invoices_Id DEFAULT NEWSEQUENTIALID() NOT NULL,
            Number       NVARCHAR (20)    NOT NULL,
            CustomerId   UNIQUEIDENTIFIER NOT NULL,
            IssueDate    DATE             NOT NULL,
            Status       TINYINT          CONSTRAINT DF_Invoices_Status DEFAULT 1 NOT NULL,
            CreatedAtUtc DATETIME2 (3)    CONSTRAINT DF_Invoices_CreatedAtUtc DEFAULT SYSUTCDATETIME() NOT NULL,
            CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_Invoices_Number UNIQUE (Number),
            CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
            CONSTRAINT CK_Invoices_Status CHECK (Status IN (1, 2, 3))
        );
        CREATE NONCLUSTERED INDEX IX_Invoices_CustomerId
            ON dbo.Invoices(CustomerId)
            INCLUDE(Number, IssueDate, Status);
        CREATE NONCLUSTERED INDEX IX_Invoices_IssueDate
            ON dbo.Invoices(IssueDate)
            INCLUDE(Number, CustomerId, Status);
    END
