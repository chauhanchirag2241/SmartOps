using SmartOps.Application.Modules.Authorization.DTOs;

namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IUserScopeService
{
    Task<UserScopeDto> GetScopeAsync(Guid userId, Guid? schoolId, CancellationToken cancellationToken = default);

    Task<int> GetScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default);

    Task BumpScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default);
}
