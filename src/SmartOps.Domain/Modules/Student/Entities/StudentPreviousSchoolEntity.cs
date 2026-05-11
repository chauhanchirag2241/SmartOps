using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentPreviousSchoolEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? SchoolName { get; set; }
    public string? LastClassPassed { get; set; }
    public string? PercentageOrCgpa { get; set; }
    public string? TcNumber { get; set; }
}
