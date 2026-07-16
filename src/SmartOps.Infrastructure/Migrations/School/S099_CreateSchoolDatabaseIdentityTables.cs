using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Identity tables required inside each dedicated school database (<c>global</c> schema).
/// Platform-only tables (e.g. <c>schools</c>) are intentionally excluded.
/// </summary>
[Tags("School")]
[Migration(99, "School database — identity tables")]
public sealed class S099_CreateSchoolDatabaseIdentityTables : Migration
{
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(G).Exists())
        {
            Create.Schema(G);
        }

        EnsureUsersTable();
        EnsureRolesTable();
        EnsureMenusTable();
        EnsureUserRolesTable();
        EnsureRoleMenuPermissionsTable();
        EnsureUserSchoolMappingsTable();
        EnsureRefreshTokensTable();
        EnsureUserScopeVersionsTable();
        EnsureDashboardWidgetsTables();
        EnsureUserTypesTable();
        EnsureSchoolSettingsTable();
        EnsureSchoolBranchesTable();
        EnsureUserBranchMappingsTable();
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableUserBranchMappings).InSchema(G);
        Delete.Table(DatabaseConfig.TableSchoolBranches).InSchema(G);
        Delete.Table(DatabaseConfig.TableSchoolSettings).InSchema(G);
        Delete.Table(DatabaseConfig.TableUserTypes).InSchema(G);
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} DROP CONSTRAINT IF EXISTS fk_role_dashboard_widget_permissions_role;");
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} DROP CONSTRAINT IF EXISTS fk_role_dashboard_widget_permissions_widget;");
        Delete.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(G);
        Delete.Table(DatabaseConfig.TableDashboardWidgets).InSchema(G);
        Delete.Table(DatabaseConfig.TableUserScopeVersions).InSchema(G);
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableRefreshTokens} DROP CONSTRAINT IF EXISTS fk_refresh_tokens_user;");
        Delete.Table(DatabaseConfig.TableRefreshTokens).InSchema(G);
        Delete.Table(DatabaseConfig.TableUserSchoolMappings).InSchema(G);
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableRoleMenuPermissions} DROP CONSTRAINT IF EXISTS fk_role_menu_permissions_role;");
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableRoleMenuPermissions} DROP CONSTRAINT IF EXISTS fk_role_menu_permissions_menu;");
        Delete.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(G);
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_user;");
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_role;");
        Delete.Table(DatabaseConfig.TableUserRoles).InSchema(G);
        Execute.Sql($"ALTER TABLE {G}.{DatabaseConfig.TableMenus} DROP CONSTRAINT IF EXISTS fk_menus_parent;");
        Delete.Table(DatabaseConfig.TableMenus).InSchema(G);
        Delete.Table(DatabaseConfig.TableRoles).InSchema(G);
        Delete.Table(DatabaseConfig.TableUsers).InSchema(G);
    }

    private void EnsureUsersTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUsers).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUsers).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("passwordhash").AsCustom("text").NotNullable()
            .WithColumn("securitystamp").AsCustom("text").Nullable()
            .WithColumn("lockoutend").AsDateTimeOffset().Nullable()
            .WithColumn("accessfailedcount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("lockoutenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();
    }

    private void EnsureRolesTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableRoles).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoles).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(100).NotNullable().Unique()
            .WithColumn("code").AsString(50).NotNullable().Unique()
            .WithColumn("description").AsCustom("text").Nullable()
            .WithAuditColumns();
    }

    private void EnsureMenusTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableMenus).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableMenus).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(150).NotNullable()
            .WithColumn("code").AsString(50).NotNullable()
            .WithColumn("parentmenuid").AsGuid().Nullable()
            .WithColumn("route").AsString(300).Nullable()
            .WithColumn("icon").AsString(100).Nullable()
            .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("application").AsString(20).NotNullable().WithDefaultValue("COMMON")
            .WithAuditColumns();

        Create.UniqueConstraint("uq_menus_code_application")
            .OnTable(DatabaseConfig.TableMenus).WithSchema(G)
            .Columns("code", "application");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableMenus}
    ADD CONSTRAINT fk_menus_parent FOREIGN KEY (parentmenuid)
    REFERENCES {G}.{DatabaseConfig.TableMenus}(id) ON DELETE SET NULL;
""");
    }

    private void EnsureUserRolesTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUserRoles).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserRoles).InSchema(G)
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_user_roles")
            .OnTable(DatabaseConfig.TableUserRoles)
            .WithSchema(G)
            .Columns("userid", "roleid");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_user FOREIGN KEY (userid) REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_role FOREIGN KEY (roleid) REFERENCES {G}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureRoleMenuPermissionsTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableRoleMenuPermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(G)
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
            .WithSchema(G)
            .Columns("roleid", "menuid");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableRoleMenuPermissions}
    ADD CONSTRAINT fk_role_menu_permissions_role FOREIGN KEY (roleid)
    REFERENCES {G}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableRoleMenuPermissions}
    ADD CONSTRAINT fk_role_menu_permissions_menu FOREIGN KEY (menuid)
    REFERENCES {G}.{DatabaseConfig.TableMenus}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureUserSchoolMappingsTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUserSchoolMappings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserSchoolMappings).InSchema(G)
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("role").AsString(100).NotNullable()
            .WithColumn("usertypeid").AsGuid().Nullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_user_school_mappings")
            .OnTable(DatabaseConfig.TableUserSchoolMappings)
            .WithSchema(G)
            .Columns("userid", "schoolid");

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableUserSchoolMappings}
    ADD CONSTRAINT fk_user_school_mappings_user FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureRefreshTokensTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableRefreshTokens).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRefreshTokens).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("token").AsCustom("text").NotNullable().Unique()
            .WithColumn("expiresat").AsDateTimeOffset().NotNullable()
            .WithColumn("isrevoked").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableRefreshTokens}
    ADD CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");
    }

    private void EnsureUserScopeVersionsTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUserScopeVersions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserScopeVersions).InSchema(G)
            .WithColumn("userid").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("version").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("updatedon").AsDateTimeOffset().NotNullable()
                .WithDefaultValue(RawSql.Insert("NOW()"));

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableUserScopeVersions}
    ADD CONSTRAINT fk_userscopeversions_userid FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

        Create.Index("ix_userscopeversions_schoolid")
            .OnTable(DatabaseConfig.TableUserScopeVersions).InSchema(G)
            .OnColumn("schoolid").Ascending();
    }

    private void EnsureDashboardWidgetsTables()
    {
        if (!Schema.Schema(G).Table(DatabaseConfig.TableDashboardWidgets).Exists())
        {
            Create.Table(DatabaseConfig.TableDashboardWidgets).InSchema(G)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("code").AsString(80).NotNullable()
                .WithColumn("name").AsString(120).NotNullable()
                .WithColumn("category").AsString(40).NotNullable()
                .WithColumn("requiredmenucode").AsString(80).NotNullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("defaultsize").AsString(20).NotNullable().WithDefaultValue("stat")
                .WithAuditColumns();

            Create.UniqueConstraint("uq_dashboard_widgets_code")
                .OnTable(DatabaseConfig.TableDashboardWidgets)
                .WithSchema(G)
                .Columns("code");
        }

        if (!Schema.Schema(G).Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).Exists())
        {
            Create.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(G)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("roleid").AsGuid().NotNullable()
                .WithColumn("widgetid").AsGuid().NotNullable()
                .WithColumn("canview").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_role_dashboard_widget_permissions_role_widget")
                .OnTable(DatabaseConfig.TableRoleDashboardWidgetPermissions)
                .WithSchema(G)
                .Columns("roleid", "widgetid");

            Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    ADD CONSTRAINT fk_role_dashboard_widget_permissions_role FOREIGN KEY (roleid)
    REFERENCES {G}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");

            Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    ADD CONSTRAINT fk_role_dashboard_widget_permissions_widget FOREIGN KEY (widgetid)
    REFERENCES {G}.{DatabaseConfig.TableDashboardWidgets}(id) ON DELETE CASCADE;
""");
        }
    }

    private void EnsureUserTypesTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUserTypes).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserTypes).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("code").AsString(50).NotNullable().Unique()
            .WithColumn("name").AsString(100).NotNullable()
            .WithAuditColumns();
    }

    private void EnsureSchoolSettingsTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableSchoolSettings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchoolSettings).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("settingkey").AsString(100).NotNullable()
            .WithColumn("settingvalue").AsString(500).NotNullable()
            .WithAuditColumns();

        Create.UniqueConstraint("uq_schoolsettings_school_key")
            .OnTable(DatabaseConfig.TableSchoolSettings).WithSchema(G)
            .Columns("schoolid", "settingkey");
    }

    private void EnsureSchoolBranchesTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableSchoolBranches).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchoolBranches).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("email").AsString(256).Nullable()
            .WithColumn("address").AsString(500).Nullable()
            .WithColumn("isheadoffice").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.Index("ix_schoolbranches_schoolid")
            .OnTable(DatabaseConfig.TableSchoolBranches).InSchema(G)
            .OnColumn("schoolid").Ascending();
    }

    private void EnsureUserBranchMappingsTable()
    {
        if (Schema.Schema(G).Table(DatabaseConfig.TableUserBranchMappings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserBranchMappings).InSchema(G)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("branchid").AsGuid().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("isdefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {G}.{DatabaseConfig.TableUserBranchMappings}
    ADD CONSTRAINT fk_userbranchmappings_user FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;

ALTER TABLE {G}.{DatabaseConfig.TableUserBranchMappings}
    ADD CONSTRAINT fk_userbranchmappings_branch FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id) ON DELETE CASCADE;
""");

        Create.UniqueConstraint("uq_userbranchmappings_user_branch")
            .OnTable(DatabaseConfig.TableUserBranchMappings).WithSchema(G)
            .Columns("userid", "branchid");

        Create.Index("ix_userbranchmappings_userid")
            .OnTable(DatabaseConfig.TableUserBranchMappings).InSchema(G)
            .OnColumn("userid").Ascending();
    }
}
