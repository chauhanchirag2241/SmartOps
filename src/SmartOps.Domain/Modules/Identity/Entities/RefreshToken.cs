using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class RefreshToken : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }
}
