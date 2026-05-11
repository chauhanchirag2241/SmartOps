namespace SmartOps.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Each concrete entity declares its own primary key (<c>Id</c> or composite keys).
/// </summary>
public abstract class AuditableEntity
{
    public bool IsActive { get; set; } = true;

    public int VersionNo { get; set; } = 1;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public Guid UpdatedBy { get; set; }

    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
}
