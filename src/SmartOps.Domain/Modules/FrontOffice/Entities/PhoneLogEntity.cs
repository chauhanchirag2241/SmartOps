using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Modules.FrontOffice;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

[TrackHistory]
public sealed class PhoneLogEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string CallerName { get; set; } = null!;
    public string? Phone { get; set; }
    public CallType CallType { get; set; }
    public DateOnly CallDate { get; set; }
    public string? Duration { get; set; }
    public string Description { get; set; } = null!;
    public DateOnly? NextFollowUpDate { get; set; }
    public string? Note { get; set; }
}
