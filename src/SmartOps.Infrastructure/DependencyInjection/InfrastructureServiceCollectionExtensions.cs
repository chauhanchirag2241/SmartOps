using FluentMigrator.Runner;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Identity.Utilities;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Migrations.Global;
using SmartOps.Infrastructure.Modules.Identity.Repositories;
using SmartOps.Infrastructure.Modules.Identity.Services;
using SmartOps.Infrastructure.Modules.Identity.Stores;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Factories;
using SmartOps.Infrastructure.Persistence.TypeHandlers;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Domain.Modules.Class.Interfaces;
using SmartOps.Domain.Modules.Subject.Interfaces;
using SmartOps.Domain.Modules.AcademicYear.Interfaces;
using SmartOps.Domain.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.School.Interfaces;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Infrastructure.Modules.Attendance.Repositories;
using SmartOps.Infrastructure.Modules.Attendance.Services;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Domain.Modules.Setting.Interfaces;
using SmartOps.Infrastructure.Modules.Setting.Repositories;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Infrastructure.Modules.Teacher.Services;
using Dapper;

namespace SmartOps.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSmartOpsDatabaseInfrastructure(configuration);
        services.AddSmartOpsIdentityInfrastructure(configuration);
        services.AddSmartOpsAuthorizationInfrastructure(configuration);
        services.AddSmartOpsMultiTenancyInfrastructure();
        return services;
    }

    public static IServiceCollection AddSmartOpsDatabaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<DapperContext>();
        
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<ITeacherAssignmentService, TeacherAssignmentService>();

        string? connectionString = configuration.GetConnectionString("GlobalDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'GlobalDb' is not configured.");
        }

        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(G000_EnablePgCrypto).Assembly).For.Migrations());

        services.AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    public static IServiceCollection AddSmartOpsIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PersonaRoleMappingOptions>(configuration.GetSection(PersonaRoleMappingOptions.SectionName));

        services.AddScoped<IPersonaRoleMapper, PersonaRoleMapper>();
        services.AddScoped<UserRepository>();
        services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<UserRepository>());

        services.AddScoped<RoleRepository>();
        services.AddScoped<IRoleRepository>(sp => sp.GetRequiredService<RoleRepository>());

        services.AddScoped<MenuRepository>();
        services.AddScoped<IMenuRepository>(sp => sp.GetRequiredService<MenuRepository>());

        services.AddScoped<IPermissionService, PermissionService>();

        services.AddScoped<IUserProvisioningService, UserProvisioningService>();

        services.AddScoped<RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenRepository>(sp => sp.GetRequiredService<RefreshTokenRepository>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<ILookupNormalizer, UpperInvariantLookupNormalizer>();
        services.AddScoped<IdentityErrorDescriber>();

        services.AddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
        services.AddScoped<IRoleStore<ApplicationRole>, CustomRoleStore>();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<ApplicationRole>();

        return services;
    }

    public static IServiceCollection AddSmartOpsMultiTenancyInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ITenantSchemaProvider, TenantSchemaProvider>();
        services.AddScoped<ITenantSchoolResolver, TenantSchoolResolver>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ITenantSchemaSyncService, TenantSchemaSyncService>();
        return services;
    }
}
