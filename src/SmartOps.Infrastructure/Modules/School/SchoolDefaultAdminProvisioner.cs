using System.Data;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
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
    private readonly ISchoolDbConnectionFactory _schoolDb;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ILogger<SchoolDefaultAdminProvisioner> _logger;

    public SchoolDefaultAdminProvisioner(
        IDbConnectionFactory connectionFactory,
        ISchoolDbConnectionFactory schoolDb,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger<SchoolDefaultAdminProvisioner> logger)
    {
        _connectionFactory = connectionFactory;
        _schoolDb = schoolDb;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task ProvisionAsync(SchoolEntity school, CancellationToken cancellationToken = default)
    {
        // Dedicated school DB identity lives in man schema (not platform global).
        string man = DatabaseConfig.Schema_Man;
        await using NpgsqlConnection connection = await OpenSchoolIdentityConnectionAsync(school, cancellationToken)
            .ConfigureAwait(false);

        bool userExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                $"""
SELECT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableUsers}
    WHERE lower(trim(email)) = lower(trim(@Email))
       OR lower(trim(username)) = lower(trim(@Username))
);
""",
                new { Email = DefaultEmail, Username = DefaultUsername },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        Guid? roleId = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {man}.{DatabaseConfig.TableRoles}
WHERE isactive = true
  AND lower(trim(name)) = lower(trim(@RoleName))
LIMIT 1;
""",
                new { RoleName = RoleNames.SchoolAdmin },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (roleId is null || roleId == Guid.Empty)
        {
            _logger.LogWarning(
                "Skipped default admin for school {SchoolId}: School Admin role was not found.",
                school.Id);
            return;
        }

        // usertypeid soft-references platform global.usertypes (canonical IDs in UserTypeCodes).
        Guid userTypeId = UserTypeCodes.Ids.SchoolAdmin;

        // IST wall-clock DateTime (Unspecified). Npgsql rejects non-UTC DateTimeOffset for timestamptz.
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid userId;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (userExists)
            {
                userId = await connection.ExecuteScalarAsync<Guid>(
                    new CommandDefinition(
                        $"""
SELECT id FROM {man}.{DatabaseConfig.TableUsers}
WHERE lower(trim(email)) = lower(trim(@Email))
   OR lower(trim(username)) = lower(trim(@Username))
LIMIT 1;
""",
                        new { Email = DefaultEmail, Username = DefaultUsername },
                        transaction: transaction,
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
INSERT INTO {man}.{DatabaseConfig.TableUsers}
(
    id, firstname, lastname, mobile, usertypeid, username, email, passwordhash, securitystamp,
    lockoutend, accessfailedcount, lockoutenabled, mustchangepassword,
    isactive, versionno, createdby, createdon, updatedby, updatedon
)
VALUES
(
    @Id, @FirstName, @LastName, NULL, @UserTypeId, @Username, @Email, @PasswordHash, @SecurityStamp,
    NULL, 0, true, true,
    true, 1, @Actor, @Now, @Actor, @Now
);
""",
                        new
                        {
                            Id = userId,
                            FirstName = "School",
                            LastName = "Admin",
                            UserTypeId = userTypeId,
                            Username = DefaultUsername,
                            Email = DefaultEmail,
                            PasswordHash = passwordHash,
                            SecurityStamp = Guid.NewGuid().ToString("N"),
                            Actor = SystemActor,
                            Now = now
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                _logger.LogInformation(
                    "Created default admin user for school {SchoolId} ({Subdomain}).",
                    school.Id,
                    school.Subdomain);
            }

            await EnsureUserRoleAsync(connection, transaction, man, userId, roleId.Value, now, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Npgsql may dispose the transaction when the command fails; keep the original error.
            }

            throw;
        }
    }

    private async Task<NpgsqlConnection> OpenSchoolIdentityConnectionAsync(
        SchoolEntity school,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(school.ConnectionString))
        {
            return (NpgsqlConnection)await _schoolDb
                .OpenAsync(school.ConnectionString, cancellationToken)
                .ConfigureAwait(false);
        }

        return (NpgsqlConnection)await _connectionFactory
            .CreatePlatformConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task EnsureUserRoleAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string schema,
        Guid userId,
        Guid roleId,
        DateTime now,
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
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
