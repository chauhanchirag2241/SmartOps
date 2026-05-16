namespace SmartOps.Application.Modules.Identity.DTOs;

public sealed class RoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<RoleMenuPermissionDto> MenuPermissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();
}

public sealed class CreateRoleDto
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<RoleMenuPermissionDto> MenuPermissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();
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

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public sealed class CreateUserDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;

    public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
}

public sealed class UpdateUserDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool LockoutEnabled { get; set; } = true;
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
