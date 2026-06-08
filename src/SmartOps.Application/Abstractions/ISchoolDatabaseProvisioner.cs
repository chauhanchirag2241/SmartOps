namespace SmartOps.Application.Abstractions;

public interface ISchoolDatabaseProvisioner
{
    /// <summary>
    /// Creates a dedicated PostgreSQL database for a school, copies schema structure,
    /// seeds defaults, and returns the connection string.
    /// </summary>
    Task<(string DatabaseName, string ConnectionString)> ProvisionAsync(
        Guid schoolId,
        string subdomain,
        CancellationToken cancellationToken = default);
}
