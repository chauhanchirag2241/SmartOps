using FluentMigrator.Runner;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Identity.Utilities;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Migrations.Global;
using SmartOps.Infrastructure.Modules.Identity;
using SmartOps.Infrastructure.Modules.Identity.Services;
using SmartOps.Infrastructure.Modules.Identity.Stores;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Factories;
using SmartOps.Infrastructure.Persistence.TypeHandlers;
using SmartOps.Domain.Modules.Student;
using SmartOps.Domain.Modules.Class;
using SmartOps.Domain.Modules.Subject;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Modules.Teacher;
using SmartOps.Domain.Modules.School;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Application.Modules.Homework.Interfaces;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Infrastructure.Modules.AcademicYear;
using SmartOps.Infrastructure.Modules.Attendance;
using SmartOps.Infrastructure.Modules.Attendance.Services;
using SmartOps.Infrastructure.Modules.Homework;
using SmartOps.Infrastructure.Modules.Homework.Services;
using SmartOps.Infrastructure.Modules.Fees;
using SmartOps.Infrastructure.Modules.Fees.Services;
using SmartOps.Infrastructure.Modules.Class;
using SmartOps.Infrastructure.Modules.School;
using SmartOps.Infrastructure.Modules.Student;
using SmartOps.Infrastructure.Modules.Subject;
using SmartOps.Infrastructure.Modules.Teacher;
using SmartOps.Domain.Modules.Setting;
using SmartOps.Infrastructure.Modules.Setting;
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
        services.AddScoped<IHomeworkRepository, HomeworkRepository>();
        services.AddScoped<IHomeworkService, HomeworkService>();
        services.AddScoped<IFeeStructureRepository, FeeStructureRepository>();
        services.AddScoped<IClassFeeAmountRepository, ClassFeeAmountRepository>();
        services.AddScoped<IFeeCollectionRepository, FeeCollectionRepository>();
        services.AddScoped<IFeeStructureService, FeeStructureService>();
        services.AddScoped<IClassFeeAmountService, ClassFeeAmountService>();
        services.AddScoped<IFeeCollectionService, FeeCollectionService>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<IClassSubjectTeacherMappingRepository, ClassSubjectTeacherMappingRepository>();
        services.AddScoped<IClassSubjectTeacherMappingService, ClassSubjectTeacherMappingService>();

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
