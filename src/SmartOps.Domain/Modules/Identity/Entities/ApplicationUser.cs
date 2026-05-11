using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class ApplicationUser : AuditableEntity
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? SecurityStamp { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    public bool LockoutEnabled { get; set; }
}
