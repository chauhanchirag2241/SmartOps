using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Class.Entities;

/// <summary>
/// Per-class / per-group settings. Class teacher lives in <see cref="TeacherId"/> for a section.
/// Nullable keys allow reuse for other class-wise settings later.
/// </summary>
public sealed class ClassSettingEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid? ClassGroupId { get; set; }

    /// <summary>Section / class id (<c>classes.id</c>).</summary>
    public Guid? SectionId { get; set; }

    /// <summary>Class teacher employee id when set for this section.</summary>
    public Guid? TeacherId { get; set; }
}
