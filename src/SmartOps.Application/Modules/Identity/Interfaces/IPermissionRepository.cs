using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);
}
