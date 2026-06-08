using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveApproverResolver
{
    Task<LeaveApproverResolution> ResolveAsync(LeaveRequestEntity leave, Guid schoolId, CancellationToken ct = default);
}

public sealed class LeaveApproverResolution
{
    public IReadOnlyList<Guid> AssigneeUserIds { get; set; } = Array.Empty<Guid>();

    public string ApprovalMode { get; set; } = string.Empty;
}
