namespace SmartOps.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Each concrete entity declares its own primary key (<c>Id</c> or composite keys).
/// </summary>
public abstract class AuditableEntity
{
    public bool IsActive { get; set; } = true;

    public int VersionNo { get; set; } = 1;

    public Guid CreatedBy { get; set; }

    /// <summary>Stored as school local wall-clock (IST).</summary>
    public DateTime CreatedOn { get; set; }

    public Guid UpdatedBy { get; set; }

    /// <summary>Stored as school local wall-clock (IST).</summary>
    public DateTime UpdatedOn { get; set; }
}
