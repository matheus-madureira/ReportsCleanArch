# Plano de Implementação — Geração de PDFs com Clean Architecture

**Stack:** .NET 10 · Dapper · Stored Procedures · SQL Server (local) · QuestPDF
**Objetivo:** API que gera relatórios/documentos em PDF (ex.: fatura de pedido), com dados vindos de procedures via Dapper e renderização via QuestPDF.

Este plano segue as convenções mais difundidas na comunidade .NET para Clean Architecture — em especial os templates e conteúdos de **Jason Taylor** (Clean Architecture Solution Template), **Steve "Ardalis" Smith** (Clean Architecture template + regra de dependência), **Milan Jovanović** (Clean Architecture aplicada de forma pragmática, CQRS, Result pattern) e os princípios originais de **Robert C. Martin (Uncle Bob)**.

> **O que mudou nesta versão:**
> 1. Scripts SQL completos (tabelas, procedures, seeds) versionados dentro de `Infrastructure/Persistence/Database`.
> 2. Explicação detalhada do papel de cada camada — o que entra, o que **não** entra e por quê.
> 3. Guia passo a passo de **como implementar uma nova feature**, com exemplo concreto do início ao fim, para onboarding do time.

---

## 1. Princípios norteadores (consenso da comunidade)

1. **Regra de Dependência (Uncle Bob):** dependências apontam sempre para dentro. `Domain` não conhece ninguém; `Application` conhece só o `Domain`; `Infrastructure` e `API` conhecem as camadas internas — nunca o contrário.
2. **Abstrações no centro, detalhes na borda (Ardalis):** Dapper, SQL Server e QuestPDF são *detalhes de implementação*. Eles vivem na `Infrastructure` atrás de interfaces declaradas na `Application`.
3. **Use Cases explícitos (Jason Taylor / Milan Jovanović):** cada operação de negócio é um caso de uso (ex.: `GetInvoicePdfQuery`), com handler próprio — CQRS via MediatR ou handlers manuais.
4. **Pragmatismo com Dapper (Milan Jovanović):** ao usar Dapper + procedures, não há "repositório genérico" nem ORM rastreando entidades. Os repositórios expõem métodos específicos e retornam modelos de leitura — perfeito para cenários de relatório (read-heavy).
5. **QuestPDF é um "renderer", não regra de negócio:** o documento (layout, fontes, tabelas) é um adaptador de saída. A `Application` pede "gere o PDF da fatura X" e recebe `byte[]`/`Stream` — sem saber que QuestPDF existe.
6. **O banco também é código:** scripts `.sql` versionados junto do projeto, numerados e idempotentes (`CREATE OR ALTER`, `IF NOT EXISTS`), para que qualquer dev suba o ambiente local do zero com um comando.

---

## 2. Estrutura da solução

```text
src/
├── Reports.Domain/                 # Entidades, Value Objects, regras invariantes
│   ├── Entities/
│   │   ├── Invoice.cs
│   │   └── InvoiceItem.cs
│   ├── ValueObjects/
│   │   └── Money.cs
│   └── Errors/
│       └── DomainErrors.cs
│
├── Reports.Application/            # Casos de uso, interfaces (ports), DTOs
│   ├── Abstractions/
│   │   ├── Data/
│   │   │   ├── IDbConnectionFactory.cs
│   │   │   └── IInvoiceRepository.cs
│   │   └── Documents/
│   │       └── IPdfGenerator.cs          # port de saída p/ PDF
│   ├── Invoices/
│   │   └── GetInvoicePdf/
│   │       ├── GetInvoicePdfQuery.cs
│   │       ├── GetInvoicePdfHandler.cs
│   │       └── InvoiceReportModel.cs     # modelo de leitura do relatório
│   └── Common/
│       └── Result.cs                     # Result pattern (Milan Jovanović)
│
├── Reports.Infrastructure/         # Dapper, procedures, QuestPDF, scripts SQL
│   ├── Persistence/
│   │   ├── Database/                     # ★ TODOS os .sql vivem aqui ★
│   │   │   ├── 00_Setup/
│   │   │   │   └── 000_CreateDatabase.sql
│   │   │   ├── 01_Tables/
│   │   │   │   ├── 001_Customers.sql
│   │   │   │   ├── 002_Invoices.sql
│   │   │   │   └── 003_InvoiceItems.sql
│   │   │   ├── 02_Procedures/
│   │   │   │   ├── 101_usp_Invoice_GetReport.sql
│   │   │   │   ├── 102_usp_Invoice_List.sql
│   │   │   │   └── 103_usp_Invoice_Create.sql
│   │   │   ├── 03_Seeds/
│   │   │   │   └── 201_SeedDevData.sql
│   │   │   └── deploy-local.ps1          # aplica tudo em ordem via sqlcmd
│   │   ├── SqlConnectionFactory.cs
│   │   └── Repositories/
│   │       └── InvoiceRepository.cs      # chama as procedures via Dapper
│   ├── Documents/
│   │   ├── QuestPdfGenerator.cs          # implementa IPdfGenerator
│   │   └── Templates/
│   │       ├── InvoiceDocument.cs        # IDocument do QuestPDF
│   │       └── Components/
│   │           └── AddressComponent.cs   # IComponent reutilizável
│   └── DependencyInjection.cs
│
├── Reports.Api/                    # Minimal API / Controllers
│   ├── Endpoints/
│   │   └── InvoiceEndpoints.cs
│   ├── Program.cs
│   └── appsettings.json
│
tests/
├── Reports.Application.UnitTests/
├── Reports.Infrastructure.IntegrationTests/
└── Reports.Api.FunctionalTests/
```

**Grafo de dependências:**

```text
Api ──────────► Application ──► Domain
 │                   ▲
 └──► Infrastructure ┘   (Infrastructure referencia Application e Domain)
```

**Convenção de numeração dos scripts:** `0xx` setup, `1xx`... dentro de cada pasta a ordem numérica é a ordem de execução. Pastas executam em ordem alfabética (`00_Setup` → `01_Tables` → `02_Procedures` → `03_Seeds`). Scripts de tabela usam guarda `IF NOT EXISTS`; procedures usam `CREATE OR ALTER` — ou seja, **rodar tudo de novo é sempre seguro** (idempotência).

Para que os `.sql` acompanhem o build (útil para testes de integração com Testcontainers), adicione ao `Reports.Infrastructure.csproj`:

```xml
<ItemGroup>
  <None Include="Persistence\Database\**\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## 3. Banco de dados — SQL Server local

### 3.1 Modelo de dados

```text
Customers 1 ──── N Invoices 1 ──── N InvoiceItems
```

| Tabela | Papel |
|---|---|
| `Customers` | Cliente da fatura (nome, endereço, documento). |
| `Invoices` | Cabeçalho da fatura (número, data de emissão, status, FK para cliente). |
| `InvoiceItems` | Linhas da fatura (descrição, quantidade, preço unitário, ordem de exibição). |

Decisões de schema:
- **PKs `UNIQUEIDENTIFIER`** geradas pela aplicação (ou `NEWSEQUENTIALID()` como default) — combinam com o `Guid Id` do Domain e evitam round-trip para obter identity.
- **`Number` da fatura é `UNIQUE`** — invariante de negócio garantida também no banco.
- **`LineNumber`** em `InvoiceItems` para ordenar as linhas do PDF de forma determinística.
- **`DECIMAL(18,2)`** para valores monetários — nunca `FLOAT`.
- **`SYSUTCDATETIME()`** para auditoria (`CreatedAtUtc`) — sempre UTC no banco, conversão de fuso é responsabilidade da borda (API/relatório).

### 3.2 `00_Setup/000_CreateDatabase.sql`

```sql
-- Cria o banco local de desenvolvimento, se não existir.
-- Execute conectado ao master.
IF DB_ID(N'ReportsDb') IS NULL
BEGIN
    CREATE DATABASE ReportsDb;
END
GO

ALTER DATABASE ReportsDb SET RECOVERY SIMPLE; -- dev local: log enxuto
GO
```

### 3.3 `01_Tables/001_Customers.sql`

```sql
USE ReportsDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Customers'))
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id            UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Customers_Id DEFAULT NEWSEQUENTIALID(),
        Name          NVARCHAR(200)    NOT NULL,
        DocumentNumber NVARCHAR(20)    NOT NULL,   -- CPF/CNPJ sem máscara
        AddressLine   NVARCHAR(300)    NOT NULL,
        City          NVARCHAR(100)    NOT NULL,
        State         NCHAR(2)         NOT NULL,
        ZipCode       NVARCHAR(10)     NOT NULL,
        CreatedAtUtc  DATETIME2(3)     NOT NULL
            CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Customers_DocumentNumber UNIQUE (DocumentNumber)
    );
END
GO
```

### 3.4 `01_Tables/002_Invoices.sql`

```sql
USE ReportsDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Invoices'))
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id           UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Invoices_Id DEFAULT NEWSEQUENTIALID(),
        Number       NVARCHAR(20)     NOT NULL,    -- ex.: 'INV-2026-000123'
        CustomerId   UNIQUEIDENTIFIER NOT NULL,
        IssueDate    DATE             NOT NULL,
        Status       TINYINT          NOT NULL     -- 1=Draft, 2=Issued, 3=Cancelled
            CONSTRAINT DF_Invoices_Status DEFAULT 1,
        CreatedAtUtc DATETIME2(3)     NOT NULL
            CONSTRAINT DF_Invoices_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Invoices_Number UNIQUE (Number),
        CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId)
            REFERENCES dbo.Customers (Id),
        CONSTRAINT CK_Invoices_Status CHECK (Status IN (1, 2, 3))
    );

    -- Consultas por cliente e por período são as mais comuns em relatório.
    CREATE NONCLUSTERED INDEX IX_Invoices_CustomerId
        ON dbo.Invoices (CustomerId) INCLUDE (Number, IssueDate, Status);

    CREATE NONCLUSTERED INDEX IX_Invoices_IssueDate
        ON dbo.Invoices (IssueDate) INCLUDE (Number, CustomerId, Status);
END
GO
```

### 3.5 `01_Tables/003_InvoiceItems.sql`

```sql
USE ReportsDb;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.InvoiceItems'))
BEGIN
    CREATE TABLE dbo.InvoiceItems
    (
        Id          UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_InvoiceItems_Id DEFAULT NEWSEQUENTIALID(),
        InvoiceId   UNIQUEIDENTIFIER NOT NULL,
        LineNumber  INT              NOT NULL,      -- ordem de exibição no PDF
        Description NVARCHAR(300)    NOT NULL,
        Quantity    INT              NOT NULL,
        UnitPrice   DECIMAL(18, 2)   NOT NULL,

        CONSTRAINT PK_InvoiceItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId)
            REFERENCES dbo.Invoices (Id) ON DELETE CASCADE,
        CONSTRAINT UQ_InvoiceItems_Invoice_Line UNIQUE (InvoiceId, LineNumber),
        CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_InvoiceItems_UnitPrice CHECK (UnitPrice >= 0)
    );

    -- Cobre exatamente o SELECT da procedure de relatório (evita key lookup).
    CREATE NONCLUSTERED INDEX IX_InvoiceItems_InvoiceId
        ON dbo.InvoiceItems (InvoiceId, LineNumber)
        INCLUDE (Description, Quantity, UnitPrice);
END
GO
```

### 3.6 `02_Procedures/101_usp_Invoice_GetReport.sql`

Procedure principal do caso de uso de PDF: **dois resultsets em uma ida ao banco** (cabeçalho + itens), consumidos com `QueryMultipleAsync` no Dapper — evita N+1.

```sql
USE ReportsDb;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_GetReport
    @InvoiceId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: cabeçalho da fatura + dados do cliente
    SELECT
        i.Id,
        i.Number,
        i.IssueDate,
        c.Name                                          AS CustomerName,
        CONCAT(c.AddressLine, N' — ', c.City, N'/', c.State,
               N' — CEP ', c.ZipCode)                   AS CustomerAddress
    FROM dbo.Invoices i
    INNER JOIN dbo.Customers c ON c.Id = i.CustomerId
    WHERE i.Id = @InvoiceId;

    -- Resultset 2: itens ordenados
    SELECT
        it.Description,
        it.Quantity,
        it.UnitPrice
    FROM dbo.InvoiceItems it
    WHERE it.InvoiceId = @InvoiceId
    ORDER BY it.LineNumber;
END
GO
```

### 3.7 `02_Procedures/102_usp_Invoice_List.sql`

Listagem paginada (útil para uma tela/endpoint de consulta antes de gerar o PDF).

```sql
USE ReportsDb;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_List
    @Page     INT = 1,
    @PageSize INT = 20,
    @DateFrom DATE = NULL,
    @DateTo   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 OR @PageSize > 100 SET @PageSize = 20;

    SELECT
        i.Id,
        i.Number,
        i.IssueDate,
        i.Status,
        c.Name AS CustomerName,
        ISNULL(t.Total, 0) AS Total,
        COUNT(*) OVER ()   AS TotalCount   -- total p/ paginação em 1 query
    FROM dbo.Invoices i
    INNER JOIN dbo.Customers c ON c.Id = i.CustomerId
    OUTER APPLY (
        SELECT SUM(it.Quantity * it.UnitPrice) AS Total
        FROM dbo.InvoiceItems it
        WHERE it.InvoiceId = i.Id
    ) t
    WHERE (@DateFrom IS NULL OR i.IssueDate >= @DateFrom)
      AND (@DateTo   IS NULL OR i.IssueDate <= @DateTo)
    ORDER BY i.IssueDate DESC, i.Number DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
```

### 3.8 `02_Procedures/103_usp_Invoice_Create.sql`

Escrita transacional com TVP (Table-Valued Parameter) para inserir cabeçalho + itens atomicamente.

```sql
USE ReportsDb;
GO

-- TVP para os itens
IF TYPE_ID(N'dbo.InvoiceItemTableType') IS NULL
BEGIN
    CREATE TYPE dbo.InvoiceItemTableType AS TABLE
    (
        LineNumber  INT            NOT NULL,
        Description NVARCHAR(300)  NOT NULL,
        Quantity    INT            NOT NULL,
        UnitPrice   DECIMAL(18, 2) NOT NULL
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_Create
    @Id         UNIQUEIDENTIFIER,
    @Number     NVARCHAR(20),
    @CustomerId UNIQUEIDENTIFIER,
    @IssueDate  DATE,
    @Items      dbo.InvoiceItemTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;  -- rollback automático em qualquer erro

    BEGIN TRANSACTION;

    INSERT INTO dbo.Invoices (Id, Number, CustomerId, IssueDate, Status)
    VALUES (@Id, @Number, @CustomerId, @IssueDate, 2 /* Issued */);

    INSERT INTO dbo.InvoiceItems (InvoiceId, LineNumber, Description, Quantity, UnitPrice)
    SELECT @Id, LineNumber, Description, Quantity, UnitPrice
    FROM @Items;

    COMMIT TRANSACTION;
END
GO
```

### 3.9 `03_Seeds/201_SeedDevData.sql`

Dados de exemplo para desenvolvimento — idempotente (só insere se ainda não existir).

```sql
USE ReportsDb;
GO

DECLARE @CustomerId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @InvoiceId  UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId)
BEGIN
    INSERT INTO dbo.Customers (Id, Name, DocumentNumber, AddressLine, City, State, ZipCode)
    VALUES (@CustomerId, N'ACME Ltda', N'12345678000199',
            N'Av. Paulista, 1000 — Cj. 101', N'São Paulo', N'SP', N'01310-100');
END

IF NOT EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = @InvoiceId)
BEGIN
    INSERT INTO dbo.Invoices (Id, Number, CustomerId, IssueDate, Status)
    VALUES (@InvoiceId, N'INV-2026-000001', @CustomerId, '2026-07-01', 2);

    INSERT INTO dbo.InvoiceItems (InvoiceId, LineNumber, Description, Quantity, UnitPrice)
    VALUES
        (@InvoiceId, 1, N'Licença de software — plano anual', 2, 1200.00),
        (@InvoiceId, 2, N'Horas de consultoria',              10,  350.00),
        (@InvoiceId, 3, N'Suporte premium (mensal)',           1,  499.90);
END
GO
```

### 3.10 `deploy-local.ps1` — aplicar tudo em ordem

Script único que qualquer dev roda para subir o banco local do zero (requer `sqlcmd`, incluso no SQL Server ou via *Microsoft Command Line Utilities*):

```powershell
# Infrastructure/Persistence/Database/deploy-local.ps1
# Uso:  .\deploy-local.ps1                          (instância padrão, auth do Windows)
#       .\deploy-local.ps1 -Server "localhost\SQLEXPRESS"
#       .\deploy-local.ps1 -Server "localhost,1433" -User sa -Password "SuaSenha"
param(
    [string]$Server   = "localhost",
    [string]$User     = "",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$authArgs = if ($User) { @("-U", $User, "-P", $Password) } else { @("-E") }

# Pastas em ordem alfabética => ordem de execução garantida pela numeração
$scripts = Get-ChildItem -Path $root -Recurse -Filter *.sql | Sort-Object FullName

foreach ($script in $scripts) {
    Write-Host "Aplicando $($script.FullName.Substring($root.Length + 1))..." -ForegroundColor Cyan
    & sqlcmd -S $Server @authArgs -b -i $script.FullName
    if ($LASTEXITCODE -ne 0) { throw "Falha ao aplicar $($script.Name)" }
}

Write-Host "`nBanco ReportsDb atualizado com sucesso." -ForegroundColor Green
```

**Connection string local** (`Reports.Api/appsettings.Development.json`):

```json
{
  "ConnectionStrings": {
    "Reports": "Server=localhost;Database=ReportsDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

> **Evolução futura:** quando o projeto crescer, migre a execução manual para **DbUp** (roda os mesmos `.sql` embutidos no assembly e registra o que já foi aplicado em uma tabela de journal) ou **projeto SSDT/dacpac**. A estrutura de pastas proposta já é compatível com ambos — nada precisa ser reescrito.

---

## 4. Camada por camada — como cada uma funciona

Esta seção é o guia de referência do time. Para cada camada: **responsabilidade**, **o que entra**, **o que NÃO entra**, **dependências permitidas** e **como se testa**.

### 4.1 Domain (`Reports.Domain`) — o coração

**Responsabilidade:** representar os conceitos e regras de negócio que seriam verdadeiros mesmo se o sistema não fosse um software. "Uma fatura tem itens e seu total é a soma das linhas" é verdade independente de SQL Server ou PDF existirem.

**O que entra:**
- **Entidades** com comportamento (`Invoice.CalculateTotal()`, `Invoice.Cancel()`).
- **Value Objects** (`Money`, `DocumentNumber`) — tipos imutáveis que eliminam *primitive obsession* e concentram validação (um `DocumentNumber` inválido nem consegue ser construído).
- **Erros de domínio** (`DomainErrors.Invoice.NotFound`) — catálogo de erros com código + mensagem, consumido pelo Result pattern.
- **Enums de negócio** (`InvoiceStatus`).

**O que NÃO entra:** atributos de ORM, anotações de serialização, `[Required]`, referências a Dapper/QuestPDF/ASP.NET, DTOs de API, lógica de acesso a dados. **Zero pacotes NuGet.**

**Dependências:** nenhuma. É o centro do grafo.

**Como se testa:** testes unitários puros, sem mocks — instancia o objeto, chama o método, verifica o resultado. São os testes mais rápidos e baratos do projeto.

```csharp
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

public sealed record InvoiceItem(string Description, int Quantity, decimal UnitPrice);
```

> **Nota da comunidade:** para relatórios puros (read-only), Jason Taylor e Milan Jovanović aceitam pular a entidade de domínio e usar *read models* direto na Application. Mantenha o Domain para o que tiver regra de negócio real.

### 4.2 Application (`Reports.Application`) — a orquestração

**Responsabilidade:** definir **o que o sistema faz** (casos de uso) sem saber **como** os detalhes técnicos acontecem. Cada operação de negócio vira uma pasta de feature com Query/Command + Handler + modelos. O handler é um roteiro: busca dados por uma interface, aplica regras, devolve um `Result`.

**O que entra:**
- **Ports (interfaces):** `IInvoiceRepository`, `IPdfGenerator`, `IDbConnectionFactory`. A Application declara os contratos; a Infrastructure implementa. É isso que inverte a dependência.
- **Use cases:** um por operação, organizado por feature (*vertical slice*): `Invoices/GetInvoicePdf/`, `Invoices/CreateInvoice/`.
- **Read models / DTOs internos:** `InvoiceReportModel` — o formato de dados que o caso de uso precisa, desacoplado do schema do banco e do layout do PDF.
- **Result pattern:** sucesso/falha explícitos, sem exceptions para fluxo de negócio.
- **Validação** dos inputs do caso de uso (FluentValidation ou validação manual no handler).

**O que NÃO entra:** SQL, `SqlConnection`, Dapper, QuestPDF, HTTP, `HttpContext`, JSON. Se um `using Dapper;` aparecer aqui, a Regra de Dependência foi violada.

**Dependências:** apenas `Reports.Domain`.

**Como se testa:** testes unitários com mocks/fakes dos ports. Como o handler só conhece interfaces, testa-se 100% em memória — sem banco, sem PDF real:

```csharp
[Fact]
public async Task Handle_deve_retornar_NotFound_quando_fatura_nao_existe()
{
    var repo = Substitute.For<IInvoiceRepository>();
    repo.GetInvoiceReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((InvoiceReportModel?)null);

    var handler = new GetInvoicePdfHandler(repo, Substitute.For<IPdfGenerator>());

    var result = await handler.HandleAsync(new GetInvoicePdfQuery(Guid.NewGuid()), default);

    Assert.False(result.IsSuccess);
    Assert.Equal(DomainErrors.Invoice.NotFound, result.Error);
}
```

**Ports:**

```csharp
namespace Reports.Application.Abstractions.Data;

public interface IDbConnectionFactory
{
    ValueTask<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}

public interface IInvoiceRepository
{
    Task<InvoiceReportModel?> GetInvoiceReportAsync(Guid invoiceId, CancellationToken ct = default);
}
```

```csharp
namespace Reports.Application.Abstractions.Documents;

public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportModel model);
}
```

**Use case (handler):**

```csharp
namespace Reports.Application.Invoices.GetInvoicePdf;

public sealed record GetInvoicePdfQuery(Guid InvoiceId);

public sealed class GetInvoicePdfHandler(
    IInvoiceRepository repository,
    IPdfGenerator pdfGenerator)
{
    public async Task<Result<byte[]>> HandleAsync(GetInvoicePdfQuery query, CancellationToken ct)
    {
        var invoice = await repository.GetInvoiceReportAsync(query.InvoiceId, ct);

        if (invoice is null)
            return Result<byte[]>.Failure(DomainErrors.Invoice.NotFound);

        var pdf = pdfGenerator.GenerateInvoicePdf(invoice);
        return Result<byte[]>.Success(pdf);
    }
}
```

> Se preferir MediatR (padrão do template do Jason Taylor), transforme o handler em `IRequestHandler<GetInvoicePdfQuery, Result<byte[]>>` e adicione pipeline behaviors (validação, logging). Para escopo pequeno, handlers manuais registrados no DI são igualmente aceitos pela comunidade (menos dependências — e o MediatR passou a ter licença comercial em 2025).

**Read model do relatório:**

```csharp
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
```

### 4.3 Infrastructure (`Reports.Infrastructure`) — os detalhes técnicos

**Responsabilidade:** implementar os ports da Application usando tecnologias concretas. É a única camada que conhece SQL Server, Dapper e QuestPDF. Se amanhã o time trocar QuestPDF por outro renderer, ou SQL Server por Postgres, **só esta camada muda**.

**O que entra:**
- `Persistence/Database/` — **todos os scripts `.sql`** (tabelas, procedures, seeds, deploy). O banco versionado ao lado do código que o consome.
- `Persistence/SqlConnectionFactory.cs` e `Persistence/Repositories/` — implementações Dapper dos repositórios.
- `Documents/` — templates QuestPDF (`IDocument`, `IComponent`) e o adapter `QuestPdfGenerator`.
- `DependencyInjection.cs` — registro de tudo no container.

**O que NÃO entra:** regra de negócio. Se você se pegar escrevendo um `if` de negócio dentro de um repositório ou de um template de PDF, ele pertence ao Domain ou à Application.

**Dependências:** `Reports.Application` + `Reports.Domain` + NuGets (`Dapper`, `Microsoft.Data.SqlClient`, `QuestPDF`).

**Como se testa:** testes de **integração** — repositório rodando contra SQL Server real em Docker (**Testcontainers**), aplicando os mesmos scripts de `Persistence/Database` no container antes dos testes. O PDF se testa gerando bytes e validando o header `%PDF` + snapshot visual quando necessário.

**Regras internas da camada:**
- *Row models* (`InvoiceHeaderRow`, `InvoiceItemRow`) são `private` dentro do repositório: espelham as colunas da procedure e **nunca vazam** para fora — o repositório traduz row → read model da Application.
- Toda chamada Dapper usa `CommandDefinition` com `CancellationToken` e `CommandType.StoredProcedure`.
- Uma procedure com múltiplos resultsets (`QueryMultipleAsync`) substitui N chamadas — evita N+1.

**Connection factory:**

```csharp
public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public async ValueTask<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(configuration.GetConnectionString("Reports"));
        await connection.OpenAsync(ct);
        return connection;
    }
}
```

**Repositório com Dapper + procedure (multi-resultset):**

```csharp
public sealed class InvoiceRepository(IDbConnectionFactory connectionFactory) : IInvoiceRepository
{
    public async Task<InvoiceReportModel?> GetInvoiceReportAsync(Guid invoiceId, CancellationToken ct = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        var command = new CommandDefinition(
            "dbo.usp_Invoice_GetReport",
            new { InvoiceId = invoiceId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        using var multi = await connection.QueryMultipleAsync(command);

        var header = await multi.ReadSingleOrDefaultAsync<InvoiceHeaderRow>();
        if (header is null) return null;

        var items = (await multi.ReadAsync<InvoiceItemRow>()).ToList();

        return new InvoiceReportModel(
            header.Id, header.Number, DateOnly.FromDateTime(header.IssueDate),
            header.CustomerName, header.CustomerAddress,
            items.Select(i => new InvoiceReportItem(i.Description, i.Quantity, i.UnitPrice)).ToList());
    }

    private sealed record InvoiceHeaderRow(Guid Id, string Number, DateTime IssueDate,
        string CustomerName, string CustomerAddress);
    private sealed record InvoiceItemRow(string Description, int Quantity, decimal UnitPrice);
}
```

**QuestPDF — template do documento (`IDocument`):**

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public sealed class InvoiceDocument(InvoiceReportModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default with
    {
        Title = $"Fatura {model.Number}"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Fatura #{model.Number}").SemiBold().FontSize(20);
                    col.Item().Text($"Emitida em {model.IssueDate:dd/MM/yyyy}");
                });
            });

            page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
            {
                col.Item().Text(model.CustomerName).SemiBold();
                col.Item().Text(model.CustomerAddress);
                col.Item().PaddingTop(15).Element(ComposeTable);
                col.Item().AlignRight().PaddingTop(10)
                    .Text($"Total: {model.Total:C}").SemiBold().FontSize(12);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Text("Descrição").SemiBold();
                header.Cell().AlignRight().Text("Qtd").SemiBold();
                header.Cell().AlignRight().Text("Preço Unit.").SemiBold();
                header.Cell().AlignRight().Text("Total").SemiBold();
                header.Cell().ColumnSpan(4).PaddingTop(4).BorderBottom(1);
            });

            foreach (var item in model.Items)
            {
                table.Cell().Text(item.Description);
                table.Cell().AlignRight().Text(item.Quantity.ToString());
                table.Cell().AlignRight().Text($"{item.UnitPrice:C}");
                table.Cell().AlignRight().Text($"{item.LineTotal:C}");
            }
        });
    }
}
```

**Adapter que implementa o port:**

```csharp
public sealed class QuestPdfGenerator : IPdfGenerator
{
    public byte[] GenerateInvoicePdf(InvoiceReportModel model)
        => new InvoiceDocument(model).GeneratePdf();
}
```

**Registro de dependências da Infrastructure:**

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Licença QuestPDF: gratuita para empresas < US$ 1M/ano, non-profits e FOSS.
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();

        return services;
    }
}
```

### 4.4 API (`Reports.Api`) — a porta de entrada

**Responsabilidade:** traduzir HTTP ↔ casos de uso. Recebe a request, monta a Query/Command, chama o handler e converte o `Result` em resposta HTTP (`200` com o arquivo, `404` com ProblemDetails, `400` em validação). É uma camada **fina** de propósito: endpoints não devem ter lógica além dessa tradução.

**O que entra:** endpoints (Minimal API), `Program.cs` (composition root — o único lugar que enxerga todas as camadas para montar o DI), configuração (`appsettings`), middlewares (exception handler, logging, auth), mapeamento `Result` → HTTP.

**O que NÃO entra:** SQL, regra de negócio, chamadas diretas a repositórios pulando o handler.

**Dependências:** `Reports.Application` (para os handlers) e `Reports.Infrastructure` (apenas para chamar `AddInfrastructure()` no startup).

**Como se testa:** testes funcionais end-to-end com `WebApplicationFactory` — sobe a API em memória, chama o endpoint real, valida status code, `Content-Type: application/pdf` e bytes iniciando com `%PDF`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddScoped<GetInvoicePdfHandler>();

var app = builder.Build();

app.MapGet("/invoices/{id:guid}/pdf", async (
    Guid id,
    GetInvoicePdfHandler handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new GetInvoicePdfQuery(id), ct);

    return result.IsSuccess
        ? Results.File(result.Value, "application/pdf", $"invoice-{id}.pdf")
        : Results.NotFound(result.Error);
});

app.Run();
```

### 4.5 Resumo visual do fluxo de uma request

```text
HTTP GET /invoices/{id}/pdf
        │
        ▼
[Api] InvoiceEndpoints ── monta GetInvoicePdfQuery
        │
        ▼
[Application] GetInvoicePdfHandler
        │  1. repository.GetInvoiceReportAsync(id)   (port)
        │  2. valida: existe? senão → Result.Failure
        │  3. pdfGenerator.GenerateInvoicePdf(model) (port)
        ▼
[Infrastructure] InvoiceRepository ──► SQL Server: dbo.usp_Invoice_GetReport
[Infrastructure] QuestPdfGenerator ──► QuestPDF: InvoiceDocument → byte[]
        │
        ▼
[Api] Result.Success → Results.File(bytes, "application/pdf")
```

---

## 5. Guia do time — como implementar uma nova feature

Regra de ouro: **desenvolva de dentro para fora** (Domain → Application → Infrastructure → API). Assim as decisões de negócio são tomadas antes das decisões técnicas, e cada passo compila e testa sobre o anterior.

### 5.1 O passo a passo (receita padrão)

| # | Passo | Camada | Artefatos |
|---|---|---|---|
| 1 | Modelar o negócio (se houver regra nova) | Domain | Entidade / VO / erro em `DomainErrors` |
| 2 | Criar a pasta da feature com Query/Command + read model | Application | `Feature/NomeDaFeature/*.cs` |
| 3 | Declarar (ou estender) o port necessário | Application | Método novo em `IXxxRepository` / interface nova |
| 4 | Escrever o handler + testes unitários com mocks | Application | `NomeDaFeatureHandler.cs` + teste |
| 5 | Escrever o script SQL (tabela nova? procedure nova?) | Infrastructure | `Persistence/Database/0x_.../NNN_*.sql` |
| 6 | Implementar o port com Dapper + teste de integração | Infrastructure | Método no repositório |
| 7 | (Se for PDF) criar/ajustar template QuestPDF | Infrastructure | `Documents/Templates/*.cs` |
| 8 | Registrar novos serviços no DI | Infrastructure/Api | `DependencyInjection.cs` / `Program.cs` |
| 9 | Expor o endpoint + teste funcional | Api | `Endpoints/*.cs` |
| 10 | Rodar `deploy-local.ps1` e testar de ponta a ponta | — | — |

### 5.2 Exemplo completo: "Relatório de vendas por período em PDF"

Requisito: `GET /reports/sales?from=2026-01-01&to=2026-06-30` retorna um PDF com o total faturado por cliente no período.

**Passo 1 — Domain.** Não há regra de negócio nova (é um relatório de leitura pura): pulamos a entidade e vamos direto ao read model na Application. Só adicionamos o erro:

```csharp
// Domain/Errors/DomainErrors.cs
public static class DomainErrors
{
    public static class SalesReport
    {
        public static readonly Error InvalidPeriod =
            new("SalesReport.InvalidPeriod", "A data inicial deve ser anterior à final.");
        public static readonly Error Empty =
            new("SalesReport.Empty", "Nenhuma venda encontrada no período.");
    }
}
```

**Passo 2 — Application: pasta da feature + read model.**

```csharp
// Application/Reports/GetSalesReportPdf/GetSalesReportPdfQuery.cs
public sealed record GetSalesReportPdfQuery(DateOnly From, DateOnly To);

// Application/Reports/GetSalesReportPdf/SalesReportModel.cs
public sealed record SalesReportModel(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<SalesReportLine> Lines)
{
    public decimal GrandTotal => Lines.Sum(l => l.Total);
}

public sealed record SalesReportLine(string CustomerName, int InvoiceCount, decimal Total);
```

**Passo 3 — Application: port.** Relatório novo com fonte de dados própria → interface nova (não inche `IInvoiceRepository` com métodos de outro contexto):

```csharp
// Application/Abstractions/Data/ISalesReportRepository.cs
public interface ISalesReportRepository
{
    Task<IReadOnlyList<SalesReportLine>> GetSalesByPeriodAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);
}
```

E o port de PDF ganha um método (ou, se preferir granularidade, crie `ISalesReportPdfGenerator`):

```csharp
public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportModel model);
    byte[] GenerateSalesReportPdf(SalesReportModel model);   // novo
}
```

**Passo 4 — Application: handler + teste.**

```csharp
public sealed class GetSalesReportPdfHandler(
    ISalesReportRepository repository,
    IPdfGenerator pdfGenerator)
{
    public async Task<Result<byte[]>> HandleAsync(GetSalesReportPdfQuery query, CancellationToken ct)
    {
        if (query.From > query.To)
            return Result<byte[]>.Failure(DomainErrors.SalesReport.InvalidPeriod);

        var lines = await repository.GetSalesByPeriodAsync(query.From, query.To, ct);

        if (lines.Count == 0)
            return Result<byte[]>.Failure(DomainErrors.SalesReport.Empty);

        var model = new SalesReportModel(query.From, query.To, lines);
        return Result<byte[]>.Success(pdfGenerator.GenerateSalesReportPdf(model));
    }
}
```

Teste unitário (sem banco, sem PDF): mocka `ISalesReportRepository` retornando lista vazia e verifica `SalesReport.Empty`; mocka período invertido e verifica `InvalidPeriod`.

**Passo 5 — Infrastructure: script SQL.** Novo arquivo `Persistence/Database/02_Procedures/104_usp_Sales_GetByPeriod.sql`:

```sql
USE ReportsDb;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Sales_GetByPeriod
    @DateFrom DATE,
    @DateTo   DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Name                              AS CustomerName,
        COUNT(DISTINCT i.Id)                AS InvoiceCount,
        SUM(it.Quantity * it.UnitPrice)     AS Total
    FROM dbo.Invoices i
    INNER JOIN dbo.Customers    c  ON c.Id = i.CustomerId
    INNER JOIN dbo.InvoiceItems it ON it.InvoiceId = i.Id
    WHERE i.IssueDate BETWEEN @DateFrom AND @DateTo
      AND i.Status = 2  -- somente faturas emitidas
    GROUP BY c.Name
    ORDER BY Total DESC;
END
GO
```

Rode `.\deploy-local.ps1` — por ser idempotente, só a procedure nova é criada/atualizada.

**Passo 6 — Infrastructure: repositório + teste de integração.**

```csharp
public sealed class SalesReportRepository(IDbConnectionFactory connectionFactory) : ISalesReportRepository
{
    public async Task<IReadOnlyList<SalesReportLine>> GetSalesByPeriodAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        var command = new CommandDefinition(
            "dbo.usp_Sales_GetByPeriod",
            new { DateFrom = from.ToDateTime(TimeOnly.MinValue), DateTo = to.ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct);

        var rows = await connection.QueryAsync<SalesRow>(command);

        return rows.Select(r => new SalesReportLine(r.CustomerName, r.InvoiceCount, r.Total)).ToList();
    }

    private sealed record SalesRow(string CustomerName, int InvoiceCount, decimal Total);
}
```

Teste de integração (Testcontainers): sobe SQL Server em Docker, aplica os scripts de `Persistence/Database` + um seed do teste, chama o método real e valida agregação e ordenação.

**Passo 7 — Infrastructure: template QuestPDF.** Novo `Documents/Templates/SalesReportDocument.cs` seguindo o mesmo padrão de `InvoiceDocument` (Header com o período, tabela Cliente/Faturas/Total, rodapé com paginação), e o adapter ganha:

```csharp
public byte[] GenerateSalesReportPdf(SalesReportModel model)
    => new SalesReportDocument(model).GeneratePdf();
```

Dica: itere o layout com o **QuestPDF Companion App** (hot-reload visual) antes de fechar o PR.

**Passo 8 — DI.**

```csharp
// Infrastructure/DependencyInjection.cs
services.AddScoped<ISalesReportRepository, SalesReportRepository>();

// Api/Program.cs
builder.Services.AddScoped<GetSalesReportPdfHandler>();
```

**Passo 9 — API: endpoint + teste funcional.**

```csharp
app.MapGet("/reports/sales", async (
    DateOnly from, DateOnly to,
    GetSalesReportPdfHandler handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new GetSalesReportPdfQuery(from, to), ct);

    return result.IsSuccess
        ? Results.File(result.Value, "application/pdf", $"sales-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf")
        : result.Error.Code == "SalesReport.InvalidPeriod"
            ? Results.BadRequest(result.Error)
            : Results.NotFound(result.Error);
});
```

Teste funcional: `GET /reports/sales?from=2026-01-01&to=2026-06-30` → `200`, `Content-Type: application/pdf`, bytes iniciando com `%PDF`. E `from > to` → `400`.

**Passo 10 —** `deploy-local.ps1`, subir a API, testar manualmente com o seed.

### 5.3 Checklist de PR (colar no template do repositório)

- [ ] A feature tem pasta própria em `Application/<Contexto>/<Feature>/`?
- [ ] Handler depende **só de interfaces** (nenhum `using Dapper/QuestPDF` na Application)?
- [ ] Script `.sql` novo está em `Infrastructure/Persistence/Database/` com numeração correta e é **idempotente** (`CREATE OR ALTER` / `IF NOT EXISTS`)?
- [ ] Procedure usa `SET NOCOUNT ON` (e `SET XACT_ABORT ON` + transação se escrever em mais de uma tabela)?
- [ ] Row models do Dapper são `private` no repositório?
- [ ] `CommandDefinition` com `CancellationToken` e `CommandType.StoredProcedure`?
- [ ] Teste unitário do handler (mocks) + teste de integração do repositório (Testcontainers)?
- [ ] Serviços registrados no DI (`DependencyInjection.cs` / `Program.cs`)?
- [ ] Endpoint converte `Result` em HTTP correto (`400` validação, `404` não encontrado)?
- [ ] `deploy-local.ps1` roda sem erro a partir de um banco limpo?

### 5.4 Erros comuns a evitar (revisar com atenção no code review)

1. **Vazar row model do Dapper para a Application** — a Application só conhece os read models dela.
2. **Colocar `if` de negócio na procedure ou no template do PDF** — filtros de dados podem viver no SQL (ex.: `Status = 2`), mas decisões como "fatura cancelada não gera PDF" pertencem ao handler.
3. **Repositório genérico** (`IRepository<T>` com Dapper) — métodos específicos por caso de uso são mais claros (Ardalis/Jason Taylor).
4. **Alterar script antigo de tabela em vez de criar um novo** — depois que um script de tabela foi aplicado em ambientes compartilhados, mudanças de schema entram como novo script (`004_AlterInvoices_AddDueDate.sql`). Procedures podem ser editadas no próprio arquivo (o `CREATE OR ALTER` reaplica).
5. **Esquecer o registro no DI** — o erro `Unable to resolve service` em runtime quase sempre é isso.
6. **Endpoint chamando repositório direto** — sempre passe pelo handler, mesmo que pareça "só um SELECT".

---

## 6. Fases de implementação (roadmap)

### Fase 1 — Fundação (1–2 dias)
- [ ] Criar a solution e os 4 projetos com as referências corretas (validar o grafo de dependências).
- [ ] Configurar `Directory.Build.props` (nullable enabled, `TreatWarningsAsErrors`, LangVersion latest).
- [ ] Adicionar analisadores (ex.: `Microsoft.CodeAnalysis.NetAnalyzers`) e, opcionalmente, testes de arquitetura com **NetArchTest/ArchUnitNET** para garantir a Regra de Dependência em CI (prática recomendada por Milan Jovanović).

### Fase 2 — Banco de dados (1 dia)
- [ ] Criar `Infrastructure/Persistence/Database/` com a estrutura `00_Setup` / `01_Tables` / `02_Procedures` / `03_Seeds` e os scripts das seções 3.2–3.9.
- [ ] `deploy-local.ps1` funcionando do zero em máquina limpa (documentar pré-requisito `sqlcmd`).
- [ ] Validar procedures no SSMS/Azure Data Studio com o seed.

### Fase 3 — Application + Domain (1–2 dias)
- [ ] Entidades/read models e `Result` pattern.
- [ ] Ports: `IDbConnectionFactory`, `IInvoiceRepository`, `IPdfGenerator`.
- [ ] Handler `GetInvoicePdfHandler` + testes unitários com mocks (o handler não sabe nada de Dapper/QuestPDF, então testa-se 100% em memória).

### Fase 4 — Infrastructure (2–3 dias)
- [ ] `SqlConnectionFactory` + `InvoiceRepository` com Dapper.
- [ ] Testes de integração do repositório com **Testcontainers** (SQL Server em Docker), aplicando os mesmos scripts de `Persistence/Database` no container.
- [ ] `InvoiceDocument` no QuestPDF, iterando o layout com o **QuestPDF Companion App** (hot-reload visual do documento).
- [ ] Componentes reutilizáveis (`IComponent`) para cabeçalhos, endereços, rodapés.

### Fase 5 — API + Cross-cutting (1–2 dias)
- [ ] Endpoint `/invoices/{id}/pdf` + `ProblemDetails` para erros.
- [ ] Logging estruturado (Serilog), health checks, OpenAPI.
- [ ] Teste funcional end-to-end: chama o endpoint, valida `Content-Type: application/pdf` e bytes com header `%PDF`.

### Fase 6 — Hardening (contínuo)
- [ ] Cache de PDFs imutáveis (ex.: fatura fechada) se o volume justificar.
- [ ] Geração assíncrona em background (canal/fila) para relatórios pesados, retornando 202 + polling — recomendação comum para PDFs grandes.
- [ ] Observabilidade: métricas de tempo de geração e tamanho dos documentos.
- [ ] Avaliar migração do `deploy-local.ps1` para **DbUp** (mesmos scripts, com journal de execução) quando houver mais de um ambiente.

---

## 7. Decisões e trade-offs (o que a comunidade diz)

| Decisão | Alternativa | Por que esta escolha |
|---|---|---|
| Dapper + procedures | EF Core | Relatórios são read-heavy; procedures dão controle fino de SQL e performance. Dapper é o micro-ORM padrão da comunidade para esse caso. |
| Scripts `.sql` em `Infrastructure/Persistence/Database` | Pasta `database/` fora do src / projeto SSDT separado | Mantém o banco versionado ao lado do código que o consome; os testes de integração reutilizam os mesmos arquivos; migração futura para DbUp/SSDT é direta. |
| Scripts idempotentes numerados + `deploy-local.ps1` | Migrations de ORM | Sem ORM não há migrations automáticas; numeração + idempotência dão previsibilidade e onboarding em um comando. |
| `IPdfGenerator` na Application | QuestPDF direto no handler | Mantém a Regra de Dependência; permite trocar o renderer e testar o use case sem gerar PDF real. |
| Handlers manuais | MediatR | MediatR passou a ter licença comercial em 2025; para projetos novos muita gente da comunidade migrou para handlers próprios ou libs como Wolverine. Ambos são válidos. |
| Repositório específico (`IInvoiceRepository`) | Repositório genérico | Ardalis e Jason Taylor desaconselham repositório genérico com Dapper — métodos específicos por caso de uso são mais claros. |
| TVP em `usp_Invoice_Create` | N inserts em loop | Uma ida ao banco, transação única, atomicidade garantida com `XACT_ABORT`. |
| Retornar `byte[]` | `Stream` | Para faturas típicas, `byte[]` simplifica. Para documentos muito grandes, troque o port para `Stream`/`PipeWriter`. |

## 8. Observações sobre licenciamento do QuestPDF

O QuestPDF é gratuito (Community License) para indivíduos e empresas com receita bruta anual abaixo de US$ 1 milhão, ONGs e projetos FOSS; acima disso é necessária licença paga. Configure explicitamente `QuestPDF.Settings.License` na inicialização — a biblioteca lança exceção se a licença não for definida. Para estudo/avaliação existe o `LicenseType.Evaluation`.

## 9. Referências

- QuestPDF — Quick Start e Invoice Tutorial: https://www.questpdf.com/quick-start.html
- Jason Taylor — Clean Architecture Solution Template: https://github.com/jasontaylordev/CleanArchitecture
- Ardalis (Steve Smith) — Clean Architecture Template: https://github.com/ardalis/CleanArchitecture
- Milan Jovanović — artigos sobre Clean Architecture, Dapper e Result pattern: https://www.milanjovanovic.tech/
- Robert C. Martin — *The Clean Architecture* (blog original, 2012)
- Dapper — documentação oficial: https://github.com/DapperLib/Dapper
- DbUp — versionamento de scripts SQL: https://dbup.readthedocs.io/
- Testcontainers for .NET — SQL Server: https://dotnet.testcontainers.org/modules/mssql/
