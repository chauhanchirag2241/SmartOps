using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave;

namespace SmartOps.Domain.Modules.Leave.Entities;

public sealed class LeaveRequestEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public LeaveRequestType RequestType { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public LeaveType? LeaveType { get; set; }
    public string? Reason { get; set; }
    public LeaveRequestStatus Status { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedOn { get; set; }
    public string? ApproverRemark { get; set; }
}
