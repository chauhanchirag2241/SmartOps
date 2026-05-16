namespace SmartOps.Application.Modules.Identity.DTOs;

public sealed class RoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class PermissionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class UpdateRolePermissionsDto
{
    public IReadOnlyList<string> PermissionNames { get; set; } = Array.Empty<string>();
}

public sealed class SchoolUserDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
