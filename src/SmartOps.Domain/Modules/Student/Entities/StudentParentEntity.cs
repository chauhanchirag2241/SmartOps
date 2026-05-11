using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentParentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string RelationType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Occupation { get; set; }
}
