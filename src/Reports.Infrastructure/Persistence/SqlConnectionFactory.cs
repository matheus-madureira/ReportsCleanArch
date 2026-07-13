using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Reports.Application.Abstractions.Data;
using System.Data;

namespace Reports.Infrastructure.Persistence;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public async ValueTask<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(configuration.GetConnectionString("Reports"));

        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}
