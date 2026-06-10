using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Abstractions;

public interface ITenantSchoolResolver
{
    Task<SchoolEntity?> ResolveBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);

    Task<SchoolEntity?> ResolveBySchoolIdAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
