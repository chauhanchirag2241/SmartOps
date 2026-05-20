using System.Data;
using Npgsql;
using SmartOps.Application.Common.Abstractions;

namespace SmartOps.Infrastructure.Persistence.Context;

public sealed class DapperContext : IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ITenantSchemaProvider _tenantSchemaProvider;
    private NpgsqlConnection? _connection;

    public DapperContext(IDbConnectionFactory connectionFactory, ITenantSchemaProvider tenantSchemaProvider)
    {
        _connectionFactory = connectionFactory;
        _tenantSchemaProvider = tenantSchemaProvider;
    }

    public string OperationalSchema => _tenantSchemaProvider.GetOperationalSchema();

    public async Task<IDbConnection> GetGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || IsConnectionDisposed(_connection))
        {
            _connection = (NpgsqlConnection)await _connectionFactory
                .CreateGlobalConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return _connection;
    }

    /// <summary>
    /// Scoped connection is owned by DapperContext — callers must not dispose it.
    /// </summary>
    private static bool IsConnectionDisposed(IDbConnection connection)
    {
        if (connection is not NpgsqlConnection npgsql)
        {
            return false;
        }

        try
        {
            _ = npgsql.State;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    public Task<IDbConnection> GetOperationalConnectionAsync(CancellationToken cancellationToken = default)
    {
        return GetGlobalConnectionAsync(cancellationToken);
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
