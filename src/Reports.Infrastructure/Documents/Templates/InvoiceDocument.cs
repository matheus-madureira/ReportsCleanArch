using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reports.Application.Invoices.GetInvoicePdf;

namespace Reports.Infrastructure.Documents.Templates;

public sealed class InvoiceDocument(InvoiceReportModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
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
