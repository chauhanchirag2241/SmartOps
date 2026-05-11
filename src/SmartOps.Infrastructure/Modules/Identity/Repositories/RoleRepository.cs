using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Common;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity.Repositories;

public sealed class RoleRepository : BaseRepository
{
    public RoleRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
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
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE id = @Id AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
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
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE name = @Name AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateAsync(ApplicationRole role, CancellationToken cancellationToken = default)
    {
        if (role.Id == Guid.Empty)
        {
            role.Id = Guid.NewGuid();
        }

        DateTime utcNow = DateTime.UtcNow;
        EnsureInsertAudit(role, utcNow);

        string sql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
(
    id,
    name,
    description,
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
    @Name,
    @Description,
    @IsActive,
    @VersionNo,
    @CreatedBy,
    @CreatedOn,
    @UpdatedBy,
    @UpdatedOn
)
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, role, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApplicationRole role, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actor = ResolveUpdateActor();

        string sql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
SET
    name = @Name,
    description = @Description,
    isactive = @IsActive,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @Id AND versionno = @VersionNo AND isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        int rows = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    role.Id,
                    role.Name,
                    role.Description,
                    role.IsActive,
                    UpdatedBy = actor,
                    UpdatedOn = utcNow,
                    VersionNo = role.VersionNo
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }

        role.VersionNo += 1;
        role.UpdatedBy = actor;
        role.UpdatedOn = utcNow;
    }
}
