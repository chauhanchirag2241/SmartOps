namespace SmartOps.Application.Modules.Identity.DTOs;

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

public sealed class UpdateRoleMenuPermissionsDto
{
    public IReadOnlyList<RoleMenuPermissionDto> Permissions { get; set; } = Array.Empty<RoleMenuPermissionDto>();
}
