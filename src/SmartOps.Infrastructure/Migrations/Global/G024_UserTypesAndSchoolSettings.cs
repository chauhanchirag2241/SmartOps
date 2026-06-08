using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(24, "Global — user types, school settings, usertypeid on mappings")]
public sealed class G024_UserTypesAndSchoolSettings : Migration
{
    private const string UserTypesTable = "usertypes";
    private const string SchoolSettingsTable = "schoolsettings";
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Code, string Name)[] UserTypes =
    [
        (Guid.Parse("30000000-0000-0000-0000-000000000001"), UserTypeCodes.Principal, "Principal"),
        (Guid.Parse("30000000-0000-0000-0000-000000000002"), UserTypeCodes.VicePrincipal, "Vice Principal"),
        (Guid.Parse("30000000-0000-0000-0000-000000000003"), UserTypeCodes.SchoolAdmin, "School Admin"),
        (Guid.Parse("30000000-0000-0000-0000-000000000004"), UserTypeCodes.Hod, "HOD"),
        (Guid.Parse("30000000-0000-0000-0000-000000000005"), UserTypeCodes.Teacher, "Teacher"),
        (Guid.Parse("30000000-0000-0000-0000-000000000006"), UserTypeCodes.Accountant, "Accountant"),
        (Guid.Parse("30000000-0000-0000-0000-000000000007"), UserTypeCodes.Staff, "Staff"),
    ];

    public override void Up()
    {
        string g = DatabaseConfig.Schema_Global;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!Schema.Schema(g).Table(UserTypesTable).Exists())
        {
            Create.Table(UserTypesTable).InSchema(g)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
                .WithColumn("code").AsString(50).NotNullable().Unique()
                .WithColumn("name").AsString(100).NotNullable()
                .WithAuditColumns();

            foreach ((Guid id, string code, string name) in UserTypes)
            {
                Execute.Sql($"""
INSERT INTO {g}.{UserTypesTable}
    (id, code, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    ('{id}', '{code}', '{name}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}');
""");
            }
        }

        if (!Schema.Schema(g).Table(DatabaseConfig.TableUserSchoolMappings).Column("usertypeid").Exists())
        {
            Alter.Table(DatabaseConfig.TableUserSchoolMappings).InSchema(g)
                .AddColumn("usertypeid").AsGuid().Nullable();

            Execute.Sql($"""
ALTER TABLE {g}.{DatabaseConfig.TableUserSchoolMappings}
    ADD CONSTRAINT fk_user_school_mappings_usertype
    FOREIGN KEY (usertypeid) REFERENCES {g}.{UserTypesTable}(id);
""");
        }

        if (!Schema.Schema(g).Table(SchoolSettingsTable).Exists())
        {
            Create.Table(SchoolSettingsTable).InSchema(g)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("schoolid").AsGuid().NotNullable()
                .WithColumn("settingkey").AsString(100).NotNullable()
                .WithColumn("settingvalue").AsString(500).NotNullable()
                .WithAuditColumns();

            Create.UniqueConstraint("uq_schoolsettings_school_key")
                .OnTable(SchoolSettingsTable).WithSchema(g)
                .Columns("schoolid", "settingkey");

            Execute.Sql($"""
ALTER TABLE {g}.{SchoolSettingsTable}
    ADD CONSTRAINT fk_schoolsettings_school
    FOREIGN KEY (schoolid) REFERENCES {g}.{DatabaseConfig.TableSchools}(id) ON DELETE CASCADE;
""");

            SeedLeaveSettingsForAllSchools(now);
        }

        Execute.Sql($"""
UPDATE {g}.{DatabaseConfig.TableUserSchoolMappings} m
SET usertypeid = ut.id,
    updatedby = '{SeedActor}',
    updatedon = '{now:O}',
    versionno = m.versionno + 1
FROM {g}.{DatabaseConfig.TableUsers} u
INNER JOIN {g}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id AND ur.isactive = true
INNER JOIN {g}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid AND r.isactive = true
INNER JOIN {g}.{UserTypesTable} ut ON ut.code = '{UserTypeCodes.SchoolAdmin}'
WHERE m.userid = u.id
  AND m.isactive = true
  AND m.usertypeid IS NULL
  AND (r.code = '{RoleCodes.SchoolAdmin}' OR r.code = '{RoleCodes.Admin}');
""");
    }

    private void SeedLeaveSettingsForAllSchools(DateTimeOffset now)
    {
        string g = DatabaseConfig.Schema_Global;
        (string Key, string Value)[] defaults =
        [
            (LeaveSettingKeys.StaffApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StaffApproverUserTypes, UserTypeCodes.SchoolAdmin),
            (LeaveSettingKeys.StudentApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StudentDefaultApprover, LeaveApproverTokens.ClassTeacher),
            (LeaveSettingKeys.StudentLongLeaveMinDays, "4"),
            (LeaveSettingKeys.StudentLongLeaveApproverUserTypes, UserTypeCodes.Principal),
            (LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, "true"),
        ];

        foreach ((string key, string value) in defaults)
        {
            Execute.Sql($"""
INSERT INTO {g}.{SchoolSettingsTable}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), s.id, '{key}', '{value}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {g}.{DatabaseConfig.TableSchools} s
WHERE s.isactive = true
  AND NOT EXISTS (
    SELECT 1 FROM {g}.{SchoolSettingsTable} ss
    WHERE ss.schoolid = s.id AND ss.settingkey = '{key}'
  );
""");
        }
    }

    public override void Down()
    {
        string g = DatabaseConfig.Schema_Global;

        Execute.Sql($"ALTER TABLE {g}.{DatabaseConfig.TableUserSchoolMappings} DROP CONSTRAINT IF EXISTS fk_user_school_mappings_usertype;");
        if (Schema.Schema(g).Table(DatabaseConfig.TableUserSchoolMappings).Column("usertypeid").Exists())
        {
            Delete.Column("usertypeid").FromTable(DatabaseConfig.TableUserSchoolMappings).InSchema(g);
        }

        Delete.Table(SchoolSettingsTable).InSchema(g);
        Delete.Table(UserTypesTable).InSchema(g);
    }
}
