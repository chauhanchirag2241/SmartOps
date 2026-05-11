using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Common;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity.Repositories;

public sealed class RefreshTokenRepository : BaseRepository, IRefreshTokenRepository
{
    public RefreshTokenRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    userid AS UserId,
    token AS Token,
    expiresat AS ExpiresAt,
    isrevoked AS IsRevoked,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRefreshTokens}
WHERE token = @Token AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            new CommandDefinition(sql, new { Token = token }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        if (token.Id == Guid.Empty)
        {
            token.Id = Guid.NewGuid();
        }

        DateTime utcNow = DateTime.UtcNow;
        EnsureInsertAudit(token, utcNow, token.UserId);

        string sql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRefreshTokens}
(
    id,
    userid,
    token,
    expiresat,
    isrevoked,
    isactive,
    versionno,
    createdby,
    createdon,
    updatedby,
    updatedon
)
VALUES
(
    @Id,
    @UserId,
    @Token,
    @ExpiresAt,
    @IsRevoked,
    @IsActive,
    @VersionNo,
    @CreatedBy,
    @CreatedOn,
    @UpdatedBy,
    @UpdatedOn
)
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, token, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        RefreshToken? existing = await GetByTokenAsync(token, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return;
        }

        DateTime utcNow = DateTime.UtcNow;
        Guid actor = CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty
            ? CurrentUser.UserId
            : existing.UserId;

        string sql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRefreshTokens}
SET
    isrevoked = true,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE token = @Token AND versionno = @VersionNo AND isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        int rows = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Token = token,
                    UpdatedBy = actor,
                    UpdatedOn = utcNow,
                    VersionNo = existing.VersionNo
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }
    }
}
