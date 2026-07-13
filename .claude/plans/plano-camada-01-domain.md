# Plano de Camada — Reports.Domain

> **Plano mestre:** [`plano-clean-architecture-dotnet10-questpdf.md`](./plano-clean-architecture-dotnet10-questpdf.md) — seção 4.1
> **Ordem de implementação:** 1º (o coração; desenvolve-se de dentro para fora)
> **Depende de:** nada (é o centro do grafo). Pré-requisito: solution e projeto `Reports.Domain` criados (Fase 1 do plano mestre).

---

## 1. Objetivo da camada

Representar os conceitos e regras de negócio que seriam verdadeiros mesmo se o sistema não fosse um software. Zero pacotes NuGet, zero dependências de projeto.

**Regra de ouro:** se um `using Dapper;`, `using QuestPDF;`, `[Required]`, ou qualquer atributo de ORM/serialização aparecer aqui, a Regra de Dependência foi violada.

---

## 2. Escopo (o que entra nesta camada)

| Artefato | Arquivo | Papel |
|---|---|---|
| `Error` (Value Object) | `Common/Error.cs` | Tipo de erro (código + mensagem) usado pelo Result pattern. |
| `DomainErrors` | `Errors/DomainErrors.cs` | Catálogo estático de erros de negócio. |
| `InvoiceStatus` (enum) | `Enums/InvoiceStatus.cs` | Estados da fatura (Draft=1, Issued=2, Cancelled=3). |
| `Invoice` (entidade) | `Entities/Invoice.cs` | Cabeçalho da fatura + itens + total calculado. |
| `InvoiceItem` (entidade/record) | `Entities/InvoiceItem.cs` | Linha da fatura. |
| `Money` (Value Object) — opcional | `ValueObjects/Money.cs` | Valor monetário imutável (elimina *primitive obsession*). |

> **Nota:** para relatórios read-only, a comunidade aceita pular a entidade de domínio e usar *read models* direto na Application. Mantenha `Invoice`/`InvoiceItem` no Domain apenas se houver regra de negócio real (ex.: `Cancel()`, validação de invariante). Para o MVP focado em PDF, o mínimo essencial é `Error`, `DomainErrors` e `InvoiceStatus`.

**O que NÃO entra:** atributos de ORM/serialização, DTOs de API, lógica de acesso a dados, referências a Dapper/QuestPDF/ASP.NET.

---

## 3. Passo a passo

### Passo 3.1 — `Common/Error.cs`
Value Object base do Result pattern. Record imutável com `Code` e `Message`, com igualdade estrutural.

```csharp
namespace Reports.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

### Passo 3.2 — `Enums/InvoiceStatus.cs`
```csharp
namespace Reports.Domain.Enums;

public enum InvoiceStatus : byte
{
    Draft = 1,
    Issued = 2,
    Cancelled = 3
}
```

### Passo 3.3 — `Errors/DomainErrors.cs`
Catálogo de erros. Comece com o necessário para o caso de uso de PDF; cresce por feature.

```csharp
using Reports.Domain.Common;

namespace Reports.Domain.Errors;

public static class DomainErrors
{
    public static class Invoice
    {
        public static readonly Error NotFound =
            new("Invoice.NotFound", "Fatura não encontrada.");
    }
}
```

### Passo 3.4 — Entidades (`Invoice`, `InvoiceItem`)
Conforme seção 4.1 do plano mestre. `Invoice.Total` é calculado (soma das linhas) — a regra "o total é a soma das linhas" mora aqui.

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

### Passo 3.5 — `ValueObjects/Money.cs` (opcional)
Se o time decidir tipar valores monetários. Imutável, com validação no construtor (não permite negativo se a regra exigir), operações de soma. Só implemente se agregar valor real ao MVP.

---

## 4. Configuração do projeto (`Reports.Domain.csproj`)

- `<Nullable>enable</Nullable>` (herdado do `Directory.Build.props`).
- LangVersion latest.
- **Nenhum** `PackageReference`.
- **Nenhum** `ProjectReference`.

---

## 5. Testes (`Reports.Domain.UnitTests` — parte do `Reports.Application.UnitTests` ou projeto próprio)

Testes unitários puros, sem mocks:
- [ ] `Invoice.Total` soma corretamente `Quantity * UnitPrice` de cada item.
- [ ] `Invoice.Total` de uma fatura sem itens é `0`.
- [ ] `Error` tem igualdade estrutural (dois `Error` com mesmo código são iguais).
- [ ] (Se `Money` for implementado) construção rejeita valores inválidos; soma funciona.

---

## 6. Definition of Done

- [ ] Projeto compila sem warnings.
- [ ] Zero pacotes NuGet e zero referências de projeto.
- [ ] `Error`, `DomainErrors.Invoice.NotFound`, `InvoiceStatus` disponíveis para a Application consumir.
- [ ] Testes unitários do Domain verdes.
- [ ] Nenhum `using` de tecnologia de borda (Dapper/QuestPDF/ASP.NET/EF).
