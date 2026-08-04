using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Infrastructure.MultiTenancy;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// School DB identity/management tables in <c>man</c> schema.
/// Catalog tables (menus, dashboard_widgets, usertypes, schools) live only on the platform global DB.
/// <c>roledashboardwidgetpermissions</c> is school-only (no FK to catalog widgets).
/// </summary>
[Tags("School")]
[Migration(99, "School database — identity tables")]
public sealed class S099_CreateSchoolDatabaseIdentityTables : Migration
{
    private static string M => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(M).Exists())
        {
            Create.Schema(M);
        }

        EnsureUsersTable();
        EnsureRolesTable();
        EnsureUserRolesTable();
        EnsureRoleMenuPermissionsTable();
        EnsureRefreshTokensTable();
        EnsureRoleDashboardWidgetPermissionsTable();
        EnsureSchoolSettingsTable();
        EnsureSchoolBranchesTable();
        EnsureUserBranchMappingsTable();

        // Default roles + menu grants must exist before later S1xx "grant to School Admin" migrations.
        Execute.Sql(SchoolDefaultRoleSeeder.BuildMigrationSql(SchoolLocalTime.Now()));
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableUserBranchMappings).InSchema(M);
        Delete.Table(DatabaseConfig.TableSchoolBranches).InSchema(M);
        Delete.Table(DatabaseConfig.TableSchoolSettings).InSchema(M);
        Execute.Sql($"ALTER TABLE {M}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} DROP CONSTRAINT IF EXISTS fk_role_dashboard_widget_permissions_role;");
        Delete.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(M);
        Execute.Sql($"ALTER TABLE {M}.{DatabaseConfig.TableRefreshTokens} DROP CONSTRAINT IF EXISTS fk_refresh_tokens_user;");
        Delete.Table(DatabaseConfig.TableRefreshTokens).InSchema(M);
        Execute.Sql($"ALTER TABLE {M}.{DatabaseConfig.TableRoleMenuPermissions} DROP CONSTRAINT IF EXISTS fk_role_menu_permissions_role;");
        Delete.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(M);
        Execute.Sql($"ALTER TABLE {M}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_user;");
        Execute.Sql($"ALTER TABLE {M}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_role;");
        Delete.Table(DatabaseConfig.TableUserRoles).InSchema(M);
        Delete.Table(DatabaseConfig.TableRoles).InSchema(M);
        Delete.Table(DatabaseConfig.TableUsers).InSchema(M);
    }

    private void EnsureUsersTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableUsers).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUsers).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("firstname").AsString(50).NotNullable()
            .WithColumn("lastname").AsString(50).NotNullable()
            .WithColumn("mobile").AsString(20).Nullable()
            .WithColumn("usertypeid").AsGuid().NotNullable()
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("passwordhash").AsCustom("text").NotNullable()
            .WithColumn("securitystamp").AsCustom("text").Nullable()
            .WithColumn("lockoutend").AsDateTimeOffset().Nullable()
            .WithColumn("accessfailedcount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("lockoutenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("mustchangepassword").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();
        // usertypeid soft-references platform global.usertypes (no local FK)
    }

    private void EnsureRolesTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableRoles).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoles).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("description").AsCustom("text").Nullable()
            .WithAuditColumns();
    }

    private void EnsureUserRolesTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableUserRoles).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserRoles).InSchema(M)
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_user_roles")
            .OnTable(DatabaseConfig.TableUserRoles)
            .WithSchema(M)
            .Columns("userid", "roleid");

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_user FOREIGN KEY (userid) REFERENCES {M}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_role FOREIGN KEY (roleid) REFERENCES {M}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureRoleMenuPermissionsTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableRoleMenuPermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithColumn("menuid").AsGuid().NotNullable()
            .WithColumn("canview").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canadd").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canedit").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("candelete").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canexport").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.UniqueConstraint("uq_role_menu_permissions_role_menu")
            .OnTable(DatabaseConfig.TableRoleMenuPermissions)
            .WithSchema(M)
            .Columns("roleid", "menuid");

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableRoleMenuPermissions}
    ADD CONSTRAINT fk_role_menu_permissions_role FOREIGN KEY (roleid)
    REFERENCES {M}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");
        // menuid soft-references platform global.menus (no local FK)
    }

    private void EnsureRefreshTokensTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableRefreshTokens).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRefreshTokens).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("token").AsCustom("text").NotNullable().Unique()
            .WithColumn("expiresat").AsDateTimeOffset().NotNullable()
            .WithColumn("isrevoked").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableRefreshTokens}
    ADD CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (userid)
    REFERENCES {M}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureRoleDashboardWidgetPermissionsTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithColumn("widgetid").AsGuid().NotNullable()
            .WithColumn("canview").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.UniqueConstraint("uq_role_dashboard_widget_permissions_role_widget")
            .OnTable(DatabaseConfig.TableRoleDashboardWidgetPermissions)
            .WithSchema(M)
            .Columns("roleid", "widgetid");

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    ADD CONSTRAINT fk_role_dashboard_widget_permissions_role FOREIGN KEY (roleid)
    REFERENCES {M}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");
        // widgetid soft-references platform global.dashboard_widgets (no local FK; school-only table)
    }

    private void EnsureSchoolSettingsTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableSchoolSettings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchoolSettings).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("settingkey").AsString(100).NotNullable()
            .WithColumn("settingvalue").AsString(500).NotNullable()
            .WithAuditColumns();

        Create.UniqueConstraint("uq_schoolsettings_school_key")
            .OnTable(DatabaseConfig.TableSchoolSettings).WithSchema(M)
            .Columns("schoolid", "settingkey");
    }

    private void EnsureSchoolBranchesTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableSchoolBranches).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchoolBranches).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("email").AsString(256).Nullable()
            .WithColumn("address").AsString(500).Nullable()
            .WithColumn("isheadoffice").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.Index("ix_schoolbranches_schoolid")
            .OnTable(DatabaseConfig.TableSchoolBranches).InSchema(M)
            .OnColumn("schoolid").Ascending();
    }

    private void EnsureUserBranchMappingsTable()
    {
        if (Schema.Schema(M).Table(DatabaseConfig.TableUserBranchMappings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserBranchMappings).InSchema(M)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("branchid").AsGuid().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("isdefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {M}.{DatabaseConfig.TableUserBranchMappings}
    ADD CONSTRAINT fk_userbranchmappings_user FOREIGN KEY (userid)
    REFERENCES {M}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;

ALTER TABLE {M}.{DatabaseConfig.TableUserBranchMappings}
    ADD CONSTRAINT fk_userbranchmappings_branch FOREIGN KEY (branchid)
    REFERENCES {M}.{DatabaseConfig.TableSchoolBranches}(id) ON DELETE CASCADE;
""");

        Create.UniqueConstraint("uq_userbranchmappings_user_branch")
            .OnTable(DatabaseConfig.TableUserBranchMappings).WithSchema(M)
            .Columns("userid", "branchid");

        Create.Index("ix_userbranchmappings_userid")
            .OnTable(DatabaseConfig.TableUserBranchMappings).InSchema(M)
            .OnColumn("userid").Ascending();
    }
}
