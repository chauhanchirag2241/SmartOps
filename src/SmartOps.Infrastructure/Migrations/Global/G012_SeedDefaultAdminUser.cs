using FluentMigrator;
using Microsoft.AspNetCore.Identity;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(12, "Global — seed platform admin")]
public sealed class G012_SeedDefaultAdminUser : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private const string AdminEmail = "admin@smartops.com";
    private const string AdminPassword = "Admin@123";

    public override void Up()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var tempUser = new ApplicationUser { Email = AdminEmail, Username = "platform.admin" };
        string passwordHash = hasher.HashPassword(tempUser, AdminPassword);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
    (id, username, email, passwordhash, securitystamp, lockoutend, accessfailedcount, lockoutenabled,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{userId}', 'platform.admin', '{AdminEmail}', '{passwordHash.Replace("'", "''")}', '{Guid.NewGuid():N}',
       NULL, 0, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} WHERE email = '{AdminEmail}');
""");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
    (userid, roleid, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{userId}', r.id, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
WHERE r.code = 'ADMIN'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur
    WHERE ur.userid = '{userId}' AND ur.roleid = r.id
  );
""");
    }

    public override void Down()
    {
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
WHERE userid = '22222222-2222-2222-2222-222222222222';
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}
WHERE email = '{AdminEmail}';
""");
    }
}
