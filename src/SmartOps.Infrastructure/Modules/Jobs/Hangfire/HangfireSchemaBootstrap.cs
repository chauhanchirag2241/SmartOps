using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

/// <summary>
/// Hangfire.PostgreSql owns its schema (not FluentMigrator). After a fresh DB recreate,
/// install tables before the BackgroundJobServer / recurring-job sync touches hangfire.lock.
/// </summary>
public static class HangfireSchemaBootstrap
{
    public const string SchemaName = "hangfire";

    public static void EnsureInstalled(IConfiguration configuration, ILogger logger)
    {
        string? cs = configuration.GetConnectionString("GlobalDatabase")
            ?? configuration.GetConnectionString("GlobalDb")
            ?? configuration.GetConnectionString("PlatformDb");

        if (string.IsNullOrWhiteSpace(cs))
        {
            logger.LogWarning("Skipping Hangfire schema install — global connection string is missing.");
            return;
        }

        try
        {
            using NpgsqlConnection connection = new(cs);
            connection.Open();

            using (NpgsqlCommand createSchema = new($"CREATE SCHEMA IF NOT EXISTS {SchemaName};", connection))
            {
                createSchema.ExecuteNonQuery();
            }

            PostgreSqlObjectsInstaller.Install(connection, SchemaName);
            logger.LogInformation("Hangfire schema '{Schema}' is ready.", SchemaName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install Hangfire schema '{Schema}'.", SchemaName);
            throw;
        }
    }
}
