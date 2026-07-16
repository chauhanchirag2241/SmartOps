using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Branch.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Branch;

public sealed class BranchRepository : BaseRepository, IBranchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly TenantContext _tenantContext;

    public BranchRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IDbConnectionFactory connectionFactory,
        TenantContext tenantContext)
        : base(context, currentUser)
    {
        _connectionFactory = connectionFactory;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<BranchDropdownItemDto>> GetBranchesBySchoolAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id AS Id, name AS Name, isheadoffice AS IsHeadOffice, false AS IsDefault
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolBranches}
WHERE schoolid = @SchoolId AND isactive = true
ORDER BY isheadoffice DESC, name ASC;
""";
        IEnumerable<BranchDropdownItemDto> rows = await connection
            .QueryAsync<BranchDropdownItemDto>(sql, new { SchoolId = schoolId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetUserBranchIdsAsync(
        Guid userId,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await GetIdentityConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT branchid
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings}
WHERE userid = @UserId AND schoolid = @SchoolId AND isactive = true
ORDER BY isdefault DESC;
""";
        IEnumerable<Guid> rows = await connection
            .QueryAsync<Guid>(sql, new { UserId = userId, SchoolId = schoolId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<BranchDropdownItemDto>> GetUserBranchesAsync(
        Guid userId,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await GetIdentityConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT b.id AS Id, b.name AS Name, b.isheadoffice AS IsHeadOffice, m.isdefault AS IsDefault
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings} m
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolBranches} b ON b.id = m.branchid
WHERE m.userid = @UserId AND m.schoolid = @SchoolId AND m.isactive = true AND b.isactive = true
ORDER BY m.isdefault DESC, b.isheadoffice DESC, b.name ASC;
""";
        IEnumerable<BranchDropdownItemDto> rows = await connection
            .QueryAsync<BranchDropdownItemDto>(sql, new { UserId = userId, SchoolId = schoolId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SetUserBranchesAsync(
        Guid userId,
        Guid schoolId,
        IReadOnlyList<Guid> branchIds,
        Guid? defaultBranchId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await GetIdentityConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid actor = ResolveInsertActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        HashSet<Guid> desired = branchIds.Where(id => id != Guid.Empty).ToHashSet();
        Guid? resolvedDefault = defaultBranchId is not null && desired.Contains(defaultBranchId.Value)
            ? defaultBranchId
            : desired.FirstOrDefault();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string existingSql = $"""
SELECT branchid FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings}
WHERE userid = @UserId AND schoolid = @SchoolId;
""";
            List<Guid> existing = (await conn.QueryAsync<Guid>(
                existingSql,
                new { UserId = userId, SchoolId = schoolId },
                tx).ConfigureAwait(false)).ToList();

            foreach (Guid branchId in existing.Where(id => !desired.Contains(id)))
            {
                await conn.ExecuteAsync(
                    $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings}
SET isactive = false, updatedby = @Actor, updatedon = @Now
WHERE userid = @UserId AND schoolid = @SchoolId AND branchid = @BranchId;
""",
                    new { UserId = userId, SchoolId = schoolId, BranchId = branchId, Actor = actor, Now = now },
                    tx).ConfigureAwait(false);
            }

            foreach (Guid branchId in desired)
            {
                bool isDefault = resolvedDefault.HasValue && resolvedDefault.Value == branchId;
                await conn.ExecuteAsync(
                    $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings}
    (id, userid, branchid, schoolid, isdefault, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (gen_random_uuid(), @UserId, @BranchId, @SchoolId, @IsDefault, true, 1, @Actor, @Now, @Actor, @Now)
ON CONFLICT (userid, branchid) DO UPDATE SET
    isactive = true,
    isdefault = @IsDefault,
    schoolid = @SchoolId,
    updatedby = @Actor,
    updatedon = @Now;
""",
                    new
                    {
                        UserId = userId,
                        BranchId = branchId,
                        SchoolId = schoolId,
                        IsDefault = isDefault,
                        Actor = actor,
                        Now = now
                    },
                    tx).ConfigureAwait(false);
            }

            if (resolvedDefault.HasValue)
            {
                await conn.ExecuteAsync(
                    $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserBranchMappings}
SET isdefault = (branchid = @DefaultBranchId), updatedby = @Actor, updatedon = @Now
WHERE userid = @UserId AND schoolid = @SchoolId AND isactive = true;
""",
                    new { UserId = userId, SchoolId = schoolId, DefaultBranchId = resolvedDefault.Value, Actor = actor, Now = now },
                    tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        await SyncUserBranchMappingsToPlatformAsync(userId, schoolId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncBranchesToSchoolDatabaseAsync(
        Guid schoolId,
        IReadOnlyList<SchoolBranchEntity> branches,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.UsesDedicatedDatabase)
        {
            return;
        }

        IDbConnection platform = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        IDbConnection schoolDb = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string g = DatabaseConfig.Schema_Global;
        Guid actor = ResolveInsertActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (SchoolBranchEntity branch in branches)
        {
            await schoolDb.ExecuteAsync(
                $"""
INSERT INTO {g}.{DatabaseConfig.TableSchoolBranches}
    (id, schoolid, name, email, address, isheadoffice, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @SchoolId, @Name, @Email, @Address, @IsHeadOffice, true, 1, @Actor, @Now, @Actor, @Now)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    email = EXCLUDED.email,
    address = EXCLUDED.address,
    isheadoffice = EXCLUDED.isheadoffice,
    isactive = true,
    updatedby = @Actor,
    updatedon = @Now;
""",
                new
                {
                    branch.Id,
                    SchoolId = schoolId,
                    branch.Name,
                    branch.Email,
                    branch.Address,
                    branch.IsHeadOffice,
                    Actor = actor,
                    Now = now
                }).ConfigureAwait(false);
        }

        _ = platform;
    }

    private async Task SyncUserBranchMappingsToPlatformAsync(
        Guid userId,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.UsesDedicatedDatabase)
        {
            return;
        }

        IDbConnection schoolDb = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IDbConnection platform = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        string g = DatabaseConfig.Schema_Global;

        string selectSql = $"""
SELECT id, userid, branchid, schoolid, isdefault, isactive, versionno, createdby, createdon, updatedby, updatedon
FROM {g}.{DatabaseConfig.TableUserBranchMappings}
WHERE userid = @UserId AND schoolid = @SchoolId;
""";
        IEnumerable<UserBranchMappingEntity> mappings = await schoolDb
            .QueryAsync<UserBranchMappingEntity>(selectSql, new { UserId = userId, SchoolId = schoolId })
            .ConfigureAwait(false);

        foreach (UserBranchMappingEntity mapping in mappings)
        {
            await platform.ExecuteAsync(
                $"""
INSERT INTO {g}.{DatabaseConfig.TableUserBranchMappings}
    (id, userid, branchid, schoolid, isdefault, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @UserId, @BranchId, @SchoolId, @IsDefault, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
ON CONFLICT (userid, branchid) DO UPDATE SET
    isdefault = EXCLUDED.isdefault,
    isactive = EXCLUDED.isactive,
    schoolid = EXCLUDED.schoolid,
    updatedby = EXCLUDED.updatedby,
    updatedon = EXCLUDED.updatedon;
""",
                mapping).ConfigureAwait(false);
        }
    }

    private async Task<IDbConnection> GetIdentityConnectionAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.UsesDedicatedDatabase)
        {
            return await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        return await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
