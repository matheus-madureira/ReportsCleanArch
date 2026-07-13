# Plano de Camada — Reports.Application

> **Plano mestre:** [`plano-clean-architecture-dotnet10-questpdf.md`](./plano-clean-architecture-dotnet10-questpdf.md) — seção 4.2
> **Ordem de implementação:** 2º (após o Domain)
> **Depende de:** `Reports.Domain` (única referência de projeto permitida).

---

## 1. Objetivo da camada

Definir **o que o sistema faz** (casos de uso) sem saber **como** os detalhes técnicos acontecem. A Application declara os contratos (ports/interfaces); a Infrastructure os implementa — é isso que inverte a dependência.

**Regra de ouro:** nenhum `using Dapper;`, `using QuestPDF;`, `SqlConnection`, `HttpContext` ou JSON aqui. Se aparecer, a Regra de Dependência foi violada.

---

## 2. Escopo (o que entra nesta camada)

| Artefato | Arquivo | Papel |
|---|---|---|
| `Result` / `Result<T>` | `Common/Result.cs` | Sucesso/falha explícitos, sem exceptions para fluxo de negócio. |
| `IDbConnectionFactory` | `Abstractions/Data/IDbConnectionFactory.cs` | Port: abre conexão (implementado na Infra). |
| `IInvoiceRepository` | `Abstractions/Data/IInvoiceRepository.cs` | Port: acesso a dados de fatura. |
| `IPdfGenerator` | `Abstractions/Documents/IPdfGenerator.cs` | Port de saída para gerar PDF. |
| `InvoiceReportModel` + `InvoiceReportItem` | `Invoices/GetInvoicePdf/InvoiceReportModel.cs` | Read model do relatório (desacoplado de schema e de layout). |
| `GetInvoicePdfQuery` | `Invoices/GetInvoicePdf/GetInvoicePdfQuery.cs` | Input do caso de uso. |
| `GetInvoicePdfHandler` | `Invoices/GetInvoicePdf/GetInvoicePdfHandler.cs` | Orquestra: busca dados → valida → gera PDF. |

**Organização:** *vertical slice* — uma pasta por feature em `Invoices/<Feature>/`. Ports genéricos ficam em `Abstractions/`.

**O que NÃO entra:** SQL, Dapper, QuestPDF, HTTP, mapeamento para status code (isso é da API).

---

## 3. Passo a passo

### Passo 3.1 — `Common/Result.cs`
Result pattern (estilo Milan Jovanović). `Result` (sem valor) e `Result<T>` (com valor), fábricas `Success`/`Failure`, propriedades `IsSuccess`, `Error`, `Value`.

```csharp
using Reports.Domain.Common;

namespace Reports.Application.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Sucesso não pode ter erro.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Falha exige um erro.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não há valor em um resultado de falha.");

    public static Result<T> Success(T value) => new(value, true, Error.None);
    public static Result<T> Failure(Error error) => new(default, false, error);
}
```

### Passo 3.2 — Ports de dados
```csharp
// Abstractions/Data/IDbConnectionFactory.cs
using System.Data;
namespace Reports.Application.Abstractions.Data;

public interface IDbConnectionFactory
{
    ValueTask<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
```

```csharp
// Abstractions/Data/IInvoiceRepository.cs
using Reports.Application.Invoices.GetInvoicePdf;
namespace Reports.Application.Abstractions.Data;

public interface IInvoiceRepository
{
    Task<InvoiceReportModel?> GetInvoiceReportAsync(Guid invoiceId, CancellationToken ct = default);
}
```

### Passo 3.3 — Port de PDF
```csharp
// Abstractions/Documents/IPdfGenerator.cs
using Reports.Application.Invoices.GetInvoicePdf;
namespace Reports.Application.Abstractions.Documents;

public interface IPdfGenerator
{
    byte[] GenerateInvoicePdf(InvoiceReportModel model);
}
```

### Passo 3.4 — Read model
```csharp
namespace Reports.Application.Invoices.GetInvoicePdf;

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

### Passo 3.5 — Query + Handler
```csharp
// GetInvoicePdfQuery.cs
namespace Reports.Application.Invoices.GetInvoicePdf;
public sealed record GetInvoicePdfQuery(Guid InvoiceId);
```

```csharp
// GetInvoicePdfHandler.cs
using Reports.Application.Abstractions.Data;
using Reports.Application.Abstractions.Documents;
using Reports.Application.Common;
using Reports.Domain.Errors;

namespace Reports.Application.Invoices.GetInvoicePdf;

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

### Passo 3.6 — (Opcional) `DependencyInjection.cs` da Application
Se usar validação (FluentValidation) ou pipeline behaviors, registre aqui via `AddApplication()`. Para handlers manuais simples, o registro pode ficar no `Program.cs` da API.

---

## 4. Configuração do projeto (`Reports.Application.csproj`)

- `ProjectReference` → `Reports.Domain` (única).
- Pacotes permitidos (opcionais): `FluentValidation` para validação de inputs. **Nada** de Dapper/QuestPDF/ASP.NET.

---

## 5. Testes (`Reports.Application.UnitTests`)

100% em memória, com mocks/fakes dos ports (ex.: NSubstitute):
- [ ] `Handle` retorna `NotFound` quando o repositório devolve `null` (não chama o `IPdfGenerator`).
- [ ] `Handle` retorna `Success` com os bytes quando a fatura existe.
- [ ] `InvoiceReportModel.Total` e `InvoiceReportItem.LineTotal` calculam corretamente.
- [ ] Contratos do `Result<T>`: acessar `.Value` em falha lança; `.Error` em sucesso é `Error.None`.

```csharp
[Fact]
public async Task Handle_deve_retornar_NotFound_quando_fatura_nao_existe()
{
    var repo = Substitute.For<IInvoiceRepository>();
    repo.GetInvoiceReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((InvoiceReportModel?)null);

    var handler = new GetInvoicePdfHandler(repo, Substitute.For<IPdfGenerator>());

    var result = await handler.HandleAsync(new GetInvoicePdfQuery(Guid.NewGuid()), default);

    Assert.True(result.IsFailure);
    Assert.Equal(DomainErrors.Invoice.NotFound, result.Error);
}
```

---

## 6. Definition of Done

- [ ] Projeto compila sem warnings; referencia **apenas** `Reports.Domain`.
- [ ] Nenhum `using` de Dapper/QuestPDF/ASP.NET/`System.Data.SqlClient`.
- [ ] Ports (`IDbConnectionFactory`, `IInvoiceRepository`, `IPdfGenerator`) declarados.
- [ ] `GetInvoicePdfHandler` + read models prontos.
- [ ] Testes unitários do handler verdes.
