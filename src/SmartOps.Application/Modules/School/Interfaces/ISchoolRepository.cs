using SchoolEntity = SmartOps.Domain.Modules.School.Entities.School;

namespace SmartOps.Application.Modules.School.Interfaces;

public interface ISchoolRepository
{
    Task<SchoolEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IList<SchoolEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SchoolEntity?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);

    Task CreateAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task UpdateAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, Guid deletedBy, CancellationToken cancellationToken = default);
}
