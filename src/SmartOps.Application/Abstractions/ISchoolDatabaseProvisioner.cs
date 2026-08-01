namespace SmartOps.Application.Abstractions;

public interface ISchoolDatabaseProvisioner
{
    /// <summary>
    /// Creates a dedicated PostgreSQL database for a school, runs school migrations,
    /// seeds defaults, and returns the connection string.
    /// </summary>
    Task<(string DatabaseName, string ConnectionString)> ProvisionAsync(
        Guid schoolId,
        string subdomain,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops a dedicated school database if it exists (used to roll back a failed create).
    /// </summary>
    Task DropDatabaseIfExistsAsync(string databaseName, CancellationToken cancellationToken = default);
}
