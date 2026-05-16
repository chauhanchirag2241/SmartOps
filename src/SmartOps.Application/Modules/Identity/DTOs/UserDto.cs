namespace SmartOps.Application.Modules.Identity.DTOs;

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
