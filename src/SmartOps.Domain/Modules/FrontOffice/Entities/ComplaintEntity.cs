using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Modules.FrontOffice;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

[TrackHistory]
public sealed class ComplaintEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ComplaintTypeId { get; set; }
    public DateOnly ComplaintDate { get; set; }
    public bool IsAnonymous { get; set; }
    public string? ComplainantName { get; set; }
    public string? Phone { get; set; }
    public string Description { get; set; } = null!;
    public Guid AssignedToEmployeeId { get; set; }
    public ComplaintStatus Status { get; set; }
    public string? ActionTaken { get; set; }
    public string? Note { get; set; }
    public string? DocumentPath { get; set; }
}
