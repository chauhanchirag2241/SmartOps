using System.Data;
using Dapper;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Application.Abstractions;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Identity;

public sealed class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        GetByEmailCoreAsync(email, transaction: null, cancellationToken);

    public Task<ApplicationUser?> GetByEmailAsync(
        string email,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        GetByEmailCoreAsync(email, transaction, cancellationToken);

    private async Task<ApplicationUser?> GetByEmailCoreAsync(
        string email,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT
    id AS Id,
    firstname AS FirstName,
    lastname AS LastName,
    mobile AS Mobile,
    usertypeid AS UserTypeId,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    mustchangepassword AS MustChangePassword,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers}
WHERE lower(trim(email)) = lower(trim(@Email)) AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await ResolveConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(
            sql,
            new { Email = email.Trim() },
            transaction: transaction,
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    firstname AS FirstName,
    lastname AS LastName,
    mobile AS Mobile,
    usertypeid AS UserTypeId,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    mustchangepassword AS MustChangePassword,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers}
WHERE id = @Id AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        GetByUsernameCoreAsync(username, transaction: null, cancellationToken);

    public Task<ApplicationUser?> GetByUsernameAsync(
        string username,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        GetByUsernameCoreAsync(username, transaction, cancellationToken);

    private async Task<ApplicationUser?> GetByUsernameCoreAsync(
        string username,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT
    id AS Id,
    firstname AS FirstName,
    lastname AS LastName,
    mobile AS Mobile,
    usertypeid AS UserTypeId,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    mustchangepassword AS MustChangePassword,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers}
WHERE lower(trim(username)) = lower(trim(@Username)) AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await ResolveConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(
            sql,
            new { Username = username.Trim() },
            transaction: transaction,
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApplicationUser>(command).ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByLoginIdentifierAsync(
        string login,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        string trimmed = login.Trim();

        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            return await GetByEmailAsync(trimmed.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        }

        string digits = NormalizeMobileDigits(trimmed);
        if (digits.Length >= 10)
        {
            ApplicationUser? byMobile = await GetByMobileAsync(digits, cancellationToken).ConfigureAwait(false);
            if (byMobile is not null)
            {
                return byMobile;
            }
        }

        ApplicationUser? byUsername = await GetByUsernameAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (byUsername is not null)
        {
            return byUsername;
        }

        if (digits.Length >= 3)
        {
            return await GetByUsernameAsync(digits, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<ApplicationUser?> GetByMobileAsync(
        string tenDigitMobile,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT
    id AS Id,
    firstname AS FirstName,
    lastname AS LastName,
    mobile AS Mobile,
    usertypeid AS UserTypeId,
    username AS Username,
    email AS Email,
    passwordhash AS PasswordHash,
    securitystamp AS SecurityStamp,
    lockoutend AS LockoutEnd,
    accessfailedcount AS AccessFailedCount,
    lockoutenabled AS LockoutEnabled,
    mustchangepassword AS MustChangePassword,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers}
WHERE isactive = true
  AND mobile IS NOT NULL
  AND right(regexp_replace(mobile, '\D', '', 'g'), 10) = @Mobile
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection
            .QuerySingleOrDefaultAsync<ApplicationUser>(
                new CommandDefinition(sql, new { Mobile = tenDigitMobile }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static string NormalizeMobileDigits(string value)
    {
        string digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
        {
            return digits[^10..];
        }

        return digits;
    }

    public Task CreateAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(user, transaction: null, cancellationToken);

    public Task CreateAsync(
        ApplicationUser user,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(user, transaction, cancellationToken);

    private async Task CreateCoreAsync(
        ApplicationUser user,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (user.Id == Guid.Empty)
        {
            user.Id = Guid.NewGuid();
        }

        DateTime now = SchoolLocalTime.NowDateTime();
        EnsureInsertAudit(user, now, user.Id);

        string sql = $"""
INSERT INTO {IdentitySchema}.{DatabaseConfig.TableUsers}
(
    id,
    firstname,
    lastname,
    mobile,
    usertypeid,
    username,
    email,
    passwordhash,
    securitystamp,
    lockoutend,
    accessfailedcount,
    lockoutenabled,
    mustchangepassword,
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
    @FirstName,
    @LastName,
    @Mobile,
    @UserTypeId,
    @Username,
    @Email,
    @PasswordHash,
    @SecurityStamp,
    @LockoutEnd,
    @AccessFailedCount,
    @LockoutEnabled,
    @MustChangePassword,
    @IsActive,
    @VersionNo,
    @CreatedBy,
    @CreatedOn,
    @UpdatedBy,
    @UpdatedOn
)
""";

        IDbConnection connection = await ResolveConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);
        CommandDefinition command = new(sql, user, transaction: transaction, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actor = ResolveUpdateActor(user.Id);

        string sql = $"""
UPDATE {IdentitySchema}.{DatabaseConfig.TableUsers}
SET
    firstname = @FirstName,
    lastname = @LastName,
    mobile = @Mobile,
    usertypeid = @UserTypeId,
    username = @Username,
    email = @Email,
    passwordhash = @PasswordHash,
    securitystamp = @SecurityStamp,
    lockoutend = @LockoutEnd,
    accessfailedcount = @AccessFailedCount,
    lockoutenabled = @LockoutEnabled,
    mustchangepassword = @MustChangePassword,
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
                user.FirstName,
                user.LastName,
                user.Mobile,
                user.UserTypeId,
                user.Username,
                user.Email,
                user.PasswordHash,
                user.SecurityStamp,
                user.LockoutEnd,
                user.AccessFailedCount,
                user.LockoutEnabled,
                user.MustChangePassword,
                user.IsActive,
                UpdatedBy = actor,
                UpdatedOn = now,
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
        user.UpdatedOn = now;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    u.id AS Id,
    u.firstname AS FirstName,
    u.lastname AS LastName,
    u.mobile AS Mobile,
    u.usertypeid AS UserTypeId,
    u.username AS Username,
    u.email AS Email,
    u.passwordhash AS PasswordHash,
    u.securitystamp AS SecurityStamp,
    u.lockoutend AS LockoutEnd,
    u.accessfailedcount AS AccessFailedCount,
    u.lockoutenabled AS LockoutEnabled,
    u.mustchangepassword AS MustChangePassword,
    u.isactive AS IsActive,
    u.versionno AS VersionNo,
    u.createdby AS CreatedBy,
    u.createdon AS CreatedOn,
    u.updatedby AS UpdatedBy,
    u.updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers} u
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid
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
FROM {IdentitySchema}.{DatabaseConfig.TableRoles} r
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = r.id
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND r.isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<string?> GetUserTypeCodeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT u.usertypeid
FROM {IdentitySchema}.{DatabaseConfig.TableUsers} u
WHERE u.id = @UserId
  AND u.isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid? userTypeId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return userTypeId is null || userTypeId == Guid.Empty
            ? null
            : UserTypeCodes.GetName(userTypeId.Value);
    }

    public async Task<(Guid RoleId, string RoleName)?> GetPrimaryRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT r.id AS RoleId, r.name AS RoleName
FROM {IdentitySchema}.{DatabaseConfig.TableRoles} r
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = r.id
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND r.isactive = true
ORDER BY r.name
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        RoleSummaryRow? row = await connection.QuerySingleOrDefaultAsync<RoleSummaryRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return row is null ? null : (row.RoleId, row.RoleName);
    }

    public Task AddUserToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default) =>
        AddUserToRoleCoreAsync(userId, roleName, transaction: null, cancellationToken);

    public Task AddUserToRoleAsync(
        Guid userId,
        string roleName,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        AddUserToRoleCoreAsync(userId, roleName, transaction, cancellationToken);

    private async Task AddUserToRoleCoreAsync(
        Guid userId,
        string roleName,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        Guid actor = ResolveUpdateActor(userId);
        DateTime now = SchoolLocalTime.NowDateTime();

        IDbConnection connection = await ResolveConnectionAsync(transaction, cancellationToken).ConfigureAwait(false);

        string roleSql = $"""
SELECT id
FROM {IdentitySchema}.{DatabaseConfig.TableRoles}
WHERE name = @RoleName AND isactive = true
LIMIT 1
""";

        Guid? roleId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                roleSql,
                new { RoleName = roleName },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (roleId is null || roleId.Value == Guid.Empty)
        {
            throw new InvalidOperationException($"Role '{roleName}' was not found.");
        }

        string mappingSql = $"""
SELECT isactive AS IsActive, versionno AS VersionNo
FROM {IdentitySchema}.{DatabaseConfig.TableUserRoles}
WHERE userid = @UserId AND roleid = @RoleId
LIMIT 1
""";

        UserRoleMappingRow? mappingRow = await connection.QuerySingleOrDefaultAsync<UserRoleMappingRow>(
            new CommandDefinition(
                mappingSql,
                new { UserId = userId, RoleId = roleId },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (mappingRow is not null)
        {
            if (mappingRow.IsActive)
            {
                return;
            }

            string reviveSql = $"""
UPDATE {IdentitySchema}.{DatabaseConfig.TableUserRoles}
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
                        Now = now,
                        VersionNo = mappingRow.VersionNo
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (revived == 0)
            {
                throw new ConcurrencyException("Record was modified by another user.");
            }

            return;
        }

        string insertSql = $"""
INSERT INTO {IdentitySchema}.{DatabaseConfig.TableUserRoles}
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
                    Now = now
                },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RemoveUserFromRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        Guid actor = ResolveUpdateActor(userId);
        DateTime now = SchoolLocalTime.NowDateTime();

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string roleSql = $"""
SELECT id
FROM {IdentitySchema}.{DatabaseConfig.TableRoles}
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
FROM {IdentitySchema}.{DatabaseConfig.TableUserRoles}
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
UPDATE {IdentitySchema}.{DatabaseConfig.TableUserRoles}
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
                    Now = now,
                    VersionNo = version.Value
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }
    }

    public async Task SetUserTypeAsync(
        Guid userId,
        Guid? userTypeId,
        CancellationToken cancellationToken = default)
    {
        if (userTypeId is null || userTypeId == Guid.Empty)
        {
            return;
        }

        ApplicationUser? user = await GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.UserTypeId = userTypeId.Value;
        await UpdateAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersBySchoolAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        // Dedicated school databases only contain that school's users.
        _ = schoolId;
        string sql = $"""
SELECT
    u.id AS Id,
    u.firstname AS FirstName,
    u.lastname AS LastName,
    u.mobile AS Mobile,
    u.usertypeid AS UserTypeId,
    u.username AS Username,
    u.email AS Email,
    u.passwordhash AS PasswordHash,
    u.securitystamp AS SecurityStamp,
    u.lockoutend AS LockoutEnd,
    u.accessfailedcount AS AccessFailedCount,
    u.lockoutenabled AS LockoutEnabled,
    u.mustchangepassword AS MustChangePassword,
    u.isactive AS IsActive,
    u.versionno AS VersionNo,
    u.createdby AS CreatedBy,
    u.createdon AS CreatedOn,
    u.updatedby AS UpdatedBy,
    u.updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableUsers} u
WHERE u.isactive = true
ORDER BY u.username
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ApplicationUser> rows = await connection.QueryAsync<ApplicationUser>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private async Task<IDbConnection> ResolveConnectionAsync(
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection is not null)
        {
            return transaction.Connection;
        }

        return await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class UserRoleMappingRow
    {
        public bool IsActive { get; set; }

        public int VersionNo { get; set; }
    }

    private sealed class RoleSummaryRow
    {
        public Guid RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;
    }
}
