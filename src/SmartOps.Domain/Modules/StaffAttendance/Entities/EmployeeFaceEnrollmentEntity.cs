using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.StaffAttendance.Entities;

public class EmployeeFaceEnrollmentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public float[] Embedding { get; set; } = [];
    public string? PhotoUrl { get; set; }
    public string ModelName { get; set; } = "buffalo_l";
}
