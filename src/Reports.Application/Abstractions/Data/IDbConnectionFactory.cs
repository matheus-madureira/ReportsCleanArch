using System.Data;

namespace Reports.Application.Abstractions.Data;

public interface IDbConnectionFactory
{
    ValueTask<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
