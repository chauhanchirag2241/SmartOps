using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class SchoolBranchEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public bool IsHeadOffice { get; set; }
}
