using System.Data;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.Persistence.Context;

public sealed class DapperContext : IDisposable, IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ITenantSchemaProvider _tenantSchemaProvider;
    private readonly TenantContext _tenantContext;
    private NpgsqlConnection? _connection;
    private NpgsqlConnection? _platformConnection;
    private string? _operationalBindingKey;

    public DapperContext(
        IDbConnectionFactory connectionFactory,
        ITenantSchemaProvider tenantSchemaProvider,
        TenantContext tenantContext)
    {
        _connectionFactory = connectionFactory;
        _tenantSchemaProvider = tenantSchemaProvider;
        _tenantContext = tenantContext;
    }

    public string OperationalSchema => _tenantSchemaProvider.GetOperationalSchema();

    public async Task<IDbConnection> GetPlatformConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.UsesDedicatedDatabase)
        {
            return await GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_platformConnection is null || IsConnectionDisposed(_platformConnection))
        {
            _platformConnection = (NpgsqlConnection)await _connectionFactory
                .CreatePlatformConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return _platformConnection;
    }

    public async Task<IDbConnection> GetGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        string bindingKey = GetOperationalBindingKey();

        if (_connection is not null
            && (!string.Equals(_operationalBindingKey, bindingKey, StringComparison.Ordinal)
                || IsConnectionDisposed(_connection)))
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_connection is null)
        {
            if (_tenantContext.UsesDedicatedDatabase)
            {
                _connection = (NpgsqlConnection)await _connectionFactory
                    .CreateConnectionAsync(_tenantContext.ConnectionString!, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _connection = (NpgsqlConnection)await _connectionFactory
                    .CreatePlatformConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            _operationalBindingKey = bindingKey;
        }

        return _connection;
    }

    private string GetOperationalBindingKey()
    {
        if (_tenantContext.UsesDedicatedDatabase)
        {
            return "school:" + _tenantContext.ConnectionString;
        }

        return "platform";
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

    public void Dispose()
    {
        if (_connection is not null)
        {
            _connection.Dispose();
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_platformConnection is not null)
        {
            _platformConnection.Dispose();
            _platformConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_platformConnection is not null)
        {
            await _platformConnection.DisposeAsync().ConfigureAwait(false);
            _platformConnection = null;
        }
    }
}
