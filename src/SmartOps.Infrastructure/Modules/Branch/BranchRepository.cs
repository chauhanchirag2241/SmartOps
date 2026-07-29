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
        await using var schoolDb = await OpenSchoolConnectionAsync(schoolId, cancellationToken).ConfigureAwait(false);
        string schema = DatabaseConfig.Schema_Man;
        string sql = $"""
SELECT id AS Id, name AS Name, isheadoffice AS IsHeadOffice, false AS IsDefault
FROM {schema}.{DatabaseConfig.TableSchoolBranches}
WHERE schoolid = @SchoolId AND isactive = true
ORDER BY isheadoffice DESC, name ASC;
""";
        IEnumerable<BranchDropdownItemDto> rows = await schoolDb
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
        string schema = IdentitySchema;
        string sql = $"""
SELECT branchid
FROM {schema}.{DatabaseConfig.TableUserBranchMappings}
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
        string schema = IdentitySchema;
        string sql = $"""
SELECT b.id AS Id, b.name AS Name, b.isheadoffice AS IsHeadOffice, m.isdefault AS IsDefault
FROM {schema}.{DatabaseConfig.TableUserBranchMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableSchoolBranches} b ON b.id = m.branchid
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
        string schema = IdentitySchema;
        Guid actor = ResolveInsertActor();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        HashSet<Guid> desired = branchIds.Where(id => id != Guid.Empty).ToHashSet();
        Guid? resolvedDefault = defaultBranchId is not null && desired.Contains(defaultBranchId.Value)
            ? defaultBranchId
            : desired.FirstOrDefault();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string existingSql = $"""
SELECT branchid FROM {schema}.{DatabaseConfig.TableUserBranchMappings}
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
UPDATE {schema}.{DatabaseConfig.TableUserBranchMappings}
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
INSERT INTO {schema}.{DatabaseConfig.TableUserBranchMappings}
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
UPDATE {schema}.{DatabaseConfig.TableUserBranchMappings}
SET isdefault = (branchid = @DefaultBranchId), updatedby = @Actor, updatedon = @Now
WHERE userid = @UserId AND schoolid = @SchoolId AND isactive = true;
""",
                    new { UserId = userId, SchoolId = schoolId, DefaultBranchId = resolvedDefault.Value, Actor = actor, Now = now },
                    tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Retired: schoolbranches are source-of-truth on the school DB (man schema). No platform→school sync.
    /// </summary>
    public Task SyncBranchesToSchoolDatabaseAsync(
        Guid schoolId,
        IReadOnlyList<SchoolBranchEntity> branches,
        CancellationToken cancellationToken = default)
    {
        _ = schoolId;
        _ = branches;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private async Task<Npgsql.NpgsqlConnection> OpenSchoolConnectionAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        IDbConnection platform = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string? connectionString = await platform.QuerySingleOrDefaultAsync<string>(
            $"""
SELECT connectionstring
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE id = @SchoolId AND isactive = true
LIMIT 1;
""",
            new { SchoolId = schoolId }).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"School {schoolId} has no dedicated database connection string; schoolbranches live on the school DB.");
        }

        return (Npgsql.NpgsqlConnection)await _connectionFactory
            .CreateConnectionAsync(connectionString, cancellationToken)
            .ConfigureAwait(false);
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
