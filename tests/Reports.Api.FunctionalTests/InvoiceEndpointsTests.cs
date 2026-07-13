using System.Net;
using System.Text;
using System.Text.Json;

namespace Reports.Api.FunctionalTests;

[Collection(nameof(ReportsApiCollection))]
public sealed class InvoiceEndpointsTests(ReportsApiFactory factory)
{
    // Id semeado por 03_Seeds/201_SeedDevData.sql
    private static readonly Guid SeededInvoiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task GetPdf_com_id_semeado_retorna_200_com_pdf()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/invoices/{SeededInvoiceId}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);

        // Todo PDF começa com o header mágico "%PDF".
        var header = Encoding.ASCII.GetString(bytes, 0, 4);
        Assert.Equal("%PDF", header);
    }

    [Fact]
    public async Task GetPdf_com_id_inexistente_retorna_404_problem_details()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/invoices/{Guid.NewGuid()}/pdf");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invoice.NotFound", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetPdf_com_id_malformado_nao_bate_na_rota()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/invoices/nao-e-um-guid/pdf");

        // A restrição de rota :guid rejeita o valor antes de chegar no handler.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
