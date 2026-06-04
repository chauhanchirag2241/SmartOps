using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Workflow.Interfaces;

public interface IWorkflowService
{
    Task<Result<IList<MyActionListItemDto>>> GetMyActionsAsync(short? itemType, string? search, CancellationToken ct = default);
    Task<Result<MyActionStatsDto>> GetStatsAsync(CancellationToken ct = default);
    Task<Result<MyActionDetailDto>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<Result<MyActionDetailDto>> CompleteAsync(Guid id, CompleteMyActionRequestDto request, CancellationToken ct = default);
    Task<Result> CreateLeaveApprovalTasksAsync(Guid leaveRequestId, CancellationToken ct = default);
    Task CancelPendingForLeaveAsync(Guid leaveRequestId, CancellationToken ct = default);
}
