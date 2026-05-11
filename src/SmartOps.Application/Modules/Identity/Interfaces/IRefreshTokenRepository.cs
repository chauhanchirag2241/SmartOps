using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
