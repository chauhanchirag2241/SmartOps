using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Application.Modules.Notice;
using SmartOps.Application.Modules.Notice.Interfaces;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Notice;
using SmartOps.Domain.Modules.Notice.Entities;
using SmartOps.Domain.Modules.Workflow;
using SmartOps.Domain.Modules.Workflow.Entities;

namespace SmartOps.Infrastructure.Modules.Notice.Services;

public sealed class NoticeService : INoticeService
{
    private readonly INoticeRepository _noticeRepo;
    private readonly ILeaveRepository _leaveRepo;
    private readonly IWorkflowRepository _workflowRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<NoticeService> _logger;

    public NoticeService(
        INoticeRepository noticeRepo,
        ILeaveRepository leaveRepo,
        IWorkflowRepository workflowRepo,
        ICurrentUserService currentUser,
        ILogger<NoticeService> logger)
    {
        _noticeRepo = noticeRepo;
        _leaveRepo = leaveRepo;
        _workflowRepo = workflowRepo;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IList<NoticeListItemDto>>> GetListAsync(CancellationToken ct = default)
    {
        IList<NoticeListRow> rows = await _noticeRepo.GetListAsync(ct).ConfigureAwait(false);
        return Result<IList<NoticeListItemDto>>.Success(rows.Select(MapList).ToList());
    }

    public async Task<Result<NoticeDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        NoticeEntity? entity = await _noticeRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        return entity is null
            ? Result<NoticeDetailDto>.Failure("Notice not found.")
            : Result<NoticeDetailDto>.Success(MapDetail(entity));
    }

    public async Task<Result<NoticeDetailDto>> CreateAsync(CreateNoticeRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return Result<NoticeDetailDto>.Failure("Title and body are required.");
        }

        var entity = new NoticeEntity
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            CreatedByUserId = RequireUserId(),
            RequiresResponse = request.RequiresResponse,
            ResponseDeadline = request.ResponseDeadline,
            TargetType = request.TargetType,
            TargetRefId = request.TargetRefId,
            Status = NoticeStatus.Draft
        };

        Guid id = await _noticeRepo.CreateAsync(entity, ct).ConfigureAwait(false);
        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<NoticeDetailDto>> UpdateAsync(Guid id, UpdateNoticeRequestDto request, CancellationToken ct = default)
    {
        NoticeEntity? entity = await _noticeRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<NoticeDetailDto>.Failure("Notice not found.");
        }

        if (entity.Status != NoticeStatus.Draft)
        {
            return Result<NoticeDetailDto>.Failure("Only draft notices can be edited.");
        }

        entity.Title = request.Title.Trim();
        entity.Body = request.Body.Trim();
        entity.RequiresResponse = request.RequiresResponse;
        entity.ResponseDeadline = request.ResponseDeadline;
        entity.TargetType = request.TargetType;
        entity.TargetRefId = request.TargetRefId;
        await _noticeRepo.UpdateAsync(entity, ct).ConfigureAwait(false);
        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<NoticeDetailDto>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        NoticeEntity? entity = await _noticeRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<NoticeDetailDto>.Failure("Notice not found.");
        }

        if (entity.Status != NoticeStatus.Draft)
        {
            return Result<NoticeDetailDto>.Failure("Only draft notices can be published.");
        }

        entity.Status = NoticeStatus.Published;
        entity.PublishedOn = DateTimeOffset.UtcNow;
        await _noticeRepo.UpdateAsync(entity, ct).ConfigureAwait(false);

        if (entity.RequiresResponse)
        {
            IList<Guid> targets = await ResolveTargetsAsync(entity, ct).ConfigureAwait(false);
            foreach (Guid userId in targets.Distinct())
            {
                var item = new WorkflowItemEntity
                {
                    AssigneeUserId = userId,
                    ItemType = WorkflowItemType.NoticeResponse,
                    Status = WorkflowItemStatus.Pending,
                    ReferenceType = WorkflowReferenceType.Notice,
                    ReferenceId = id,
                    Title = $"Notice: {entity.Title}",
                    Summary = entity.Body.Length > 200 ? entity.Body[..200] + "…" : entity.Body,
                    DueDate = entity.ResponseDeadline,
                    Priority = 0,
                    PayloadJson = JsonSerializer.Serialize(new { noticeId = id, entity.Title })
                };

                await _workflowRepo.CreateItemAsync(item, ct).ConfigureAwait(false);
            }
        }

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<IList<NoticeResponseItemDto>>> GetResponsesAsync(Guid id, CancellationToken ct = default)
    {
        IList<NoticeResponseRow> rows = await _noticeRepo.GetResponsesAsync(id, ct).ConfigureAwait(false);
        IList<NoticeResponseItemDto> list = rows.Select(r => new NoticeResponseItemDto(
            r.Id,
            r.RespondentUserId,
            r.RespondentEmail,
            r.ResponseBody,
            r.RespondedOn)).ToList();
        return Result<IList<NoticeResponseItemDto>>.Success(list);
    }

    public async Task<Result> RespondAsync(Guid id, RespondToNoticeRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ResponseBody))
        {
            return Result.Failure("Response is required.");
        }

        NoticeEntity? notice = await _noticeRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (notice is null || notice.Status != NoticeStatus.Published)
        {
            return Result.Failure("Notice not found or not published.");
        }

        Guid userId = RequireUserId();
        await _noticeRepo.UpsertResponseAsync(id, userId, request.ResponseBody.Trim(), ct).ConfigureAwait(false);

        WorkflowItemEntity? item = await _workflowRepo
            .GetPendingByReferenceForUserAsync(WorkflowReferenceType.Notice, id, userId, ct)
            .ConfigureAwait(false);

        if (item is not null)
        {
            item.Status = WorkflowItemStatus.Completed;
            item.CompletedByUserId = userId;
            item.CompletedOn = DateTimeOffset.UtcNow;
            item.Outcome = WorkflowActionCodes.Respond;
            await _workflowRepo.UpdateItemAsync(item, ct).ConfigureAwait(false);
            await _workflowRepo.InsertActionAsync(item.Id, WorkflowActionCodes.Respond, request.ResponseBody, userId, null, ct)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<IList<Guid>> ResolveTargetsAsync(NoticeEntity entity, CancellationToken ct)
    {
        return entity.TargetType switch
        {
            NoticeTargetType.AllStaff => await _leaveRepo.GetActiveTeacherUserIdsAsync(ct).ConfigureAwait(false),
            NoticeTargetType.ClassParents when entity.TargetRefId.HasValue =>
                await _leaveRepo.GetParentUserIdsForClassAsync(entity.TargetRefId.Value, ct).ConfigureAwait(false),
            NoticeTargetType.SingleUser when entity.TargetRefId.HasValue =>
                [entity.TargetRefId.Value],
            _ => []
        };
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        return _currentUser.UserId;
    }

    private static NoticeListItemDto MapList(NoticeListRow r) => new(
        r.Id,
        r.Title,
        (NoticeStatus)r.Status,
        ((NoticeStatus)r.Status).ToString(),
        (NoticeTargetType)r.TargetType,
        ((NoticeTargetType)r.TargetType).ToString(),
        r.RequiresResponse,
        r.ResponseDeadline,
        r.PublishedOn,
        r.ResponseCount);

    private static NoticeDetailDto MapDetail(NoticeEntity n) => new(
        n.Id,
        n.Title,
        n.Body,
        n.RequiresResponse,
        n.ResponseDeadline,
        n.Status.ToString());
}
