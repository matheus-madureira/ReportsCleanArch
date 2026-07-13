using Dapper;
using Reports.Application.Abstractions.Data;
using Reports.Application.Invoices.GetInvoicePdf;
using System.Data;

namespace Reports.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository(IDbConnectionFactory connectionFactory) : IInvoiceRepository
{
    public async Task<InvoiceReportModel?> GetInvoiceReportAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            "dbo.usp_Invoice_GetReport",
            new { InvoiceId = invoiceId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);

        var header = await multi.ReadSingleOrDefaultAsync<InvoiceHeaderRow>();

        if (header is null)
        {
            return null;
        }

        var items = (await multi.ReadAsync<InvoiceItemRow>()).ToList();

        return new InvoiceReportModel(
            header.Id,
            header.Number,
            DateOnly.FromDateTime(header.IssueDate),
            header.CustomerName,
            header.CustomerAddress,
            items.Select(i => new InvoiceReportItem(i.Description, i.Quantity, i.UnitPrice)).ToList());
    }

    private sealed record InvoiceHeaderRow(
        Guid Id,
        string Number,
        DateTime IssueDate,
        string CustomerName,
        string CustomerAddress);

    private sealed record InvoiceItemRow(string Description, int Quantity, decimal UnitPrice);
}
