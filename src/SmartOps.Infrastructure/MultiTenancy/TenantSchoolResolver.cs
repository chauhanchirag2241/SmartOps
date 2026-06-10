using SmartOps.Application.Abstractions;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Modules.School;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantSchoolResolver : ITenantSchoolResolver
{
    private readonly ISchoolRepository _schoolRepository;

    public TenantSchoolResolver(ISchoolRepository schoolRepository)
    {
        _schoolRepository = schoolRepository;
    }

    public Task<SchoolEntity?> ResolveBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default)
    {
        return _schoolRepository.GetSchoolBySubdomainAsync(subdomain, cancellationToken);
    }

    public Task<SchoolEntity?> ResolveBySchoolIdAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        return _schoolRepository.GetSchoolByIdAsync(schoolId, cancellationToken);
    }
}
