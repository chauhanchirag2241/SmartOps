using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Notice;
using SmartOps.Domain.Modules.Workflow;

namespace SmartOps.Application.Modules.Workflow;

public record MyActionListItemDto(
    Guid Id,
    WorkflowItemType ItemType,
    string ItemTypeLabel,
    string Title,
    string? Summary,
    DateOnly? DueDate,
    int Priority,
    DateTime CreatedOn);

public record MyActionStatsDto(
    int TotalPending,
    int LeaveApprovals,
    int NoticeResponses,
    int FormFills);

public record CompleteMyActionRequestDto(
    string ActionCode,
    string? Comment,
    string? Payload);

public record MyActionDetailDto(
    Guid Id,
    WorkflowItemType ItemType,
    string ItemTypeLabel,
    string Title,
    string? Summary,
    DateOnly? DueDate,
    WorkflowItemStatus Status,
    string StatusLabel,
    WorkflowReferenceType ReferenceType,
    Guid ReferenceId,
    string? PayloadJson,
    LeaveDetailDto? LeaveRequest,
    NoticeDetailDto? Notice);
