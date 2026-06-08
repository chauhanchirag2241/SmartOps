using SmartOps.Domain.Modules.Workflow;
using SmartOps.Domain.Modules.Workflow.Entities;

namespace SmartOps.Application.Modules.Workflow.Interfaces;

public interface IWorkflowRepository
{
    Task<Guid> CreateItemAsync(WorkflowItemEntity item, CancellationToken ct = default);
    Task UpdateItemAsync(WorkflowItemEntity item, CancellationToken ct = default);
    Task<WorkflowItemEntity?> GetItemByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<WorkflowItemEntity>> GetPendingForUserAsync(Guid userId, short? itemType, string? search, CancellationToken ct = default);
    Task<MyActionStatsRow> GetStatsForUserAsync(Guid userId, CancellationToken ct = default);
    Task CancelPendingForReferenceAsync(WorkflowReferenceType refType, Guid refId, CancellationToken ct = default);
    Task<Guid> InsertActionAsync(Guid workflowItemId, string actionCode, string? comment, Guid actorUserId, string? metadataJson, CancellationToken ct = default);
    Task<WorkflowItemEntity?> GetPendingByReferenceForUserAsync(WorkflowReferenceType refType, Guid refId, Guid userId, CancellationToken ct = default);
    Task<int> CountPendingForReferenceAsync(WorkflowReferenceType refType, Guid refId, CancellationToken ct = default);
}

public sealed class MyActionStatsRow
{
    public int TotalPending { get; set; }
    public int LeaveApprovals { get; set; }
    public int NoticeResponses { get; set; }
    public int FormFills { get; set; }
}
