# Plano de Camada — Reports.Api

> **Plano mestre:** [`plano-clean-architecture-dotnet10-questpdf.md`](./plano-clean-architecture-dotnet10-questpdf.md) — seção 4.4
> **Ordem de implementação:** 4º (última; a porta de entrada)
> **Depende de:** `Reports.Application` (handlers) e `Reports.Infrastructure` (apenas para chamar `AddInfrastructure()` no startup).

---

## 1. Objetivo da camada

Traduzir HTTP ↔ casos de uso. Recebe a request, monta a Query/Command, chama o handler e converte o `Result` em resposta HTTP. É uma camada **fina** de propósito: endpoints não têm lógica além dessa tradução.

**Regra de ouro:** nada de SQL, regra de negócio, ou chamadas diretas a repositórios pulando o handler. O `Program.cs` é o *composition root* — o único lugar que enxerga todas as camadas para montar o DI.

---

## 2. Escopo (o que entra nesta camada)

| Artefato | Arquivo | Papel |
|---|---|---|
| `Program.cs` | `Program.cs` | Composition root: DI, middlewares, `app.Run()`. |
| Endpoints | `Endpoints/InvoiceEndpoints.cs` | Mapeamento das rotas (Minimal API). |
| Config | `appsettings.json` / `appsettings.Development.json` | Connection string, logging. |
| Mapeamento de erro | (inline nos endpoints ou helper) | `Result` → HTTP (`200`/`400`/`404` com `ProblemDetails`). |

**O que NÃO entra:** SQL, regra de negócio.

---

## 3. Passo a passo

### Passo 3.1 — `appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Reports": "Server=localhost;Database=ReportsDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### Passo 3.2 — `Program.cs` (composition root)
```csharp
using Reports.Application.Invoices.GetInvoicePdf;
using Reports.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddScoped<GetInvoicePdfHandler>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler();
app.MapInvoiceEndpoints();

app.Run();

public partial class Program; // para WebApplicationFactory nos testes funcionais
```

### Passo 3.3 — `Endpoints/InvoiceEndpoints.cs`
Endpoint fino que converte `Result` em HTTP. Sucesso → `Results.File` com `application/pdf`; falha → `ProblemDetails` conforme o código do erro.

```csharp
using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/invoices/{id:guid}/pdf", async (
            Guid id,
            GetInvoicePdfHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetInvoicePdfQuery(id), ct);

            return result.IsSuccess
                ? Results.File(result.Value, "application/pdf", $"invoice-{id}.pdf")
                : Results.Problem(
                    title: result.Error.Message,
                    statusCode: StatusCodes.Status404NotFound,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        });

        return app;
    }
}
```

> **Mapeamento `Result` → HTTP:** para o caso de PDF, o único erro é `Invoice.NotFound` → `404`. Quando surgirem erros de validação (ex.: `SalesReport.InvalidPeriod` no exemplo da seção 5.2 do plano mestre), mapeie pelo prefixo/código do erro: `*.Invalid*` → `400`, `*.NotFound`/`*.Empty` → `404`. Considere extrair um helper `result.ToHttpResult()` quando houver mais de um endpoint.

### Passo 3.4 — Cross-cutting (recomendado — Fase 5 do plano mestre)
- [ ] Logging estruturado (Serilog).
- [ ] `ProblemDetails` para todos os erros (`AddProblemDetails` + `UseExceptionHandler`).
- [ ] Health checks (incluir verificação da conexão SQL).
- [ ] OpenAPI (`MapOpenApi` no .NET 10 / Swagger).

---

## 4. Configuração do projeto (`Reports.Api.csproj`)

- SDK `Microsoft.NET.Sdk.Web`.
- `ProjectReference` → `Reports.Application` e `Reports.Infrastructure`.
- Pacotes: `Serilog.AspNetCore` (opcional), `Microsoft.AspNetCore.OpenApi`.

---

## 5. Testes (`Reports.Api.FunctionalTests`)

End-to-end com `WebApplicationFactory` (sobe a API em memória). Estratégia de banco: usar Testcontainers ou banco de teste dedicado com o seed:
- [ ] `GET /invoices/{id}/pdf` com id semeado → `200`, `Content-Type: application/pdf`, bytes iniciando com `%PDF`.
- [ ] `GET /invoices/{id}/pdf` com id inexistente → `404` + `ProblemDetails` com `code = "Invoice.NotFound"`.
- [ ] `GET /invoices/{id}/pdf` com id malformado → `404`/`400` pela restrição de rota `:guid`.

---

## 6. Definition of Done

- [ ] `Program.cs` monta o DI (`AddInfrastructure()` + handlers) e sobe sem erro.
- [ ] Endpoint `/invoices/{id}/pdf` retorna o PDF (`200`) e `ProblemDetails` (`404`) corretamente.
- [ ] Nenhuma lógica de negócio ou SQL na API; endpoint só traduz HTTP ↔ handler.
- [ ] `ProblemDetails`, logging e OpenAPI configurados.
- [ ] Teste funcional end-to-end verde: valida status, `Content-Type` e header `%PDF`.
- [ ] Teste manual com o seed (`deploy-local.ps1`) de ponta a ponta OK.
