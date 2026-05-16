using Microsoft.AspNetCore.Authorization;
using SmartOps.Api.Authorization;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            RegisterPolicy(options, PermissionNames.StudentRead);
            RegisterPolicy(options, PermissionNames.StudentCreate);
            RegisterPolicy(options, PermissionNames.StudentUpdate);
            RegisterPolicy(options, PermissionNames.StudentDelete);
            RegisterPolicy(options, PermissionNames.AttendanceRead);
            RegisterPolicy(options, PermissionNames.AttendanceMark);
            RegisterPolicy(options, PermissionNames.FeesRead);
            RegisterPolicy(options, PermissionNames.FeesCreate);
            RegisterPolicy(options, PermissionNames.FeesUpdate);
            RegisterPolicy(options, PermissionNames.ExamsRead);
            RegisterPolicy(options, PermissionNames.ExamsCreate);
            RegisterPolicy(options, PermissionNames.HrRead);
            RegisterPolicy(options, PermissionNames.HrManage);
            RegisterPolicy(options, PermissionNames.ReportsView);
            RegisterPolicy(options, PermissionNames.AdminFull);
            RegisterPolicy(options, PermissionNames.TeacherRead);
            RegisterPolicy(options, PermissionNames.ClassRead);
            RegisterPolicy(options, PermissionNames.SubjectRead);
            RegisterPolicy(options, PermissionNames.AcademicYearRead);
            RegisterPolicy(options, PermissionNames.RolesManage);
            RegisterPolicy(options, PermissionNames.SettingsRead);
        });

        return services;
    }

    private static void RegisterPolicy(AuthorizationOptions options, string permission)
    {
        options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionRequirement(permission)));
    }
}
