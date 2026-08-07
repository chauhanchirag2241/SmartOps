using SmartOps.Domain.Modules.Leave;

namespace SmartOps.Domain.Modules.Leave.Entities;

public sealed class LeaveHalfDayEntity
{
    public Guid Id { get; set; }
    public Guid LeaveRequestId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public LeaveHalfDaySession Session { get; set; }
}
