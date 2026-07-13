using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace Reports.Api.FunctionalTests;

/// <summary>
/// Sobe a API em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) apontando para um
/// SQL Server real em Docker (Testcontainers), com os mesmos scripts versionados em
/// <c>Persistence/Database</c>. Só a connection string é sobrescrita — o restante do
/// composition root (<c>Program.cs</c>) roda de verdade.
/// </summary>
public sealed class ReportsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await DeployScriptsAsync();

        _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "ReportsDb"
        }.ConnectionString;
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Reports"] = _connectionString
            });
        });
    }

    private async Task DeployScriptsAsync()
    {
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        foreach (var file in ScriptLocator.GetOrderedScripts())
        {
            var sql = await File.ReadAllTextAsync(file);
            foreach (var batch in SplitBatches(sql))
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    // sqlcmd separa lotes por uma linha contendo apenas "GO".
    private static IEnumerable<string> SplitBatches(string sql)
    {
        var lines = sql.Split('\n');
        var current = new List<string>();

        foreach (var line in lines)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                var batch = string.Join('\n', current).Trim();
                if (batch.Length > 0)
                {
                    yield return batch;
                }

                current.Clear();
            }
            else
            {
                current.Add(line);
            }
        }

        var tail = string.Join('\n', current).Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }
}

[CollectionDefinition(nameof(ReportsApiCollection))]
public sealed class ReportsApiCollection : ICollectionFixture<ReportsApiFactory>;
