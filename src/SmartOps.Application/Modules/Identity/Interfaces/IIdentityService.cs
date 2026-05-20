using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Identity;
using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IIdentityService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<UserPermissionResponseDto>> GetUserPermissionsAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default);

    Task<Result<UserScopeDto>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default);
}
