using System.Data;
using Npgsql;
using SmartOps.Application.Common.Abstractions;

namespace SmartOps.Infrastructure.Persistence.Context;

public sealed class DapperContext : IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private NpgsqlConnection? _connection;

    public DapperContext(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IDbConnection> GetGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            _connection = (NpgsqlConnection)await _connectionFactory
                .CreateGlobalConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }
}
