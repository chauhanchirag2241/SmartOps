using System.Data;
using Dapper;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Common;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity.Repositories;

public sealed class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
WHERE email = @Email AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, new { Email = email }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
WHERE id = @Id AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
WHERE username = @Username AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, new { Username = username }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public async Task CreateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (user.Id == Guid.Empty)
        {
            user.Id = Guid.NewGuid();
        }

        DateTime utcNow = DateTime.UtcNow;
        EnsureInsertAudit(user, utcNow, user.Id);

        string sql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
(
    id,
    username,
    email,
    passwordhash,
    securitystamp,
    lockoutend,
    accessfailedcount,
    lockoutenabled,
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
    @Username,
    @Email,
    @PasswordHash,
    @SecurityStamp,
    @LockoutEnd,
    @AccessFailedCount,
    @LockoutEnabled,
    @IsActive,
    @VersionNo,
    @CreatedBy,
    @CreatedOn,
    @UpdatedBy,
    @UpdatedOn
)
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, user, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actor = ResolveUpdateActor(user.Id);

        string sql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
SET
    username = @Username,
    email = @Email,
    passwordhash = @PasswordHash,
    securitystamp = @SecurityStamp,
    lockoutend = @LockoutEnd,
    accessfailedcount = @AccessFailedCount,
    lockoutenabled = @LockoutEnabled,
    isactive = @IsActive,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @Id AND versionno = @VersionNo AND isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(
            sql,
            new
            {
                user.Id,
                user.Username,
                user.Email,
                user.PasswordHash,
                user.SecurityStamp,
                user.LockoutEnd,
                user.AccessFailedCount,
                user.LockoutEnabled,
                user.IsActive,
                UpdatedBy = actor,
                UpdatedOn = utcNow,
                VersionNo = user.VersionNo
            },
            cancellationToken: cancellationToken);

        int rowsAffected = await connection.ExecuteAsync(command).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }

        user.VersionNo += 1;
        user.UpdatedBy = actor;
        user.UpdatedOn = utcNow;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    u.id AS Id,
    u.username AS Username,
    u.email AS Email,
    u.passwordhash AS PasswordHash,
    u.securitystamp AS SecurityStamp,
    u.lockoutend AS LockoutEnd,
    u.accessfailedcount AS AccessFailedCount,
    u.lockoutenabled AS LockoutEnabled,
    u.isactive AS IsActive,
    u.versionno AS VersionNo,
    u.createdby AS CreatedBy,
    u.createdon AS CreatedOn,
    u.updatedby AS UpdatedBy,
    u.updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} u
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid
WHERE r.name = @RoleName
  AND u.isactive = true
  AND ur.isactive = true
  AND r.isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ApplicationUser> rows = await connection.QueryAsync<ApplicationUser>(
            new CommandDefinition(sql, new { RoleName = roleName }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<IList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT r.name
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = r.id
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND r.isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<IList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT DISTINCT p.name
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} p
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} rp ON rp.permissionid = p.id
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = rp.roleid
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND rp.isactive = true
  AND p.isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task AddUserToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        Guid actor = ResolveUpdateActor(userId);
        DateTime utcNow = DateTime.UtcNow;

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string roleSql = $"""
SELECT id
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE name = @RoleName AND isactive = true
LIMIT 1
""";

        Guid? roleId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(roleSql, new { RoleName = roleName }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (roleId is null || roleId.Value == Guid.Empty)
        {
            throw new InvalidOperationException($"Role '{roleName}' was not found.");
        }

        string mappingSql = $"""
SELECT isactive AS IsActive, versionno AS VersionNo
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
WHERE userid = @UserId AND roleid = @RoleId
LIMIT 1
""";

        UserRoleMappingRow? mappingRow = await connection.QuerySingleOrDefaultAsync<UserRoleMappingRow>(
            new CommandDefinition(
                mappingSql,
                new { UserId = userId, RoleId = roleId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (mappingRow is not null)
        {
            if (mappingRow.IsActive)
            {
                return;
            }

            string reviveSql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
SET isactive = true,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = versionno + 1
WHERE userid = @UserId AND roleid = @RoleId AND isactive = false AND versionno = @VersionNo
""";

            int revived = await connection.ExecuteAsync(
                new CommandDefinition(
                    reviveSql,
                    new
                    {
                        UserId = userId,
                        RoleId = roleId,
                        Actor = actor,
                        Now = utcNow,
                        VersionNo = mappingRow.VersionNo
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (revived == 0)
            {
                throw new ConcurrencyException("Record was modified by another user.");
            }

            return;
        }

        string insertSql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
(
    userid,
    roleid,
    isactive,
    versionno,
    createdby,
    createdon,
    updatedby,
    updatedon
)
VALUES
(
    @UserId,
    @RoleId,
    true,
    1,
    @Actor,
    @Now,
    @Actor,
    @Now
)
""";

        await connection.ExecuteAsync(
            new CommandDefinition(
                insertSql,
                new
                {
                    UserId = userId,
                    RoleId = roleId,
                    Actor = actor,
                    Now = utcNow
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RemoveUserFromRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        Guid actor = ResolveUpdateActor(userId);
        DateTime utcNow = DateTime.UtcNow;

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string roleSql = $"""
SELECT id
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE name = @RoleName AND isactive = true
LIMIT 1
""";

        Guid? roleId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(roleSql, new { RoleName = roleName }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (roleId is null || roleId.Value == Guid.Empty)
        {
            return;
        }

        string selectVersionSql = $"""
SELECT versionno
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
WHERE userid = @UserId AND roleid = @RoleId AND isactive = true
LIMIT 1
""";

        int? version = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                selectVersionSql,
                new { UserId = userId, RoleId = roleId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (version is null)
        {
            return;
        }

        string updateSql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
SET isactive = false,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = versionno + 1
WHERE userid = @UserId AND roleid = @RoleId AND isactive = true AND versionno = @VersionNo
""";

        int rows = await connection.ExecuteAsync(
            new CommandDefinition(
                updateSql,
                new
                {
                    UserId = userId,
                    RoleId = roleId,
                    Actor = actor,
                    Now = utcNow,
                    VersionNo = version.Value
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }
    }

    private sealed class UserRoleMappingRow
    {
        public bool IsActive { get; set; }

        public int VersionNo { get; set; }
    }
}
