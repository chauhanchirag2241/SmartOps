using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity.Repositories;

public sealed class PermissionRepository : BaseRepository, IPermissionRepository
{
    public PermissionRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    name AS Name,
    description AS Description,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions}
WHERE isactive = true
ORDER BY name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Permission> rows = await connection.QueryAsync<Permission>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }
}
