using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Shared.Constants;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IPermissionService
{
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<MenuPermissionDto> GetPermissions();

    MenuPermissionDto? GetPermission(string menuCode);

    bool HasViewAccess(string menuCode);

    bool HasAddAccess(string menuCode);

    bool HasEditAccess(string menuCode);

    bool HasDeleteAccess(string menuCode);

    bool HasExportAccess(string menuCode);

    bool HasAccess(string menuCode, MenuPermissionAction action);
}
