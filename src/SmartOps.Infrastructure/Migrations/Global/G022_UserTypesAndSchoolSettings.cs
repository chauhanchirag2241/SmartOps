using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>
/// Ensures platform usertypes catalog is complete.
/// schoolsettings live only on each school DB (<c>man.schoolsettings</c>).
/// </summary>
[Tags("Global")]
[Migration(22, "Global — user types seed (schoolsettings on school DB only)")]
public sealed class G022_UserTypesAndSchoolSettings : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public override void Up()
    {
        string g = DatabaseConfig.Schema_Global;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name) in UserTypeCodes.All)
        {
            Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableUserTypes}
    (id, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name.Replace("'", "''")}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {g}.{DatabaseConfig.TableUserTypes} WHERE lower(trim(name)) = lower(trim('{name.Replace("'", "''")}')));
""");
        }
    }

    public override void Down()
    {
    }
}
