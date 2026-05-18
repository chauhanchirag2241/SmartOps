using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantSchemaSyncService : ITenantSchemaSyncService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TenantSchemaSyncService> _logger;

    public TenantSchemaSyncService(
        IDbConnectionFactory connectionFactory,
        ILogger<TenantSchemaSyncService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task SyncAllActiveSchoolSchemasAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> schemaNames = await GetActiveTenantSchemaNamesAsync(cancellationToken).ConfigureAwait(false);

        if (schemaNames.Count == 0)
        {
            _logger.LogInformation("No active school tenant schemas found to sync.");
            return;
        }

        _logger.LogInformation("Syncing {Count} school tenant schema(s) from template '{Template}'.", schemaNames.Count, DatabaseConfig.Schema_School);

        foreach (string schemaName in schemaNames)
        {
            try
            {
                await SyncTenantSchemaAsync(schemaName, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Tenant schema '{Schema}' synced successfully.", schemaName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync tenant schema '{Schema}'.", schemaName);
            }
        }
    }

    public async Task SyncTenantSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        string tenantSchema = SanitizeSchemaName(schemaName);
        if (string.Equals(tenantSchema, DatabaseConfig.Schema_School, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using NpgsqlConnection connection = (NpgsqlConnection)await _connectionFactory
            .CreateGlobalConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"CREATE SCHEMA IF NOT EXISTS \"{tenantSchema}\";",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (string table in SchoolSchemaCatalog.TemplateTables)
        {
            if (!await TableExistsAsync(connection, DatabaseConfig.Schema_School, table, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (!await TableExistsAsync(connection, tenantSchema, table, cancellationToken).ConfigureAwait(false))
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        $"""
                         CREATE TABLE "{tenantSchema}"."{table}"
                         (LIKE {DatabaseConfig.Schema_School}."{table}" INCLUDING DEFAULTS);
                         """,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                _logger.LogDebug("Created table {Schema}.{Table} from template.", tenantSchema, table);
                continue;
            }

            int addedColumns = await SyncMissingColumnsAsync(
                connection,
                tenantSchema,
                table,
                cancellationToken).ConfigureAwait(false);

            if (addedColumns > 0)
            {
                _logger.LogDebug(
                    "Added {Count} column(s) to {Schema}.{Table} from template.",
                    addedColumns,
                    tenantSchema,
                    table);
            }
        }

        await SyncRequiredUniqueConstraintsAsync(connection, tenantSchema, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncRequiredUniqueConstraintsAsync(
        NpgsqlConnection connection,
        string tenantSchema,
        CancellationToken cancellationToken)
    {
        foreach (TenantUniqueConstraint constraint in SchoolSchemaCatalog.RequiredUniqueConstraints)
        {
            if (!await TableExistsAsync(connection, tenantSchema, constraint.Table, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (await ConstraintExistsAsync(connection, tenantSchema, constraint.Table, constraint.Name, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (!await AllColumnsExistAsync(connection, tenantSchema, constraint.Table, constraint.Columns, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Skipping constraint {Constraint} on {Schema}.{Table}: required columns missing.",
                    constraint.Name,
                    tenantSchema,
                    constraint.Table);
                continue;
            }

            string columnList = string.Join(", ", constraint.Columns.Select(column => $"\"{column}\""));
            string ddl = $"""
ALTER TABLE "{tenantSchema}"."{constraint.Table}"
ADD CONSTRAINT "{constraint.Name}" UNIQUE ({columnList});
""";

            try
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);

                _logger.LogInformation(
                    "Added unique constraint {Constraint} on {Schema}.{Table}.",
                    constraint.Name,
                    tenantSchema,
                    constraint.Table);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation || ex.SqlState == "23505")
            {
                _logger.LogWarning(
                    ex,
                    "Could not add {Constraint} on {Schema}.{Table}: duplicate rows exist. Clean duplicates and restart API.",
                    constraint.Name,
                    tenantSchema,
                    constraint.Table);
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetActiveTenantSchemaNamesAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)await _connectionFactory
            .CreateGlobalConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        const string sql = $"""
SELECT DISTINCT LOWER(
    COALESCE(
        NULLIF(TRIM(schemaname), ''),
        'school_' || REPLACE(LOWER(TRIM(subdomain)), '-', '_')
    )
) AS schemaname
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE isactive = true
  AND TRIM(subdomain) <> ''
""";

        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !string.Equals(name, DatabaseConfig.Schema_School, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SanitizeSchemaName)
            .ToList();
    }

    private static async Task<int> SyncMissingColumnsAsync(
        NpgsqlConnection connection,
        string tenantSchema,
        string table,
        CancellationToken cancellationToken)
    {
        const string missingColumnsSql = """
SELECT
    template.column_name AS ColumnName,
    template.column_type AS ColumnType,
    template.not_null AS NotNull,
    template.column_default AS ColumnDefault
FROM (
    SELECT
        a.attname AS column_name,
        pg_catalog.format_type(a.atttypid, a.atttypmod) AS column_type,
        a.attnotnull AS not_null,
        pg_catalog.pg_get_expr(ad.adbin, ad.adrelid) AS column_default,
        a.attnum AS attnum
    FROM pg_catalog.pg_attribute a
    INNER JOIN pg_catalog.pg_class c ON a.attrelid = c.oid
    INNER JOIN pg_catalog.pg_namespace n ON c.relnamespace = n.oid
    LEFT JOIN pg_catalog.pg_attrdef ad ON ad.adrelid = a.attrelid AND ad.adnum = a.attnum
    WHERE n.nspname = @TemplateSchema
      AND c.relname = @TableName
      AND a.attnum > 0
      AND NOT a.attisdropped
) template
WHERE NOT EXISTS (
    SELECT 1
    FROM pg_catalog.pg_attribute ta
    INNER JOIN pg_catalog.pg_class tc ON ta.attrelid = tc.oid
    INNER JOIN pg_catalog.pg_namespace tn ON tc.relnamespace = tn.oid
    WHERE tn.nspname = @TenantSchema
      AND tc.relname = @TableName
      AND ta.attname = template.column_name
      AND ta.attnum > 0
      AND NOT ta.attisdropped
)
ORDER BY template.attnum
""";

        IEnumerable<MissingColumnDefinition> missingColumns = await connection.QueryAsync<MissingColumnDefinition>(
            new CommandDefinition(
                missingColumnsSql,
                new
                {
                    TemplateSchema = DatabaseConfig.Schema_School,
                    TenantSchema = tenantSchema,
                    TableName = table
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        int added = 0;
        foreach (MissingColumnDefinition column in missingColumns)
        {
            string nullability = column.NotNull ? "NOT NULL" : "NULL";
            string defaultClause = string.IsNullOrWhiteSpace(column.ColumnDefault)
                ? string.Empty
                : $" DEFAULT {column.ColumnDefault}";

            string ddl = $"""
ALTER TABLE "{tenantSchema}"."{table}"
ADD COLUMN IF NOT EXISTS "{column.ColumnName}" {column.ColumnType} {nullability}{defaultClause};
""";

            await connection.ExecuteAsync(
                new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);

            added++;
        }

        return added;
    }

    private static string SanitizeSchemaName(string schemaName)
    {
        string cleaned = schemaName.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(cleaned, "^[a-z][a-z0-9_]*$"))
        {
            throw new ArgumentException($"Invalid schema name: {schemaName}", nameof(schemaName));
        }

        return cleaned;
    }

    private static async Task<bool> ConstraintExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        string constraintName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_constraint con
                INNER JOIN pg_catalog.pg_class rel ON rel.oid = con.conrelid
                INNER JOIN pg_catalog.pg_namespace nsp ON nsp.oid = rel.relnamespace
                WHERE nsp.nspname = @Schema
                  AND rel.relname = @Table
                  AND con.conname = @ConstraintName
                  AND con.contype = 'u'
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { Schema = schema, Table = table, ConstraintName = constraintName },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<bool> AllColumnsExistAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*) = @Expected
            FROM information_schema.columns
            WHERE table_schema = @Schema
              AND table_name = @Table
              AND column_name = ANY(@Columns);
            """;

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { Schema = schema, Table = table, Columns = columns.ToArray(), Expected = columns.Count },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @Schema AND table_name = @Table
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { Schema = schema, Table = table },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class MissingColumnDefinition
    {
        public string ColumnName { get; init; } = string.Empty;
        public string ColumnType { get; init; } = string.Empty;
        public bool NotNull { get; init; }
        public string? ColumnDefault { get; init; }
    }
}
