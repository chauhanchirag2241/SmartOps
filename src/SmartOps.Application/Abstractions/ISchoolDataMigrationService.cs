namespace SmartOps.Application.Abstractions;

public interface ISchoolDataMigrationService
{
    /// <summary>
    /// Migrates an existing schema-tenanted school to a dedicated database.
    /// </summary>
    Task MigrateSchoolToDedicatedDatabaseAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
