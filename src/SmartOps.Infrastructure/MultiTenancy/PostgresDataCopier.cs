using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Copies row data from source tables into target tables (same schema/table names).
/// </summary>
internal sealed class PostgresDataCopier
{
    private readonly ILogger _logger;

    public PostgresDataCopier(ILogger logger)
    {
        _logger = logger;
    }

    public async Task CopyTableDataAsync(
        NpgsqlConnection source,
        string sourceSchema,
        NpgsqlConnection target,
        string targetSchema,
        string table,
        string? whereClause = null,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(source, sourceSchema, table, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!await TableExistsAsync(target, targetSchema, table, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        string selectSql = $"""SELECT * FROM "{sourceSchema}"."{table}" """;
        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            selectSql += $" WHERE {whereClause}";
        }

        IEnumerable<dynamic> rows = await source.QueryAsync(
            new CommandDefinition(selectSql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

        List<dynamic> batch = rows.ToList();
        if (batch.Count == 0)
        {
            _logger.LogDebug("No rows to copy for {Schema}.{Table}.", sourceSchema, table);
            return;
        }

        IDictionary<string, object> first = (IDictionary<string, object>)batch[0];
        string columns = string.Join(", ", first.Keys.Select(k => $"\"{k}\""));
        string values = string.Join(", ", first.Keys.Select(k => $"@{k}"));

        string insertSql = $"""
INSERT INTO "{targetSchema}"."{table}" ({columns})
VALUES ({values})
ON CONFLICT DO NOTHING;
""";

        await target.ExecuteAsync(
            new CommandDefinition(insertSql, batch, cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation(
            "Copied {Count} row(s) into {Schema}.{Table}.",
            batch.Count,
            targetSchema,
            table);
    }

    public async Task CopyAllRowsAsync(
        NpgsqlConnection source,
        string sourceSchema,
        NpgsqlConnection target,
        string targetSchema,
        string table,
        CancellationToken cancellationToken = default)
    {
        await CopyTableDataAsync(
            source,
            sourceSchema,
            target,
            targetSchema,
            table,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = @Schema AND table_name = @Table
);
""";
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Schema = schema, Table = table }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
