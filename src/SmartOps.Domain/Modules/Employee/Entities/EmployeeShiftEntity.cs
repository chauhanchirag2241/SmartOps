using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Employee.Entities;

public sealed class EmployeeShiftEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ShiftId { get; set; }
}
