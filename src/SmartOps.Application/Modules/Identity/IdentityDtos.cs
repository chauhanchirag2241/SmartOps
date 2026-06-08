using SmartOps.Application.Modules.School;
using SmartOps.Domain.Modules.Identity.Entities;
using SchoolEntity = SmartOps.Domain.Modules.School.Entities.SchoolEntity;

namespace SmartOps.Application.Modules.Identity;

public sealed class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }
}

public static class IdentityMappingExtensions
{
    public static UserDto ToUserDto(this ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        IsActive = user.IsActive,
        CreatedOn = user.CreatedOn
    };

    public static SchoolDto ToDto(this SchoolEntity school) => new()
    {
        Id = school.Id,
        Name = school.Name,
        Subdomain = school.Subdomain,
        IsActive = school.IsActive
    };
}

public sealed class MenuDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Route { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public IReadOnlyList<MenuDto> Children { get; set; } = Array.Empty<MenuDto>();
}

public sealed class MenuPermissionDto
{
    public string MenuCode { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanExport { get; set; }
}

public sealed class UserPermissionResponseDto
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    public IReadOnlyList<MenuPermissionDto> Permissions { get; set; } = Array.Empty<MenuPermissionDto>();
}

public sealed class RoleMenuPermissionDto
{
    public Guid MenuId { get; set; }

    public string MenuCode { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanExport { get; set; }
}

public sealed class RoleDashboardWidgetPermissionDto
{
    public Guid WidgetId { get; set; }

    public string WidgetCode { get; set; } = string.Empty;

    public string WidgetName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string RequiredMenuCode { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string DefaultSize { get; set; } = "stat";

    public bool CanView { get; set; }
}

public sealed class UpdateRoleDashboardWidgetPermissionsDto
{
    public IReadOnlyList<RoleDashboardWidgetPermissionDto> Permissions { get; set; } =
        Array.Empty<RoleDashboardWidgetPermissionDto>();
}

public sealed class UpdateRoleMenuPermissionsDto
{
    public IReadOnlyList<RoleMenuPermissionDto> Permissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();
}

public sealed class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<RoleMenuPermissionDto> MenuPermissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();

    public IReadOnlyList<RoleDashboardWidgetPermissionDto> DashboardWidgetPermissions { get; set; } =
        Array.Empty<RoleDashboardWidgetPermissionDto>();
}

public sealed class CreateRoleDto
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<RoleMenuPermissionDto> MenuPermissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();

    public IReadOnlyList<RoleDashboardWidgetPermissionDto> DashboardWidgetPermissions { get; set; } =
        Array.Empty<RoleDashboardWidgetPermissionDto>();
}

public sealed class UpdateRoleDto
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SchoolUserDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;

    public Guid? UserTypeId { get; set; }

    public string? UserTypeCode { get; set; }

    public string? UserTypeName { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public sealed class CreateUserDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;

    public Guid? UserTypeId { get; set; }

    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
}

public sealed class UpdateUserDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;

    public Guid? UserTypeId { get; set; }
}

public sealed class UpdateUserRolesDto
{
    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
}

public sealed class ResetUserPasswordDto
{
    public string Password { get; set; } = string.Empty;
}

public sealed class AssignRoleUsersDto
{
    public IReadOnlyList<Guid> UserIds { get; set; } = Array.Empty<Guid>();
}

public sealed class UserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedOn { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public Guid? RoleId { get; set; }

    public string? RoleCode { get; set; }
}