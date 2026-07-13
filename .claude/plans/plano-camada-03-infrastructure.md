# Plano de Camada — Reports.Infrastructure

> **Plano mestre:** [`plano-clean-architecture-dotnet10-questpdf.md`](./plano-clean-architecture-dotnet10-questpdf.md) — seções 3 (banco) e 4.3
> **Ordem de implementação:** 3º (após Domain e Application)
> **Depende de:** `Reports.Application` + `Reports.Domain`. NuGets: `Dapper`, `Microsoft.Data.SqlClient`, `QuestPDF`.

---

## 1. Objetivo da camada

Implementar os ports da Application usando tecnologias concretas. É a **única** camada que conhece SQL Server, Dapper e QuestPDF. Se o time trocar QuestPDF por outro renderer, ou SQL Server por Postgres, só esta camada muda.

**Regra de ouro:** nenhuma regra de negócio aqui. Um `if` de negócio dentro de um repositório ou template de PDF pertence ao Domain ou à Application. Filtros de *dados* (ex.: `Status = 2`) podem viver no SQL.

---

## 2. Escopo (o que entra nesta camada)

### 2.1 Banco de dados (`Persistence/Database/`) — todos os `.sql`
| Pasta / arquivo | Papel |
|---|---|
| `00_Setup/000_CreateDatabase.sql` | Cria `ReportsDb` se não existir. |
| `01_Tables/001_Customers.sql` | Tabela `Customers`. |
| `01_Tables/002_Invoices.sql` | Tabela `Invoices` + índices. |
| `01_Tables/003_InvoiceItems.sql` | Tabela `InvoiceItems` + índice de cobertura. |
| `02_Procedures/101_usp_Invoice_GetReport.sql` | Procedure do PDF (2 resultsets). |
| `02_Procedures/102_usp_Invoice_List.sql` | Listagem paginada. |
| `02_Procedures/103_usp_Invoice_Create.sql` | Escrita transacional com TVP. |
| `03_Seeds/201_SeedDevData.sql` | Dados de exemplo idempotentes. |
| `deploy-local.ps1` | Aplica tudo em ordem via `sqlcmd`. |

Conteúdo completo dos scripts: seções **3.2 a 3.10** do plano mestre. Convenções: tabelas com `IF NOT EXISTS`; procedures com `CREATE OR ALTER`; `SET NOCOUNT ON`; `SET XACT_ABORT ON` + transação quando escreve em mais de uma tabela. **Rodar tudo de novo é sempre seguro (idempotência).**

### 2.2 Persistência (Dapper)
| Arquivo | Papel |
|---|---|
| `Persistence/SqlConnectionFactory.cs` | Implementa `IDbConnectionFactory`. |
| `Persistence/Repositories/InvoiceRepository.cs` | Implementa `IInvoiceRepository` via `QueryMultipleAsync`. |

### 2.3 Documentos (QuestPDF)
| Arquivo | Papel |
|---|---|
| `Documents/QuestPdfGenerator.cs` | Adapter que implementa `IPdfGenerator`. |
| `Documents/Templates/InvoiceDocument.cs` | `IDocument` do QuestPDF (layout da fatura). |
| `Documents/Templates/Components/AddressComponent.cs` | `IComponent` reutilizável (opcional). |

### 2.4 Composição
| Arquivo | Papel |
|---|---|
| `DependencyInjection.cs` | `AddInfrastructure()` — registra factory, repositório, gerador de PDF, licença QuestPDF. |

---

## 3. Passo a passo

### Passo 3.1 — Scripts SQL
Criar a árvore `Persistence/Database/` com os scripts das seções 3.2–3.9 do plano mestre e o `deploy-local.ps1` (seção 3.10). Adicionar ao `.csproj` para acompanhar o build (útil aos testes de integração):

```xml
<ItemGroup>
  <None Include="Persistence\Database\**\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Rodar `.\deploy-local.ps1` e validar as procedures no SSMS/Azure Data Studio com o seed.

### Passo 3.2 — `SqlConnectionFactory`
```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Reports.Application.Abstractions.Data;

namespace Reports.Infrastructure.Persistence;

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

### Passo 3.3 — `InvoiceRepository` (multi-resultset, evita N+1)
Conforme seção 4.3 do plano mestre. Pontos-chave:
- `CommandDefinition` com `CommandType.StoredProcedure` e `CancellationToken`.
- `QueryMultipleAsync` → `ReadSingleOrDefaultAsync` (header) + `ReadAsync` (items).
- **Row models (`InvoiceHeaderRow`, `InvoiceItemRow`) são `private` no repositório** — traduzem row → read model da Application e nunca vazam.

```csharp
using System.Data;
using Dapper;
using Reports.Application.Abstractions.Data;
using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Infrastructure.Persistence.Repositories;

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

### Passo 3.4 — `InvoiceDocument` (QuestPDF)
Template `IDocument` completo na seção 4.3 do plano mestre (Header com número/data, Content com cliente + tabela de itens + total, Footer com paginação). Iterar o layout com o **QuestPDF Companion App** (hot-reload visual) antes de fechar o PR. Extrair blocos reutilizáveis (endereço, rodapé) como `IComponent` quando fizer sentido.

### Passo 3.5 — `QuestPdfGenerator` (adapter)
```csharp
using QuestPDF.Fluent;
using Reports.Application.Abstractions.Documents;
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Infrastructure.Documents.Templates;

namespace Reports.Infrastructure.Documents;

public sealed class QuestPdfGenerator : IPdfGenerator
{
    public byte[] GenerateInvoicePdf(InvoiceReportModel model)
        => new InvoiceDocument(model).GeneratePdf();
}
```

### Passo 3.6 — `DependencyInjection.cs`
```csharp
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Reports.Application.Abstractions.Data;
using Reports.Application.Abstractions.Documents;
using Reports.Infrastructure.Documents;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Persistence.Repositories;

namespace Reports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Licença QuestPDF: gratuita p/ empresas < US$ 1M/ano, non-profits e FOSS.
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddSingleton<IPdfGenerator, QuestPdfGenerator>();

        return services;
    }
}
```

---

## 4. Configuração do projeto (`Reports.Infrastructure.csproj`)

- `ProjectReference` → `Reports.Application` e `Reports.Domain`.
- `PackageReference`: `Dapper`, `Microsoft.Data.SqlClient`, `QuestPDF`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`.
- `<None Include="Persistence\Database\**\*.sql" CopyToOutputDirectory="PreserveNewest" />`.

---

## 5. Testes (`Reports.Infrastructure.IntegrationTests`)

Testes de **integração** com SQL Server real em Docker (**Testcontainers**), aplicando os mesmos scripts de `Persistence/Database` no container antes dos testes:
- [ ] `InvoiceRepository.GetInvoiceReportAsync` retorna o read model correto (header + itens ordenados por `LineNumber`) para um id semeado.
- [ ] Retorna `null` para id inexistente.
- [ ] Geração de PDF: `QuestPdfGenerator.GenerateInvoicePdf` produz `byte[]` cujos primeiros bytes são o header `%PDF`. Snapshot visual quando necessário.
- [ ] `deploy-local.ps1` roda do zero contra o container sem erro (idempotência: rodar duas vezes segue OK).

---

## 6. Definition of Done

- [ ] `deploy-local.ps1` sobe o `ReportsDb` do zero em máquina limpa (pré-requisito: `sqlcmd`).
- [ ] Procedures validadas no SSMS/Azure Data Studio com o seed.
- [ ] `SqlConnectionFactory`, `InvoiceRepository` e `QuestPdfGenerator` implementam os ports.
- [ ] Row models do Dapper são `private`; nenhuma regra de negócio na Infra.
- [ ] `AddInfrastructure()` registra todos os serviços; licença QuestPDF configurada.
- [ ] Scripts `.sql` copiados para o output do build.
- [ ] Testes de integração verdes.
