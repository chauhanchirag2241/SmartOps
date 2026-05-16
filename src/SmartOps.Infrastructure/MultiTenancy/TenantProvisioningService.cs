using Dapper;
using Npgsql;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    /// <summary>
    /// Tables in the <c>school</c> template schema — copied into each tenant schema on school create.
    /// </summary>
    private static readonly string[] SchoolTemplateTables =
    [
        DatabaseConfig.TableAcademicYears,
        DatabaseConfig.TableClasses,
        DatabaseConfig.TableSubjects,
        DatabaseConfig.TableTeachers,
        DatabaseConfig.TableStudents,
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentFeeConfigs,
        DatabaseConfig.TableAttendance,
        DatabaseConfig.TableSettings,
        DatabaseConfig.TableAlerts,
    ];

    private readonly IDbConnectionFactory _connectionFactory;

    public TenantProvisioningService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task ProvisionSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        string safeSchema = SanitizeSchemaName(schemaName);

        await using NpgsqlConnection connection = (NpgsqlConnection)await _connectionFactory
            .CreateGlobalConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"CREATE SCHEMA IF NOT EXISTS \"{safeSchema}\";",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        foreach (string table in SchoolTemplateTables)
        {
            if (!await TableExistsAsync(connection, DatabaseConfig.Schema_School, table, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"""
                     CREATE TABLE IF NOT EXISTS "{safeSchema}"."{table}"
                     (LIKE {DatabaseConfig.Schema_School}."{table}" INCLUDING DEFAULTS);
                     """,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
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
}
