using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Copies table structures from a source PostgreSQL database/schema into a target database/schema.
/// </summary>
internal sealed class PostgresSchemaCopier
{
    private readonly ILogger _logger;

    public PostgresSchemaCopier(ILogger logger)
    {
        _logger = logger;
    }

    public async Task CopyTablesAsync(
        NpgsqlConnection source,
        string sourceSchema,
        NpgsqlConnection target,
        string targetSchema,
        IReadOnlyList<string> tables,
        CancellationToken cancellationToken = default)
    {
        await target.ExecuteAsync(
            new CommandDefinition(
                $"""CREATE SCHEMA IF NOT EXISTS "{targetSchema}";""",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (string table in tables)
        {
            if (!await TableExistsAsync(source, sourceSchema, table, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Skipping table {Schema}.{Table} — not found on source.",
                    sourceSchema,
                    table);
                continue;
            }

            if (await TableExistsAsync(target, targetSchema, table, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogDebug("Table {Schema}.{Table} already exists on target.", targetSchema, table);
                continue;
            }

            string createSql = await BuildCreateTableSqlAsync(
                source,
                sourceSchema,
                targetSchema,
                table,
                cancellationToken).ConfigureAwait(false);

            await target.ExecuteAsync(
                new CommandDefinition(createSql, cancellationToken: cancellationToken)).ConfigureAwait(false);

            _logger.LogInformation("Created table {Schema}.{Table} on target database.", targetSchema, table);
        }

        foreach (string table in tables)
        {
            if (!await TableExistsAsync(target, targetSchema, table, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await CopyPrimaryKeyAsync(
                source,
                sourceSchema,
                target,
                targetSchema,
                table,
                cancellationToken).ConfigureAwait(false);
            await CopyUniqueConstraintsAsync(
                source,
                sourceSchema,
                target,
                targetSchema,
                table,
                cancellationToken).ConfigureAwait(false);
        }
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

    private static async Task<string> BuildCreateTableSqlAsync(
        NpgsqlConnection source,
        string sourceSchema,
        string targetSchema,
        string table,
        CancellationToken cancellationToken)
    {
        const string columnSql = """
SELECT
    column_name AS ColumnName,
    data_type AS DataType,
    udt_name AS UdtName,
    character_maximum_length AS CharMaxLength,
    numeric_precision AS NumericPrecision,
    numeric_scale AS NumericScale,
    is_nullable AS IsNullable,
    column_default AS ColumnDefault
FROM information_schema.columns
WHERE table_schema = @Schema AND table_name = @Table
ORDER BY ordinal_position;
""";

        IEnumerable<ColumnRow> columns = await source.QueryAsync<ColumnRow>(
            new CommandDefinition(
                columnSql,
                new { Schema = sourceSchema, Table = table },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        List<string> parts = [];
        foreach (ColumnRow col in columns)
        {
            string type = MapPgType(col);
            string nullable = col.IsNullable == "YES" ? "NULL" : "NOT NULL";
            string defaultClause = string.IsNullOrWhiteSpace(col.ColumnDefault)
                ? string.Empty
                : $" DEFAULT {col.ColumnDefault}";
            parts.Add($"""  "{col.ColumnName}" {type}{defaultClause} {nullable}""");
        }

        return $"""
CREATE TABLE "{targetSchema}"."{table}" (
{string.Join(",\n", parts)}
);
""";
    }

    private static string MapPgType(ColumnRow col)
    {
        return col.DataType switch
        {
            "character varying" => col.CharMaxLength is > 0
                ? $"varchar({col.CharMaxLength})"
                : "varchar",
            "character" => col.CharMaxLength is > 0
                ? $"char({col.CharMaxLength})"
                : "char",
            "numeric" when col.NumericPrecision is > 0 && col.NumericScale is >= 0
                => $"numeric({col.NumericPrecision},{col.NumericScale})",
            "timestamp with time zone" => "timestamptz",
            "timestamp without time zone" => "timestamp",
            "double precision" => "double precision",
            "USER-DEFINED" => col.UdtName switch
            {
                "uuid" => "uuid",
                "jsonb" => "jsonb",
                _ => col.UdtName
            },
            _ => col.DataType
        };
    }

    private async Task CopyPrimaryKeyAsync(
        NpgsqlConnection source,
        string sourceSchema,
        NpgsqlConnection target,
        string targetSchema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT kcu.column_name AS ColumnName
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type = 'PRIMARY KEY'
  AND tc.table_schema = @Schema
  AND tc.table_name = @Table
ORDER BY kcu.ordinal_position;
""";

        List<string> columns = (await source.QueryAsync<string>(
            new CommandDefinition(
                sql,
                new { Schema = sourceSchema, Table = table },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (columns.Count == 0)
        {
            return;
        }

        string constraintName = $"pk_{table}";
        string columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));
        string ddl = $"""
ALTER TABLE "{targetSchema}"."{table}"
    ADD CONSTRAINT "{constraintName}" PRIMARY KEY ({columnList});
""";

        try
        {
            await target.ExecuteAsync(
                new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateObject)
        {
            _logger.LogDebug("Primary key already exists on {Schema}.{Table}.", targetSchema, table);
        }
    }

    private async Task CopyUniqueConstraintsAsync(
        NpgsqlConnection source,
        string sourceSchema,
        NpgsqlConnection target,
        string targetSchema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT tc.constraint_name AS ConstraintName, kcu.column_name AS ColumnName
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type = 'UNIQUE'
  AND tc.table_schema = @Schema
  AND tc.table_name = @Table
ORDER BY tc.constraint_name, kcu.ordinal_position;
""";

        IEnumerable<ConstraintColumnRow> rows = await source.QueryAsync<ConstraintColumnRow>(
            new CommandDefinition(
                sql,
                new { Schema = sourceSchema, Table = table },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (IGrouping<string, ConstraintColumnRow> group in rows.GroupBy(r => r.ConstraintName))
        {
            string columnList = string.Join(", ", group.Select(r => $"\"{r.ColumnName}\""));
            string ddl = $"""
ALTER TABLE "{targetSchema}"."{table}"
    ADD CONSTRAINT "{group.Key}" UNIQUE ({columnList});
""";
            try
            {
                await target.ExecuteAsync(
                    new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateObject)
            {
                _logger.LogDebug("Constraint {Constraint} already exists on {Table}.", group.Key, table);
            }
        }
    }

    private sealed class ColumnRow
    {
        public string ColumnName { get; set; } = string.Empty;

        public string DataType { get; set; } = string.Empty;

        public string UdtName { get; set; } = string.Empty;

        public int? CharMaxLength { get; set; }

        public int? NumericPrecision { get; set; }

        public int? NumericScale { get; set; }

        public string IsNullable { get; set; } = "YES";

        public string? ColumnDefault { get; set; }
    }

    private sealed class ConstraintColumnRow
    {
        public string ConstraintName { get; set; } = string.Empty;

        public string ColumnName { get; set; } = string.Empty;
    }
}
