using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Application.Modules.Notice;
using SmartOps.Application.Modules.Notice.Interfaces;
using SmartOps.Application.Modules.Workflow;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;
using SmartOps.Domain.Modules.Notice;
using SmartOps.Domain.Modules.Notice.Entities;
using SmartOps.Domain.Modules.Workflow;
using SmartOps.Domain.Modules.Workflow.Entities;

namespace SmartOps.Infrastructure.Modules.Workflow.Services;

public sealed class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepo;
    private readonly ILeaveRepository _leaveRepo;
    private readonly ILeaveApproverResolver _leaveApproverResolver;
    private readonly INoticeRepository _noticeRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IWorkflowRepository workflowRepo,
        ILeaveRepository leaveRepo,
        ILeaveApproverResolver leaveApproverResolver,
        INoticeRepository noticeRepo,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        ILogger<WorkflowService> logger)
    {
        _workflowRepo = workflowRepo;
        _leaveRepo = leaveRepo;
        _leaveApproverResolver = leaveApproverResolver;
        _noticeRepo = noticeRepo;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Result<IList<MyActionListItemDto>>> GetMyActionsAsync(
        short? itemType,
        string? search,
        CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        IList<WorkflowItemEntity> items =
            await _workflowRepo.GetPendingForUserAsync(userId, itemType, search, ct).ConfigureAwait(false);

        IList<MyActionListItemDto> list = items.Select(i => new MyActionListItemDto(
            i.Id,
            i.ItemType,
            i.ItemType.ToString(),
            i.Title,
            i.Summary,
            i.DueDate,
            i.Priority,
            i.CreatedOn)).ToList();

        return Result<IList<MyActionListItemDto>>.Success(list);
    }

    public async Task<Result<MyActionStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        MyActionStatsRow row = await _workflowRepo.GetStatsForUserAsync(userId, ct).ConfigureAwait(false);
        return Result<MyActionStatsDto>.Success(new MyActionStatsDto(
            row.TotalPending,
            row.LeaveApprovals,
            row.NoticeResponses,
            row.FormFills));
    }

    public async Task<Result<MyActionDetailDto>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        WorkflowItemEntity? item = await _workflowRepo.GetItemByIdAsync(id, ct).ConfigureAwait(false);
        if (item is null || item.AssigneeUserId != userId)
        {
            return Result<MyActionDetailDto>.Failure("Action not found.");
        }

        LeaveDetailDto? leave = null;
        NoticeDetailDto? notice = null;

        if (item.ReferenceType == WorkflowReferenceType.LeaveRequest)
        {
            LeaveDetailRow? row = await _leaveRepo.GetDetailRowAsync(item.ReferenceId, ct).ConfigureAwait(false);
            if (row is not null)
            {
                leave = MapLeaveDetail(row);
            }
        }
        else if (item.ReferenceType == WorkflowReferenceType.Notice)
        {
            NoticeEntity? n = await _noticeRepo.GetByIdAsync(item.ReferenceId, ct).ConfigureAwait(false);
            if (n is not null)
            {
                notice = MapNoticeDetail(n);
            }
        }

        return Result<MyActionDetailDto>.Success(new MyActionDetailDto(
            item.Id,
            item.ItemType,
            item.ItemType.ToString(),
            item.Title,
            item.Summary,
            item.DueDate,
            item.Status,
            item.Status.ToString(),
            item.ReferenceType,
            item.ReferenceId,
            item.PayloadJson,
            leave,
            notice));
    }

    public async Task<Result<MyActionDetailDto>> CompleteAsync(
        Guid id,
        CompleteMyActionRequestDto request,
        CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        WorkflowItemEntity? item = await _workflowRepo.GetItemByIdAsync(id, ct).ConfigureAwait(false);
        if (item is null || item.AssigneeUserId != userId)
        {
            return Result<MyActionDetailDto>.Failure("Action not found.");
        }

        if (item.Status != WorkflowItemStatus.Pending)
        {
            return Result<MyActionDetailDto>.Failure("This action is no longer pending.");
        }

        string code = request.ActionCode?.Trim() ?? "";

        if (item.ItemType == WorkflowItemType.LeaveApproval)
        {
            string approvalMode = ParseApprovalModeFromPayload(item.PayloadJson);

            if (code.Equals(WorkflowActionCodes.Approve, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(approvalMode, LeaveApprovalModes.AllMust, StringComparison.OrdinalIgnoreCase))
                {
                    Result validation = await ValidateLeaveApprovalActionAsync(item.ReferenceId, userId, ct)
                        .ConfigureAwait(false);
                    if (!validation.IsSuccess)
                    {
                        return Result<MyActionDetailDto>.Failure(validation.Error!);
                    }

                    await CompleteItemAsync(item, userId, WorkflowActionCodes.Approve, request.Comment, ct)
                        .ConfigureAwait(false);

                    int pending = await _workflowRepo
                        .CountPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, item.ReferenceId, ct)
                        .ConfigureAwait(false);
                    if (pending == 0)
                    {
                        Result<LeaveDetailDto> result = await FinalizeLeaveApprovalAsync(item.ReferenceId, userId, request.Comment, ct)
                            .ConfigureAwait(false);
                        if (!result.IsSuccess)
                        {
                            return Result<MyActionDetailDto>.Failure(result.Error!);
                        }
                    }
                }
                else
                {
                    Result<LeaveDetailDto> result = await FinalizeLeaveApprovalAsync(item.ReferenceId, userId, request.Comment, ct)
                        .ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        return Result<MyActionDetailDto>.Failure(result.Error!);
                    }

                    await CompleteItemAsync(item, userId, WorkflowActionCodes.Approve, request.Comment, ct)
                        .ConfigureAwait(false);
                    await _workflowRepo
                        .CancelPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, item.ReferenceId, ct)
                        .ConfigureAwait(false);
                }
            }
            else if (code.Equals(WorkflowActionCodes.Reject, StringComparison.OrdinalIgnoreCase))
            {
                Result<LeaveDetailDto> result = await RejectLeaveFromWorkflowAsync(item.ReferenceId, request.Comment, ct)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return Result<MyActionDetailDto>.Failure(result.Error!);
                }

                await CompleteItemAsync(item, userId, WorkflowActionCodes.Reject, request.Comment, ct).ConfigureAwait(false);
            }
            else
            {
                return Result<MyActionDetailDto>.Failure("Invalid action. Use Approve or Reject.");
            }
        }
        else if (item.ItemType is WorkflowItemType.NoticeResponse or WorkflowItemType.FormFill)
        {
            Result<MyActionDetailDto> noticeResult = await CompleteNoticeActionAsync(item, userId, code, request, ct)
                .ConfigureAwait(false);
            if (!noticeResult.IsSuccess)
            {
                return noticeResult;
            }
        }
        else
        {
            return Result<MyActionDetailDto>.Failure("This action type is not supported yet.");
        }

        return await GetDetailAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> CreateLeaveApprovalTasksAsync(Guid leaveRequestId, CancellationToken ct = default)
    {
        LeaveRequestEntity? leave = await _leaveRepo.GetByIdAsync(leaveRequestId, ct).ConfigureAwait(false);
        if (leave is null || leave.Status != LeaveRequestStatus.Submitted)
        {
            return Result.Failure("Leave request is not in submitted status.");
        }

        await _workflowRepo.CancelPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, leaveRequestId, ct)
            .ConfigureAwait(false);

        if (!TryGetSchoolId(out Guid schoolId))
        {
            return Result.Failure("School context is required.");
        }

        LeaveApproverResolution resolution = await _leaveApproverResolver
            .ResolveAsync(leave, schoolId, ct)
            .ConfigureAwait(false);
        IList<Guid> assignees = resolution.AssigneeUserIds.ToList();
        if (assignees.Count == 0)
        {
            return Result.Failure("No approver could be resolved for this leave request.");
        }

        LeaveDetailRow? detail = await _leaveRepo.GetDetailRowAsync(leaveRequestId, ct).ConfigureAwait(false);
        string title = leave.RequestType == LeaveRequestType.Staff
            ? $"Staff leave approval"
            : $"Student leave approval";
        string summary = detail is null
            ? $"{leave.FromDate:yyyy-MM-dd} to {leave.ToDate:yyyy-MM-dd}"
            : $"{detail.StudentFirstName} {detail.StudentLastName} / {detail.TeacherFirstName} — {leave.FromDate:yyyy-MM-dd} to {leave.ToDate:yyyy-MM-dd}".Trim();

        string payload = JsonSerializer.Serialize(new
        {
            leaveRequestId,
            leave.RequestType,
            leave.FromDate,
            leave.ToDate,
            approvalMode = resolution.ApprovalMode
        });

        foreach (Guid assigneeId in assignees.Distinct())
        {
            if (assigneeId == leave.RequestedByUserId)
            {
                continue;
            }

            var item = new WorkflowItemEntity
            {
                AssigneeUserId = assigneeId,
                ItemType = WorkflowItemType.LeaveApproval,
                Status = WorkflowItemStatus.Pending,
                ReferenceType = WorkflowReferenceType.LeaveRequest,
                ReferenceId = leaveRequestId,
                Title = title,
                Summary = summary.Trim(),
                DueDate = leave.ToDate,
                Priority = 1,
                PayloadJson = payload
            };

            await _workflowRepo.CreateItemAsync(item, ct).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public Task CancelPendingForLeaveAsync(Guid leaveRequestId, CancellationToken ct = default) =>
        _workflowRepo.CancelPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, leaveRequestId, ct);

    private async Task<Result> ValidateLeaveApprovalActionAsync(Guid leaveId, Guid userId, CancellationToken ct)
    {
        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(leaveId, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure("Leave request not found.");
        }

        if (entity.Status != LeaveRequestStatus.Submitted)
        {
            return Result.Failure("Only submitted requests can be approved.");
        }

        if (entity.RequestedByUserId == userId)
        {
            return Result.Failure("You cannot approve your own leave request.");
        }

        return Result.Success();
    }

    private async Task<Result<LeaveDetailDto>> FinalizeLeaveApprovalAsync(
        Guid leaveId,
        Guid userId,
        string? remark,
        CancellationToken ct)
    {
        Result validation = await ValidateLeaveApprovalActionAsync(leaveId, userId, ct).ConfigureAwait(false);
        if (!validation.IsSuccess)
        {
            return Result<LeaveDetailDto>.Failure(validation.Error!);
        }

        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(leaveId, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveDetailDto>.Failure("Leave request not found.");
        }

        entity.Status = LeaveRequestStatus.Approved;
        entity.ApprovedByUserId = userId;
        entity.ApprovedOn = DateTimeOffset.UtcNow;
        entity.ApproverRemark = remark;
        await _leaveRepo.UpdateAsync(entity, ct).ConfigureAwait(false);
        await _workflowRepo.CancelPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, leaveId, ct)
            .ConfigureAwait(false);

        LeaveDetailRow? row = await _leaveRepo.GetDetailRowAsync(leaveId, ct).ConfigureAwait(false);
        return row is null
            ? Result<LeaveDetailDto>.Failure("Leave request not found.")
            : Result<LeaveDetailDto>.Success(MapLeaveDetail(row));
    }

    private async Task<Result<LeaveDetailDto>> RejectLeaveFromWorkflowAsync(Guid leaveId, string? remark, CancellationToken ct)
    {
        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(leaveId, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveDetailDto>.Failure("Leave request not found.");
        }

        if (entity.Status != LeaveRequestStatus.Submitted)
        {
            return Result<LeaveDetailDto>.Failure("Only submitted requests can be rejected.");
        }

        Guid userId = RequireUserId();
        if (entity.RequestedByUserId == userId)
        {
            return Result<LeaveDetailDto>.Failure("You cannot reject your own leave request.");
        }

        entity.Status = LeaveRequestStatus.Rejected;
        entity.ApprovedByUserId = userId;
        entity.ApprovedOn = DateTimeOffset.UtcNow;
        entity.ApproverRemark = remark;
        await _leaveRepo.UpdateAsync(entity, ct).ConfigureAwait(false);
        await _workflowRepo.CancelPendingForReferenceAsync(WorkflowReferenceType.LeaveRequest, leaveId, ct)
            .ConfigureAwait(false);

        LeaveDetailRow? row = await _leaveRepo.GetDetailRowAsync(leaveId, ct).ConfigureAwait(false);
        return row is null
            ? Result<LeaveDetailDto>.Failure("Leave request not found.")
            : Result<LeaveDetailDto>.Success(MapLeaveDetail(row));
    }

    private async Task CompleteItemAsync(
        WorkflowItemEntity item,
        Guid userId,
        string outcome,
        string? comment,
        CancellationToken ct)
    {
        item.Status = WorkflowItemStatus.Completed;
        item.CompletedByUserId = userId;
        item.CompletedOn = DateTimeOffset.UtcNow;
        item.Outcome = outcome;
        await _workflowRepo.UpdateItemAsync(item, ct).ConfigureAwait(false);
        await _workflowRepo.InsertActionAsync(item.Id, outcome, comment, userId, null, ct).ConfigureAwait(false);
    }

    private async Task<Result<MyActionDetailDto>> CompleteNoticeActionAsync(
        WorkflowItemEntity item,
        Guid userId,
        string code,
        CompleteMyActionRequestDto request,
        CancellationToken ct)
    {
        NoticeEntity? notice = await _noticeRepo.GetByIdAsync(item.ReferenceId, ct).ConfigureAwait(false);
        if (notice is null || notice.Status != NoticeStatus.Published)
        {
            return Result<MyActionDetailDto>.Failure("Notice not found or not published.");
        }

        if (item.ItemType == WorkflowItemType.FormFill)
        {
            if (!code.Equals(WorkflowActionCodes.SubmitForm, StringComparison.OrdinalIgnoreCase))
            {
                return Result<MyActionDetailDto>.Failure("Invalid action. Use SubmitForm.");
            }

            string body = request.Payload?.Trim() ?? request.Comment?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(body))
            {
                return Result<MyActionDetailDto>.Failure("Form answers are required.");
            }

            await _noticeRepo.UpsertResponseAsync(item.ReferenceId, userId, body, ct).ConfigureAwait(false);
            await CompleteItemAsync(item, userId, WorkflowActionCodes.SubmitForm, body, ct).ConfigureAwait(false);
            return Result<MyActionDetailDto>.Success((await GetDetailAsync(item.Id, ct).ConfigureAwait(false)).Value!);
        }

        if (code.Equals(WorkflowActionCodes.Acknowledge, StringComparison.OrdinalIgnoreCase))
        {
            if (notice.RequiresResponse)
            {
                return Result<MyActionDetailDto>.Failure("This notice requires a written response.");
            }

            await CompleteItemAsync(item, userId, WorkflowActionCodes.Acknowledge, null, ct).ConfigureAwait(false);
            return Result<MyActionDetailDto>.Success((await GetDetailAsync(item.Id, ct).ConfigureAwait(false)).Value!);
        }

        if (code.Equals(WorkflowActionCodes.Respond, StringComparison.OrdinalIgnoreCase))
        {
            string body = request.Payload?.Trim() ?? request.Comment?.Trim() ?? "";
            if (notice.RequiresResponse && string.IsNullOrWhiteSpace(body))
            {
                return Result<MyActionDetailDto>.Failure("Response is required.");
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                await _noticeRepo.UpsertResponseAsync(item.ReferenceId, userId, body, ct).ConfigureAwait(false);
            }

            await CompleteItemAsync(item, userId, WorkflowActionCodes.Respond, body, ct).ConfigureAwait(false);
            return Result<MyActionDetailDto>.Success((await GetDetailAsync(item.Id, ct).ConfigureAwait(false)).Value!);
        }

        return Result<MyActionDetailDto>.Failure("Invalid action. Use Respond or Acknowledge.");
    }

    private static NoticeDetailDto MapNoticeDetail(NoticeEntity n) => new(
        n.Id,
        n.Title,
        n.Body,
        n.RequiresResponse,
        n.ResponseDeadline,
        n.Status.ToString(),
        n.TargetType,
        n.TargetType.ToString(),
        n.TargetRefId,
        n.ContentType,
        n.ContentType.ToString(),
        NoticeContentSerializer.Deserialize(n.ContentJson));

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        return _currentUser.UserId;
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = _tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }

    private static string ParseApprovalModeFromPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return LeaveApprovalModes.AnyOne;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("approvalMode", out JsonElement mode)
                && mode.ValueKind == JsonValueKind.String)
            {
                return mode.GetString() ?? LeaveApprovalModes.AnyOne;
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return LeaveApprovalModes.AnyOne;
    }

    private static LeaveDetailDto MapLeaveDetail(LeaveDetailRow r)
    {
        int days = r.ToDate.DayNumber - r.FromDate.DayNumber + 1;
        string? teacherName = string.Join(" ", new[] { r.TeacherFirstName, r.TeacherLastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        string? studentName = string.Join(" ", new[] { r.StudentFirstName, r.StudentLastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        return new LeaveDetailDto(
            r.Id,
            (LeaveRequestType)r.RequestType,
            ((LeaveRequestType)r.RequestType).ToString(),
            r.TeacherId,
            string.IsNullOrEmpty(teacherName) ? null : teacherName,
            r.StudentId,
            string.IsNullOrEmpty(studentName) ? null : studentName,
            r.ClassName,
            r.RequestedByUserId,
            r.RequestedByEmail,
            r.FromDate,
            r.ToDate,
            days,
            r.LeaveType.HasValue ? (LeaveType)r.LeaveType : null,
            r.LeaveType.HasValue ? ((LeaveType)r.LeaveType).ToString() : null,
            r.Reason,
            (LeaveRequestStatus)r.Status,
            ((LeaveRequestStatus)r.Status).ToString(),
            r.ApprovedByUserId,
            r.ApprovedByEmail,
            r.ApprovedOn,
            r.ApproverRemark,
            r.CreatedOn);
    }
}
