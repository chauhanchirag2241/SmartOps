using System.Data;

namespace SmartOps.Application.Abstractions;

/// <summary>
/// Opens a dedicated school database connection. Caller owns the connection and must dispose it.
/// Settings, branches, and school identity live on the school DB (<c>man</c> schema), not platform <c>global</c>.
/// </summary>
public interface ISchoolDbConnectionFactory
{
    /// <summary>
    /// Looks up <c>global.schools.connectionstring</c> and opens that school database.
    /// </summary>
    Task<IDbConnection> OpenBySchoolIdAsync(Guid schoolId, CancellationToken cancellationToken = default);

    /// <summary>Opens a school database with an explicit connection string.</summary>
    Task<IDbConnection> OpenAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Returns the school's dedicated connection string from the platform registry, or null.</summary>
    Task<string?> GetConnectionStringAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
