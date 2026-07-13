using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Reports.Infrastructure.IntegrationTests;

/// <summary>
/// Sobe um SQL Server real em Docker (Testcontainers) e aplica os mesmos scripts
/// versionados em <c>Persistence/Database</c> — do zero, na ordem de numeração.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Connection string já apontando para o banco <c>ReportsDb</c>.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Aplica todos os scripts .sql duas vezes: a segunda passagem prova a idempotência.
        await DeployScriptsAsync();
        await DeployScriptsAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "ReportsDb"
        }.ConnectionString;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

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

[CollectionDefinition(nameof(DatabaseCollection))]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
