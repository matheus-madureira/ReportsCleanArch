USE ReportsDb;


GO
IF NOT EXISTS (SELECT 1
               FROM   sys.tables
               WHERE  object_id = OBJECT_ID(N'dbo.Customers'))
    BEGIN
        CREATE TABLE dbo.Customers (
            Id             UNIQUEIDENTIFIER CONSTRAINT DF_Customers_Id DEFAULT NEWSEQUENTIALID() NOT NULL,
            Name           NVARCHAR (200)   NOT NULL,
            DocumentNumber NVARCHAR (20)    NOT NULL,
            AddressLine    NVARCHAR (300)   NOT NULL,
            City           NVARCHAR (100)   NOT NULL,
            State          NCHAR (2)        NOT NULL,
            ZipCode        NVARCHAR (10)    NOT NULL,
            CreatedAtUtc   DATETIME2 (3)    CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT SYSUTCDATETIME() NOT NULL,
            CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_Customers_DocumentNumber UNIQUE (DocumentNumber)
        );
    END
