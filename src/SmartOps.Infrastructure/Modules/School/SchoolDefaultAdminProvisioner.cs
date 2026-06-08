using System.Data;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Infrastructure.Modules.School;

public sealed class SchoolDefaultAdminProvisioner : ISchoolDefaultAdminProvisioner
{
    public const string DefaultEmail = "admin@smartops.com";
    public const string DefaultUsername = "admin";
    public const string DefaultPassword = "Admin@123";

    private static readonly Guid SystemActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ILogger<SchoolDefaultAdminProvisioner> _logger;

    public SchoolDefaultAdminProvisioner(
        IDbConnectionFactory connectionFactory,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger<SchoolDefaultAdminProvisioner> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task ProvisionAsync(SchoolEntity school, CancellationToken cancellationToken = default)
    {
        string g = DatabaseConfig.Schema_Global;
        await using NpgsqlConnection connection = await OpenSchoolIdentityConnectionAsync(school, cancellationToken)
            .ConfigureAwait(false);

        bool userExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                $"""
SELECT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableUsers}
    WHERE lower(trim(email)) = lower(trim(@Email))
       OR lower(trim(username)) = lower(trim(@Username))
);
""",
                new { Email = DefaultEmail, Username = DefaultUsername },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        Guid? roleId = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {g}.{DatabaseConfig.TableRoles}
WHERE isactive = true
  AND (code = @RoleCode OR lower(trim(name)) = lower(trim(@RoleName)))
LIMIT 1;
""",
                new { RoleCode = RoleCodes.SchoolAdmin, RoleName = RoleNames.SchoolAdmin },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (roleId is null || roleId == Guid.Empty)
        {
            _logger.LogWarning(
                "Skipped default admin for school {SchoolId}: School Admin role was not found.",
                school.Id);
            return;
        }

        Guid? userTypeId = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {g}.{DatabaseConfig.TableUserTypes}
WHERE isactive = true AND code = @Code
LIMIT 1;
""",
                new { Code = UserTypeCodes.SchoolAdmin },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid userId;

        if (userExists)
        {
            userId = await connection.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    $"""
SELECT id FROM {g}.{DatabaseConfig.TableUsers}
WHERE lower(trim(email)) = lower(trim(@Email))
   OR lower(trim(username)) = lower(trim(@Username))
LIMIT 1;
""",
                    new { Email = DefaultEmail, Username = DefaultUsername },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            _logger.LogInformation(
                "Default admin user already exists for school {SchoolId}; ensuring role mapping.",
                school.Id);
        }
        else
        {
            var tempUser = new ApplicationUser
            {
                Email = DefaultEmail,
                Username = DefaultUsername
            };
            string passwordHash = _passwordHasher.HashPassword(tempUser, DefaultPassword);
            userId = Guid.NewGuid();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"""
INSERT INTO {g}.{DatabaseConfig.TableUsers}
(
    id, username, email, passwordhash, securitystamp, lockoutend, accessfailedcount, lockoutenabled,
    isactive, versionno, createdby, createdon, updatedby, updatedon
)
VALUES
(
    @Id, @Username, @Email, @PasswordHash, @SecurityStamp, NULL, 0, true,
    true, 1, @Actor, @Now, @Actor, @Now
);
""",
                    new
                    {
                        Id = userId,
                        Username = DefaultUsername,
                        Email = DefaultEmail,
                        PasswordHash = passwordHash,
                        SecurityStamp = Guid.NewGuid().ToString("N"),
                        Actor = SystemActor,
                        Now = now
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            _logger.LogInformation(
                "Created default admin user for school {SchoolId} ({Subdomain}).",
                school.Id,
                school.Subdomain);
        }

        await EnsureUserRoleAsync(connection, g, userId, roleId.Value, now, cancellationToken).ConfigureAwait(false);
        await EnsureUserSchoolMappingAsync(
            connection,
            g,
            userId,
            school.Id,
            userTypeId,
            now,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<NpgsqlConnection> OpenSchoolIdentityConnectionAsync(
        SchoolEntity school,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(school.ConnectionString))
        {
            return (NpgsqlConnection)await _connectionFactory
                .CreateConnectionAsync(school.ConnectionString, cancellationToken)
                .ConfigureAwait(false);
        }

        return (NpgsqlConnection)await _connectionFactory
            .CreatePlatformConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task EnsureUserRoleAsync(
        IDbConnection connection,
        string schema,
        Guid userId,
        Guid roleId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
INSERT INTO {schema}.{DatabaseConfig.TableUserRoles}
    (userid, roleid, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @UserId, @RoleId, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableUserRoles}
    WHERE userid = @UserId AND roleid = @RoleId
);
""",
                new { UserId = userId, RoleId = roleId, Actor = SystemActor, Now = now },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task EnsureUserSchoolMappingAsync(
        IDbConnection connection,
        string schema,
        Guid userId,
        Guid schoolId,
        Guid? userTypeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
INSERT INTO {schema}.{DatabaseConfig.TableUserSchoolMappings}
    (userid, schoolid, role, usertypeid, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @UserId, @SchoolId, @Role, @UserTypeId, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableUserSchoolMappings}
    WHERE userid = @UserId AND schoolid = @SchoolId
);
""",
                new
                {
                    UserId = userId,
                    SchoolId = schoolId,
                    Role = RoleNames.SchoolAdmin,
                    UserTypeId = userTypeId,
                    Actor = SystemActor,
                    Now = now
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
