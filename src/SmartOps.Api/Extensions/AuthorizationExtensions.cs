using Microsoft.AspNetCore.Authorization;
using SmartOps.Api.Authorization;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddMenuPermissionPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, MenuPermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, MenuAnyPermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            foreach (string menuCode in MenuCodes.All)
            {
                RegisterPolicy(options, menuCode, MenuPermissionAction.View);
                RegisterPolicy(options, menuCode, MenuPermissionAction.Add);
                RegisterPolicy(options, menuCode, MenuPermissionAction.Edit);
                RegisterPolicy(options, menuCode, MenuPermissionAction.Delete);
                RegisterPolicy(options, menuCode, MenuPermissionAction.Export);
            }

            RegisterAnyPolicy(
                options,
                MenuPolicies.Students.ListForAttendanceOrModule,
                (MenuCodes.Students, MenuPermissionAction.View),
                (MenuCodes.Attendance, MenuPermissionAction.View),
                (MenuCodes.Homework, MenuPermissionAction.View));

            RegisterAnyPolicy(
                options,
                MenuPolicies.Classes.ListForAttendanceDropdown,
                (MenuCodes.Classes, MenuPermissionAction.View),
                (MenuCodes.Attendance, MenuPermissionAction.View),
                (MenuCodes.Homework, MenuPermissionAction.View));

            RegisterAnyPolicy(
                options,
                MenuPolicies.FrontOfficeEmployeeLookup,
                (MenuCodes.Complaints, MenuPermissionAction.View),
                (MenuCodes.AdmissionInquiries, MenuPermissionAction.View),
                (MenuCodes.VisitorBook, MenuPermissionAction.View),
                (MenuCodes.PhoneLogs, MenuPermissionAction.View),
                (MenuCodes.FrontOfficeSetup, MenuPermissionAction.View));
        });

        return services;
    }

    private static void RegisterAnyPolicy(
        AuthorizationOptions options,
        string policyName,
        params (string MenuCode, MenuPermissionAction Action)[] requirements)
    {
        options.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new MenuAnyPermissionRequirement(requirements)));
    }

    private static void RegisterPolicy(AuthorizationOptions options, string menuCode, MenuPermissionAction action)
    {
        string policyName = MenuPolicy.For(menuCode, action);
        options.AddPolicy(policyName, policy =>
            policy.Requirements.Add(new MenuPermissionRequirement(menuCode, action)));
    }
}
