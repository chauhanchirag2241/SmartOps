namespace SmartOps.Application.Abstractions;

/// <summary>
/// Runs FluentMigrator scripts for the platform (global) and per-school databases.
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>Applies pending migrations from <c>Migrations/Global</c> on the platform database.</summary>
    Task MigrateGlobalDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies pending migrations from <c>Migrations/School</c> on one school database.</summary>
    Task MigrateSchoolDatabaseAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Applies pending school migrations on every active dedicated school database.</summary>
    Task MigrateAllSchoolDatabasesAsync(CancellationToken cancellationToken = default);
}
