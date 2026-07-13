using Microsoft.Extensions.Configuration;
using Reports.Infrastructure.Persistence;
using Reports.Infrastructure.Persistence.Repositories;

namespace Reports.Infrastructure.IntegrationTests;

[Collection(nameof(DatabaseCollection))]
public sealed class InvoiceRepositoryTests(DatabaseFixture fixture)
{
    // Id semeado por 03_Seeds/201_SeedDevData.sql
    private static readonly Guid SeededInvoiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private InvoiceRepository CreateRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Reports"] = fixture.ConnectionString
            })
            .Build();

        return new InvoiceRepository(new SqlConnectionFactory(configuration));
    }

    [Fact]
    public async Task GetInvoiceReportAsync_retorna_read_model_com_header_e_itens_ordenados()
    {
        var repository = CreateRepository();

        var report = await repository.GetInvoiceReportAsync(SeededInvoiceId);

        Assert.NotNull(report);
        Assert.Equal("INV-2026-000001", report!.Number);
        Assert.Equal(new DateOnly(2026, 7, 1), report.IssueDate);
        Assert.Equal("ACME Ltda", report.CustomerName);

        // 3 itens, na ordem de LineNumber
        Assert.Collection(report.Items,
            i => Assert.Equal("Licença de software — plano anual", i.Description),
            i => Assert.Equal("Horas de consultoria", i.Description),
            i => Assert.Equal("Suporte premium (mensal)", i.Description));

        // Total = 2*1200 + 10*350 + 1*499.90
        Assert.Equal(6399.90m, report.Total);
    }

    [Fact]
    public async Task GetInvoiceReportAsync_retorna_null_para_id_inexistente()
    {
        var repository = CreateRepository();

        var report = await repository.GetInvoiceReportAsync(Guid.NewGuid());

        Assert.Null(report);
    }
}
