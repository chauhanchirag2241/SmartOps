using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Application.Modules.Notice;
using SmartOps.Application.Modules.Notice.Interfaces;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicYear;
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
    private readonly IAcademicYearRepository _academicYearRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<NoticeService> _logger;

    public NoticeService(
        INoticeRepository noticeRepo,
        ILeaveRepository leaveRepo,
        IWorkflowRepository workflowRepo,
        IAcademicYearRepository academicYearRepo,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        ILogger<NoticeService> logger)
    {
        _noticeRepo = noticeRepo;
        _leaveRepo = leaveRepo;
        _workflowRepo = workflowRepo;
        _academicYearRepo = academicYearRepo;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Result<IList<NoticeListItemDto>>> GetListAsync(CancellationToken ct = default)
    {
        IList<NoticeListRow> active = await _noticeRepo.GetListAsync(ct).ConfigureAwait(false);
        IList<NoticeListRow> inactive = await _noticeRepo.GetInactiveListAsync(ct).ConfigureAwait(false);
        var all = active.Select(MapList).Concat(inactive.Select(MapList)).ToList();
        return Result<IList<NoticeListItemDto>>.Success(all);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        NoticeEntity? entity = await _noticeRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure("Notice not found.");
        }

        await _noticeRepo.SoftDeleteAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
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
        Result validation = ValidateRequest(request.Title, request.Body, request.ContentType, request.Content, request.TargetType, request.TargetRefId);
        if (!validation.IsSuccess)
        {
            return Result<NoticeDetailDto>.Failure(validation.Error!);
        }

        bool requiresResponse = request.RequiresResponse;
        ApplyContentRules(request.ContentType, ref requiresResponse);
        Guid? targetRefId = ResolveStoredTargetRefId(request.TargetType, request.TargetRefId, request.Content);

        var entity = new NoticeEntity
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            CreatedByUserId = RequireUserId(),
            RequiresResponse = requiresResponse,
            ResponseDeadline = request.ResponseDeadline,
            TargetType = request.TargetType,
            TargetRefId = targetRefId,
            ContentType = request.ContentType,
            ContentJson = NoticeContentSerializer.Serialize(request.Content),
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

        if (!entity.IsActive)
        {
            return Result<NoticeDetailDto>.Failure("Notice not found.");
        }

        if (entity.Status != NoticeStatus.Draft)
        {
            return Result<NoticeDetailDto>.Failure("Only draft notices can be edited.");
        }

        Result validation = ValidateRequest(request.Title, request.Body, request.ContentType, request.Content, request.TargetType, request.TargetRefId);
        if (!validation.IsSuccess)
        {
            return Result<NoticeDetailDto>.Failure(validation.Error!);
        }

        bool requiresResponse = request.RequiresResponse;
        ApplyContentRules(request.ContentType, ref requiresResponse);

        entity.Title = request.Title.Trim();
        entity.Body = request.Body.Trim();
        entity.RequiresResponse = requiresResponse;
        entity.ResponseDeadline = request.ResponseDeadline;
        entity.TargetType = request.TargetType;
        entity.TargetRefId = ResolveStoredTargetRefId(request.TargetType, request.TargetRefId, request.Content);
        entity.ContentType = request.ContentType;
        entity.ContentJson = NoticeContentSerializer.Serialize(request.Content);
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

        if (!entity.IsActive)
        {
            return Result<NoticeDetailDto>.Failure("Notice not found.");
        }

        if (entity.Status != NoticeStatus.Draft)
        {
            return Result<NoticeDetailDto>.Failure("Only draft notices can be published.");
        }

        IList<NoticeRecipient> recipients = await ResolveRecipientsAsync(entity, ct).ConfigureAwait(false);
        if (recipients.Count == 0)
        {
            return Result<NoticeDetailDto>.Failure("No recipients matched the selected audience.");
        }

        entity.Status = NoticeStatus.Published;
        entity.PublishedOn = DateTimeOffset.UtcNow;
        await _noticeRepo.UpdateAsync(entity, ct).ConfigureAwait(false);

        WorkflowItemType workflowType = entity.ContentType == NoticeContentType.Form
            ? WorkflowItemType.FormFill
            : WorkflowItemType.NoticeResponse;

        Guid publisherId = entity.CreatedByUserId;
        int fanOutCount = 0;

        foreach (NoticeRecipient recipient in recipients.DistinctBy(r => r.UserId))
        {
            if (recipient.UserId == Guid.Empty || recipient.UserId == publisherId)
            {
                continue;
            }

            var item = new WorkflowItemEntity
            {
                AssigneeUserId = recipient.UserId,
                ItemType = workflowType,
                Status = WorkflowItemStatus.Pending,
                ReferenceType = WorkflowReferenceType.Notice,
                ReferenceId = id,
                Title = BuildWorkflowTitle(entity),
                Summary = recipient.Summary,
                DueDate = entity.ResponseDeadline,
                Priority = entity.ContentType == NoticeContentType.FeeReminder ? 2 : 0,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    noticeId = id,
                    entity.Title,
                    entity.ContentType,
                    entity.RequiresResponse
                })
            };

            await _workflowRepo.CreateItemAsync(item, ct).ConfigureAwait(false);
            fanOutCount++;
        }

        if (fanOutCount == 0)
        {
            _logger.LogWarning("Notice {NoticeId} published but no My Actions tasks were created.", id);
        }

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<NoticeAudiencePreviewDto>> GetAudiencePreviewAsync(
        NoticeTargetType targetType,
        Guid? targetRefId,
        IList<Guid>? targetRefIds = null,
        CancellationToken ct = default)
    {
        IList<NoticeAudienceOptionDto> options = [];
        int count = 0;

        switch (targetType)
        {
            case NoticeTargetType.AllStaff:
            case NoticeTargetType.SingleTeacher:
                IList<NoticeAudienceRow> teachers = await _noticeRepo.GetTeacherAudienceAsync(ct).ConfigureAwait(false);
                options = teachers.Select(MapAudience).ToList();
                count = targetType == NoticeTargetType.SingleTeacher && targetRefId.HasValue
                    ? options.Any(o => o.Id == targetRefId.Value) ? 1 : 0
                    : options.Count;
                break;

            case NoticeTargetType.ClassParents:
            {
                IList<Guid> classIds = targetRefIds?
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList() ?? [];
                if (classIds.Count == 0 && targetRefId.HasValue)
                {
                    classIds = [targetRefId.Value];
                }

                if (classIds.Count == 0)
                {
                    count = 0;
                    break;
                }

                var parentMap = new Dictionary<Guid, NoticeAudienceOptionDto>();
                foreach (Guid classId in classIds)
                {
                    IList<NoticeAudienceRow> classParents = await _noticeRepo
                        .GetParentAudienceAsync(classId, ct).ConfigureAwait(false);
                    foreach (NoticeAudienceRow row in classParents)
                    {
                        parentMap.TryAdd(row.Id, MapAudience(row));
                    }
                }

                options = parentMap.Values.ToList();
                count = options.Count;
                break;
            }

            case NoticeTargetType.SingleParent:
                IList<NoticeAudienceRow> parents = await _noticeRepo.GetParentAudienceAsync(null, ct).ConfigureAwait(false);
                options = parents.Select(MapAudience).ToList();
                count = targetRefId.HasValue && options.Any(o => o.Id == targetRefId.Value) ? 1 : 0;
                break;

            case NoticeTargetType.SingleUser:
                if (Guid.TryParse(_tenantProvider.GetCurrentSchoolId(), out Guid schoolId))
                {
                    IList<NoticeAudienceRow> users = await _noticeRepo.GetSchoolUserAudienceAsync(schoolId, ct).ConfigureAwait(false);
                    options = users.Select(MapAudience).ToList();
                }

                count = targetRefId.HasValue && options.Any(o => o.Id == targetRefId.Value) ? 1 : 0;
                break;

            case NoticeTargetType.PendingFeeParents:
                Guid? yearId = await _academicYearRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
                if (yearId.HasValue)
                {
                    IList<NoticeFeeParentRow> feeParents = await _noticeRepo
                        .GetPendingFeeParentTargetsAsync(yearId.Value, ct).ConfigureAwait(false);
                    options = feeParents.Select(r => new NoticeAudienceOptionDto
                    {
                        Id = r.ParentUserId,
                        Name = r.ParentName,
                        Subtitle = $"Pending ₹{r.PendingAmount:0.##} — {r.StudentSummary}"
                    }).ToList();
                    count = options.Count;
                }

                break;
        }

        return Result<NoticeAudiencePreviewDto>.Success(new NoticeAudiencePreviewDto
        {
            EstimatedRecipients = count,
            Options = options
        });
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

    private async Task<IList<NoticeRecipient>> ResolveRecipientsAsync(NoticeEntity entity, CancellationToken ct)
    {
        string baseSummary = entity.Body.Length > 200 ? entity.Body[..200] + "…" : entity.Body;

        return entity.TargetType switch
        {
            NoticeTargetType.AllStaff => (await _leaveRepo.GetActiveTeacherUserIdsAsync(ct).ConfigureAwait(false))
                .Select(id => new NoticeRecipient(id, baseSummary)).ToList(),

            NoticeTargetType.SingleTeacher =>
                ResolveTargetRecipientIds(entity).Select(id => new NoticeRecipient(id, baseSummary)).ToList(),

            NoticeTargetType.ClassParents =>
                await ResolveClassParentsRecipientsAsync(entity, baseSummary, ct).ConfigureAwait(false),

            NoticeTargetType.SingleParent =>
                ResolveTargetRecipientIds(entity).Select(id => new NoticeRecipient(id, baseSummary)).ToList(),

            NoticeTargetType.SingleUser =>
                ResolveTargetRecipientIds(entity).Select(id => new NoticeRecipient(id, baseSummary)).ToList(),

            NoticeTargetType.PendingFeeParents => await ResolvePendingFeeRecipientsAsync(entity, ct).ConfigureAwait(false),

            _ => []
        };
    }

    private async Task<IList<NoticeRecipient>> ResolveClassParentsRecipientsAsync(
        NoticeEntity entity,
        string baseSummary,
        CancellationToken ct)
    {
        IList<Guid> classIds = ResolveTargetRecipientIds(entity);
        if (classIds.Count == 0)
        {
            return [];
        }

        var parentIds = new HashSet<Guid>();
        foreach (Guid classId in classIds)
        {
            IList<Guid> ids = await _leaveRepo.GetParentUserIdsForClassAsync(classId, ct).ConfigureAwait(false);
            foreach (Guid id in ids)
            {
                parentIds.Add(id);
            }
        }

        return parentIds.Select(id => new NoticeRecipient(id, baseSummary)).ToList();
    }

    private async Task<IList<NoticeRecipient>> ResolvePendingFeeRecipientsAsync(NoticeEntity entity, CancellationToken ct)
    {
        Guid? yearId = await _academicYearRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
        if (!yearId.HasValue)
        {
            return [];
        }

        IList<NoticeFeeParentRow> rows = await _noticeRepo.GetPendingFeeParentTargetsAsync(yearId.Value, ct).ConfigureAwait(false);
        NoticeContentPayloadDto content = NoticeContentSerializer.Deserialize(entity.ContentJson);
        string template = content.FeeMessageTemplate ?? entity.Body;

        return rows.Select(row =>
        {
            string summary = template
                .Replace("{{parentName}}", row.ParentName, StringComparison.OrdinalIgnoreCase)
                .Replace("{{pendingAmount}}", row.PendingAmount.ToString("0.##"), StringComparison.OrdinalIgnoreCase)
                .Replace("{{students}}", row.StudentSummary, StringComparison.OrdinalIgnoreCase);
            if (summary.Length > 240)
            {
                summary = summary[..240] + "…";
            }

            return new NoticeRecipient(row.ParentUserId, summary);
        }).ToList();
    }

    private static void ApplyContentRules(NoticeContentType contentType, ref bool requiresResponse)
    {
        if (contentType == NoticeContentType.Form)
        {
            requiresResponse = true;
        }
        else if (contentType == NoticeContentType.FeeReminder)
        {
            requiresResponse = false;
        }
    }

    private static Result ValidateRequest(
        string title,
        string body,
        NoticeContentType contentType,
        NoticeContentPayloadDto? content,
        NoticeTargetType targetType,
        Guid? targetRefId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(body) && contentType != NoticeContentType.Form)
        {
            return Result.Failure("Body is required.");
        }

        if (contentType == NoticeContentType.Form && (content?.Questions.Count ?? 0) == 0)
        {
            return Result.Failure("Add at least one form question.");
        }

        if (targetType == NoticeTargetType.ClassParents)
        {
            IList<Guid> classIds = content?.TargetRecipientIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? [];
            if (!targetRefId.HasValue && classIds.Count == 0)
            {
                return Result.Failure("Select at least one class for parent audience.");
            }
        }

        if (targetType is NoticeTargetType.SingleParent or NoticeTargetType.SingleTeacher or NoticeTargetType.SingleUser)
        {
            IList<Guid> recipientIds = content?.TargetRecipientIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? [];
            if (!targetRefId.HasValue && recipientIds.Count == 0)
            {
                return Result.Failure("Select at least one recipient.");
            }
        }

        return Result.Success();
    }

    private static Guid? ResolveStoredTargetRefId(
        NoticeTargetType targetType,
        Guid? targetRefId,
        NoticeContentPayloadDto? content)
    {
        if (targetType is not (
            NoticeTargetType.SingleParent
            or NoticeTargetType.SingleTeacher
            or NoticeTargetType.SingleUser
            or NoticeTargetType.ClassParents))
        {
            return targetRefId;
        }

        IList<Guid> ids = content?.TargetRecipientIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];
        if (ids.Count > 0)
        {
            return ids[0];
        }

        return targetRefId;
    }

    private static IList<Guid> ResolveTargetRecipientIds(NoticeEntity entity)
    {
        NoticeContentPayloadDto content = NoticeContentSerializer.Deserialize(entity.ContentJson);
        IList<Guid> ids = content.TargetRecipientIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];
        if (ids.Count > 0)
        {
            return ids;
        }

        return entity.TargetRefId.HasValue ? [entity.TargetRefId.Value] : [];
    }

    private static string BuildWorkflowTitle(NoticeEntity entity) => entity.ContentType switch
    {
        NoticeContentType.Form => $"Form: {entity.Title}",
        NoticeContentType.FeeReminder => $"Fee notice: {entity.Title}",
        NoticeContentType.Document => $"Document: {entity.Title}",
        _ => $"Notice: {entity.Title}"
    };

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        return _currentUser.UserId;
    }

    private static NoticeAudienceOptionDto MapAudience(NoticeAudienceRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Subtitle = row.Subtitle
    };

    private static NoticeListItemDto MapList(NoticeListRow r) => new(
        r.Id,
        r.Title,
        (NoticeStatus)r.Status,
        ((NoticeStatus)r.Status).ToString(),
        (NoticeTargetType)r.TargetType,
        ((NoticeTargetType)r.TargetType).ToString(),
        (NoticeContentType)r.ContentType,
        ((NoticeContentType)r.ContentType).ToString(),
        r.RequiresResponse,
        r.ResponseDeadline,
        r.PublishedOn,
        r.ResponseCount,
        r.IsActive);

    private static NoticeDetailDto MapDetail(NoticeEntity n) => new(
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

    private sealed record NoticeRecipient(Guid UserId, string Summary);
}
