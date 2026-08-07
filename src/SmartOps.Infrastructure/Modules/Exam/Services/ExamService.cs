using Microsoft.Extensions.Logging;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicCalendar.Entities;
using SmartOps.Domain.Modules.Exam;

using SmartOps.Application.Abstractions;

namespace SmartOps.Infrastructure.Modules.Exam.Services;

public sealed class ExamService : IExamService
{
    private readonly IExamRepository _repo;
    private readonly IAcademicCalendarRepository _calendarRepo;
    private readonly IUserScopeContext _scope;
    private readonly ILogger<ExamService> _logger;

    public ExamService(
        IExamRepository repo,
        IAcademicCalendarRepository calendarRepo,
        IUserScopeContext scope,
        ILogger<ExamService> logger)
    {
        _repo = repo;
        _calendarRepo = calendarRepo;
        _scope = scope;
        _logger = logger;
    }

    // ── Grade scales ─────────────────────────────────────────

    public async Task<Result<IList<ExamGradeScaleDto>>> GetGradeScalesAsync(CancellationToken ct = default)
    {
        IList<ExamGradeScaleEntity> scales = await _repo.GetGradeScalesAsync(ct).ConfigureAwait(false);
        IList<ExamGradeScaleDetailEntity> details = await _repo
            .GetGradeScaleDetailsAsync(scales.Select(s => s.Id).ToList(), ct)
            .ConfigureAwait(false);

        ILookup<Guid, ExamGradeScaleDetailEntity> byScale = details.ToLookup(d => d.GradeScaleId);
        IList<ExamGradeScaleDto> result = scales
            .Select(s => MapGradeScale(s, byScale[s.Id]))
            .ToList();

        return Result<IList<ExamGradeScaleDto>>.Success(result);
    }

    public async Task<Result<ExamGradeScaleDto>> GetGradeScaleAsync(Guid id, CancellationToken ct = default)
    {
        ExamGradeScaleEntity? scale = await _repo.GetGradeScaleByIdAsync(id, ct).ConfigureAwait(false);
        if (scale is null)
        {
            return Result<ExamGradeScaleDto>.Failure("Grade scale not found.");
        }

        IList<ExamGradeScaleDetailEntity> details = await _repo
            .GetGradeScaleDetailsAsync([id], ct)
            .ConfigureAwait(false);

        return Result<ExamGradeScaleDto>.Success(MapGradeScale(scale, details));
    }

    public async Task<Result<ExamGradeScaleDto>> CreateGradeScaleAsync(
        SaveExamGradeScaleRequestDto request,
        CancellationToken ct = default)
    {
        string? validationError = ValidateGradeScale(request);
        if (validationError is not null)
        {
            return Result<ExamGradeScaleDto>.Failure(validationError);
        }

        var scale = new ExamGradeScaleEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsDefault = request.IsDefault
        };

        Guid id = await _repo
            .CreateGradeScaleAsync(scale, MapGradeDetails(request.Grades), ct)
            .ConfigureAwait(false);

        _logger.LogInformation("Exam grade scale {ScaleId} created", id);
        return await GetGradeScaleAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<ExamGradeScaleDto>> UpdateGradeScaleAsync(
        Guid id,
        SaveExamGradeScaleRequestDto request,
        CancellationToken ct = default)
    {
        ExamGradeScaleEntity? scale = await _repo.GetGradeScaleByIdAsync(id, ct).ConfigureAwait(false);
        if (scale is null)
        {
            return Result<ExamGradeScaleDto>.Failure("Grade scale not found.");
        }

        string? validationError = ValidateGradeScale(request);
        if (validationError is not null)
        {
            return Result<ExamGradeScaleDto>.Failure(validationError);
        }

        scale.Name = request.Name.Trim();
        scale.Description = request.Description?.Trim();
        scale.IsDefault = request.IsDefault;

        await _repo.UpdateGradeScaleAsync(scale, MapGradeDetails(request.Grades), ct).ConfigureAwait(false);
        return await GetGradeScaleAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteGradeScaleAsync(Guid id, CancellationToken ct = default)
    {
        ExamGradeScaleEntity? scale = await _repo.GetGradeScaleByIdAsync(id, ct).ConfigureAwait(false);
        if (scale is null)
        {
            return Result<bool>.Failure("Grade scale not found.");
        }

        if (await _repo.GradeScaleInUseAsync(id, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("Grade scale is used by an exam or exam group and cannot be deleted.");
        }

        await _repo.SoftDeleteGradeScaleAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    private static string? ValidateGradeScale(SaveExamGradeScaleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Scale name is required.";
        }

        if (request.Grades.Count == 0)
        {
            return "At least one grade row is required.";
        }

        foreach (ExamGradeScaleDetailDto grade in request.Grades)
        {
            if (string.IsNullOrWhiteSpace(grade.Grade))
            {
                return "Every grade row needs a grade label.";
            }

            if (grade.MinPercent < 0 || grade.MaxPercent > 100 || grade.MinPercent > grade.MaxPercent)
            {
                return $"Grade '{grade.Grade}' has an invalid percent range.";
            }
        }

        // Overlap check between ranges.
        var ordered = request.Grades.OrderBy(g => g.MinPercent).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].MinPercent < ordered[i - 1].MaxPercent)
            {
                return $"Grades '{ordered[i - 1].Grade}' and '{ordered[i].Grade}' have overlapping ranges.";
            }
        }

        return null;
    }

    private static IList<ExamGradeScaleDetailEntity> MapGradeDetails(IList<ExamGradeScaleDetailDto> grades) =>
        grades.Select((g, i) => new ExamGradeScaleDetailEntity
        {
            Grade = g.Grade.Trim(),
            MinPercent = g.MinPercent,
            MaxPercent = g.MaxPercent,
            GradePoint = g.GradePoint,
            Description = g.Description?.Trim(),
            DisplayOrder = g.DisplayOrder != 0 ? g.DisplayOrder : i
        }).ToList();

    private static ExamGradeScaleDto MapGradeScale(
        ExamGradeScaleEntity scale,
        IEnumerable<ExamGradeScaleDetailEntity> details) =>
        new(
            scale.Id,
            scale.Name,
            scale.Description,
            scale.IsDefault,
            details
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new ExamGradeScaleDetailDto(
                    d.Id, d.Grade, d.MinPercent, d.MaxPercent, d.GradePoint, d.Description, d.DisplayOrder))
                .ToList());

    // ── Exam groups ──────────────────────────────────────────

    public async Task<Result<IList<ExamGroupDto>>> GetGroupsAsync(CancellationToken ct = default)
    {
        IList<ExamGroupRow> rows = await _repo.GetGroupsAsync(ct).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> classGroupIdsByGroup = await _repo
            .GetClassGroupIdsByExamGroupAsync(rows.Select(r => r.Id).ToList(), ct)
            .ConfigureAwait(false);

        IList<ExamGroupDto> result = rows
            .Select(row => MapGroup(
                row,
                classGroupIdsByGroup.TryGetValue(row.Id, out IReadOnlyList<Guid>? ids) ? ids : []))
            .ToList();
        return Result<IList<ExamGroupDto>>.Success(result);
    }

    public async Task<Result<ExamGroupDto>> CreateGroupAsync(SaveExamGroupRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ExamGroupDto>.Failure("Group name is required.");
        }

        IReadOnlyList<Guid> classGroupIds = NormalizeClassGroupIds(request.ClassGroupIds);

        var group = new ExamGroupEntity
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            GradeScaleId = request.GradeScaleId,
            EvaluationType = request.EvaluationType
        };

        Guid id = await _repo.CreateGroupAsync(group, ct).ConfigureAwait(false);
        ExamGroupEntity? created = await _repo.GetGroupByIdAsync(id, ct).ConfigureAwait(false);
        if (created is null)
        {
            return Result<ExamGroupDto>.Failure("Exam group was created but could not be loaded.");
        }

        await _repo
            .SaveExamGroupClassGroupIdsAsync(id, created.BranchId, classGroupIds, allowRemove: true, ct)
            .ConfigureAwait(false);

        _logger.LogInformation("Exam group {GroupId} created", id);
        return await FindGroupDtoAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<ExamGroupDto>> UpdateGroupAsync(
        Guid id,
        SaveExamGroupRequestDto request,
        CancellationToken ct = default)
    {
        ExamGroupEntity? group = await _repo.GetGroupByIdAsync(id, ct).ConfigureAwait(false);
        if (group is null)
        {
            return Result<ExamGroupDto>.Failure("Exam group not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ExamGroupDto>.Failure("Group name is required.");
        }

        IReadOnlyList<Guid> classGroupIds = NormalizeClassGroupIds(request.ClassGroupIds);

        group.Name = request.Name.Trim();
        group.Description = request.Description?.Trim();
        group.GradeScaleId = request.GradeScaleId;
        group.EvaluationType = request.EvaluationType;

        await _repo.UpdateGroupAsync(group, ct).ConfigureAwait(false);
        await _repo
            .SaveExamGroupClassGroupIdsAsync(id, group.BranchId, classGroupIds, allowRemove: true, ct)
            .ConfigureAwait(false);
        return await FindGroupDtoAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        ExamGroupEntity? group = await _repo.GetGroupByIdAsync(id, ct).ConfigureAwait(false);
        if (group is null)
        {
            return Result<bool>.Failure("Exam group not found.");
        }

        if (await _repo.GroupHasExamsAsync(id, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("Group contains exams. Delete the exams first.");
        }

        await _repo.SoftDeleteGroupAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    private async Task<Result<ExamGroupDto>> FindGroupDtoAsync(Guid id, CancellationToken ct)
    {
        IList<ExamGroupRow> rows = await _repo.GetGroupsAsync(ct).ConfigureAwait(false);
        ExamGroupRow? row = rows.FirstOrDefault(r => r.Id == id);
        if (row is null)
        {
            return Result<ExamGroupDto>.Failure("Exam group not found.");
        }

        IReadOnlyList<Guid> classGroupIds = await _repo
            .GetClassGroupIdsForExamGroupAsync(id, ct)
            .ConfigureAwait(false);
        return Result<ExamGroupDto>.Success(MapGroup(row, classGroupIds));
    }

    private static ExamGroupDto MapGroup(ExamGroupRow row, IReadOnlyList<Guid> classGroupIds) =>
        new(
            row.Id,
            row.Name,
            row.Description,
            row.GradeScaleId,
            row.GradeScaleName,
            (ExamEvaluationType)row.EvaluationType,
            ((ExamEvaluationType)row.EvaluationType).ToDisplayString(),
            row.ExamCount,
            classGroupIds,
            string.IsNullOrWhiteSpace(row.ClassGroupNames) ? null : row.ClassGroupNames);

    private static IReadOnlyList<Guid> NormalizeClassGroupIds(IReadOnlyList<Guid>? classGroupIds) =>
        (classGroupIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    // ── Exams ────────────────────────────────────────────────

    public async Task<Result<IList<ExamListItemDto>>> GetExamsAsync(
        Guid? groupId,
        Guid? classId,
        int? status,
        string? search,
        bool inactiveOnly = false,
        CancellationToken ct = default)
    {
        IList<ExamRow> rows = await _repo
            .GetExamsAsync(groupId, classId, status, search, inactiveOnly, ct)
            .ConfigureAwait(false);
        IList<ExamClassRow> classes = await _repo
            .GetExamClassesAsync(rows.Select(r => r.Id).ToList(), includeInactive: inactiveOnly, ct)
            .ConfigureAwait(false);

        ILookup<Guid, ExamClassRow> classesByExam = classes.ToLookup(c => c.ExamId);
        IList<ExamListItemDto> result = rows.Select(row => new ExamListItemDto(
            row.Id,
            row.Name,
            row.ExamType,
            row.ExamGroupId,
            row.ExamGroupName,
            (ExamStatus)row.Status,
            ((ExamStatus)row.Status).ToDisplayString(),
            row.ResultDeclared,
            row.TotalMaxMarks,
            row.SubjectCount,
            classesByExam[row.Id]
                .Select(c => new ExamClassInfoDto(c.ClassId, c.ClassName, c.ClassGroupId, c.ClassGroupName))
                .ToList(),
            row.IsActive)).ToList();

        return Result<IList<ExamListItemDto>>.Success(result);
    }

    public async Task<Result<ExamStatsDto>> GetExamStatsAsync(CancellationToken ct = default)
    {
        IList<ExamRow> rows = await _repo.GetExamsAsync(null, null, null, null, inactiveOnly: false, ct).ConfigureAwait(false);

        int ongoing = rows.Count(r => (ExamStatus)r.Status == ExamStatus.Ongoing);
        int completed = rows.Count(r => (ExamStatus)r.Status is ExamStatus.Completed or ExamStatus.ResultDeclared);
        int upcoming = rows.Count(r => (ExamStatus)r.Status is ExamStatus.Draft or ExamStatus.Scheduled);

        return Result<ExamStatsDto>.Success(new ExamStatsDto(rows.Count, ongoing, completed, upcoming));
    }

    public async Task<Result<ExamDetailDto>> GetExamAsync(Guid id, CancellationToken ct = default)
    {
        ExamEntity? exam = await _repo.GetExamByIdAsync(id, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamDetailDto>.Failure("Exam not found.");
        }

        return Result<ExamDetailDto>.Success(await BuildExamDetailAsync(exam, ct).ConfigureAwait(false));
    }

    public async Task<Result<ExamDetailDto>> CreateExamAsync(SaveExamRequestDto request, CancellationToken ct = default)
    {
        string? validationError = ValidateExam(request);
        if (validationError is not null)
        {
            return Result<ExamDetailDto>.Failure(validationError);
        }

        ExamGroupEntity? group = await _repo.GetGroupByIdAsync(request.ExamGroupId, ct).ConfigureAwait(false);
        if (group is null)
        {
            return Result<ExamDetailDto>.Failure("Exam group not found.");
        }

        string? classScopeError = await ValidateExamClassScopeAsync(request.ExamGroupId, request.ClassIds, ct)
            .ConfigureAwait(false);
        if (classScopeError is not null)
        {
            return Result<ExamDetailDto>.Failure(classScopeError);
        }

        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        if (!_scope.ActiveAcademicYearId.HasValue)
        {
            return Result<ExamDetailDto>.Failure("Active academic year is required.");
        }

        var exam = new ExamEntity
        {
            ExamGroupId = request.ExamGroupId,
            AcademicYearId = _scope.ActiveAcademicYearId.Value,
            BranchId = group.BranchId,
            Name = request.Name.Trim(),
            ExamType = request.ExamType.Trim(),
            AcademicPeriodId = request.AcademicPeriodId,
            MinPassPercent = request.MinPassPercent,
            GradeScaleId = request.GradeScaleId,
            Status = ExamStatus.Scheduled,
            Description = request.Description?.Trim()
        };

        Guid id = await _repo
            .CreateExamAsync(exam, request.ClassIds, MapComponents(request.Components), ct)
            .ConfigureAwait(false);
        exam.Id = id;

        _logger.LogInformation("Exam {ExamId} created in group {GroupId}", id, request.ExamGroupId);
        return Result<ExamDetailDto>.Success(await BuildExamDetailAsync(exam, ct).ConfigureAwait(false));
    }

    public async Task<Result<ExamDetailDto>> UpdateExamAsync(Guid id, SaveExamRequestDto request, CancellationToken ct = default)
    {
        ExamEntity? exam = await _repo.GetExamByIdAsync(id, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamDetailDto>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared)
        {
            return Result<ExamDetailDto>.Failure("Result already declared. This exam is locked.");
        }

        string? validationError = ValidateExam(request);
        if (validationError is not null)
        {
            return Result<ExamDetailDto>.Failure(validationError);
        }

        string? classScopeError = await ValidateExamClassScopeAsync(request.ExamGroupId, request.ClassIds, ct)
            .ConfigureAwait(false);
        if (classScopeError is not null)
        {
            return Result<ExamDetailDto>.Failure(classScopeError);
        }

        exam.ExamGroupId = request.ExamGroupId;
        exam.Name = request.Name.Trim();
        exam.ExamType = request.ExamType.Trim();
        exam.AcademicPeriodId = request.AcademicPeriodId;
        exam.MinPassPercent = request.MinPassPercent;
        exam.GradeScaleId = request.GradeScaleId;
        exam.Description = request.Description?.Trim();

        await _repo.UpdateExamAsync(exam, request.ClassIds, MapComponents(request.Components), ct).ConfigureAwait(false);
        await SyncExamCalendarEventAsync(exam.Id, ct).ConfigureAwait(false);
        return Result<ExamDetailDto>.Success(await BuildExamDetailAsync(exam, ct).ConfigureAwait(false));
    }

    public async Task<Result<bool>> DeleteExamAsync(Guid id, CancellationToken ct = default)
    {
        ExamEntity? exam = await _repo.GetExamByIdAsync(id, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<bool>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared)
        {
            return Result<bool>.Failure("Result already declared. This exam cannot be deleted.");
        }

        await _repo.SoftDeleteExamAsync(id, ct).ConfigureAwait(false);
        await RemoveExamCalendarEventAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> UpdateExamStatusAsync(Guid id, ExamStatus status, CancellationToken ct = default)
    {
        ExamEntity? exam = await _repo.GetExamByIdAsync(id, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<bool>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared && status != ExamStatus.ResultDeclared)
        {
            return Result<bool>.Failure("Result already declared. Status cannot be changed.");
        }

        await _repo.UpdateExamStatusAsync(id, status, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    private async Task<string?> ValidateExamClassScopeAsync(
        Guid examGroupId,
        IList<Guid> classIds,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> ids = (classIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        bool ok = await _repo
            .AllClassesBelongToExamGroupAsync(examGroupId, ids, ct)
            .ConfigureAwait(false);
        return ok
            ? null
            : "Selected classes must belong to class groups mapped on this exam group.";
    }

    private static string? ValidateExam(SaveExamRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Exam name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ExamType))
        {
            return "Exam type is required.";
        }

        if (request.Components.Count == 0)
        {
            return "Add at least one mark component (e.g. Theory).";
        }

        foreach (ExamMarkComponentDto component in request.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Name))
            {
                return "Every mark component needs a name.";
            }

            if (component.MaxMarks <= 0)
            {
                return $"Component '{component.Name}' needs max marks greater than 0.";
            }

            if (component.PassingMarks.HasValue && component.PassingMarks.Value > component.MaxMarks)
            {
                return $"Component '{component.Name}' passing marks cannot exceed max marks.";
            }
        }

        if (request.MinPassPercent is < 0 or > 100)
        {
            return "Minimum pass percent must be between 0 and 100.";
        }

        return null;
    }

    private static IList<ExamMarkComponentEntity> MapComponents(IList<ExamMarkComponentDto> components) =>
        components.Select((c, i) => new ExamMarkComponentEntity
        {
            Id = c.Id ?? Guid.Empty,
            Name = c.Name.Trim(),
            MaxMarks = c.MaxMarks,
            PassingMarks = c.PassingMarks,
            DisplayOrder = c.DisplayOrder != 0 ? c.DisplayOrder : i
        }).ToList();

    private async Task<ExamDetailDto> BuildExamDetailAsync(ExamEntity exam, CancellationToken ct)
    {
        ExamGroupEntity? group = await _repo.GetGroupByIdAsync(exam.ExamGroupId, ct).ConfigureAwait(false);
        IList<ExamClassRow> classes = await _repo.GetExamClassesAsync([exam.Id], ct: ct).ConfigureAwait(false);
        IList<ExamMarkComponentEntity> components = await _repo.GetComponentsAsync([exam.Id], ct).ConfigureAwait(false);

        return new ExamDetailDto(
            exam.Id,
            exam.ExamGroupId,
            group?.Name ?? string.Empty,
            exam.Name,
            exam.ExamType,
            exam.AcademicPeriodId,
            exam.MinPassPercent,
            exam.GradeScaleId,
            exam.Status,
            exam.Status.ToDisplayString(),
            exam.ResultDeclared,
            exam.Description,
            classes.Select(c => c.ClassId).ToList(),
            classes.Select(c => new ExamClassInfoDto(c.ClassId, c.ClassName, c.ClassGroupId, c.ClassGroupName)).ToList(),
            components
                .Select(c => new ExamMarkComponentDto(c.Id, c.Name, c.MaxMarks, c.PassingMarks, c.DisplayOrder))
                .ToList());
    }

    // ── Schedules ────────────────────────────────────────────

    public async Task<Result<IList<ExamScheduleItemDto>>> GetSchedulesAsync(
        Guid? examId,
        Guid? classId,
        CancellationToken ct = default)
    {
        IList<ExamScheduleRow> rows = await _repo.GetSchedulesAsync(examId, classId, ct).ConfigureAwait(false);
        IList<ExamScheduleItemDto> result = rows.Select(MapSchedule).ToList();
        return Result<IList<ExamScheduleItemDto>>.Success(result);
    }

    public async Task<Result<ExamScheduleItemDto>> CreateScheduleAsync(
        SaveExamScheduleRequestDto request,
        CancellationToken ct = default)
    {
        string? validationError = ValidateSchedule(request);
        if (validationError is not null)
        {
            return Result<ExamScheduleItemDto>.Failure(validationError);
        }

        ExamEntity? exam = await _repo.GetExamByIdAsync(request.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamScheduleItemDto>.Failure("Exam not found.");
        }

        if (await _repo.ScheduleExistsAsync(request.ExamId, request.ClassId, request.SubjectId, null, ct).ConfigureAwait(false))
        {
            return Result<ExamScheduleItemDto>.Failure("This subject is already scheduled for the selected exam and class.");
        }

        var schedule = new ExamScheduleEntity
        {
            ExamId = request.ExamId,
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            ExamDate = request.ExamDate,
            StartTime = NormalizeTime(request.StartTime),
            EndTime = NormalizeTime(request.EndTime),
            RoomNo = request.RoomNo?.Trim(),
            InvigilatorId = request.InvigilatorId
        };

        Guid id = await _repo.CreateScheduleAsync(schedule, ct).ConfigureAwait(false);
        await SyncExamCalendarEventAsync(request.ExamId, ct).ConfigureAwait(false);
        return await FindScheduleDtoAsync(id, request.ExamId, ct).ConfigureAwait(false);
    }

    public async Task<Result<BulkCreateExamSchedulesResultDto>> BulkCreateSchedulesAsync(
        BulkCreateExamSchedulesRequestDto request,
        CancellationToken ct = default)
    {
        if (request.ExamId == Guid.Empty)
        {
            return Result<BulkCreateExamSchedulesResultDto>.Failure("Exam is required.");
        }

        if (request.Slots is null || request.Slots.Count == 0)
        {
            return Result<BulkCreateExamSchedulesResultDto>.Failure("Add at least one schedule slot.");
        }

        ExamEntity? exam = await _repo.GetExamByIdAsync(request.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<BulkCreateExamSchedulesResultDto>.Failure("Exam not found.");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var entities = new List<ExamScheduleEntity>(request.Slots.Count);

        foreach (BulkExamScheduleSlotDto slot in request.Slots)
        {
            var single = new SaveExamScheduleRequestDto(
                request.ExamId,
                slot.ClassId,
                slot.SubjectId,
                slot.ExamDate,
                slot.StartTime,
                slot.EndTime,
                slot.RoomNo,
                slot.InvigilatorId);

            string? validationError = ValidateSchedule(single);
            if (validationError is not null)
            {
                return Result<BulkCreateExamSchedulesResultDto>.Failure(validationError);
            }

            string key = $"{slot.ClassId:D}:{slot.SubjectId:D}";
            if (!seenKeys.Add(key))
            {
                return Result<BulkCreateExamSchedulesResultDto>.Failure(
                    "Duplicate subject for the same class in this schedule.");
            }

            if (await _repo.ScheduleExistsAsync(request.ExamId, slot.ClassId, slot.SubjectId, null, ct).ConfigureAwait(false))
            {
                return Result<BulkCreateExamSchedulesResultDto>.Failure(
                    "This subject is already scheduled for the selected exam and class.");
            }

            entities.Add(new ExamScheduleEntity
            {
                ExamId = request.ExamId,
                ClassId = slot.ClassId,
                SubjectId = slot.SubjectId,
                ExamDate = slot.ExamDate,
                StartTime = NormalizeTime(slot.StartTime),
                EndTime = NormalizeTime(slot.EndTime),
                RoomNo = slot.RoomNo?.Trim(),
                InvigilatorId = slot.InvigilatorId
            });
        }

        await _repo.CreateSchedulesAsync(entities, ct).ConfigureAwait(false);
        await SyncExamCalendarEventAsync(request.ExamId, ct).ConfigureAwait(false);

        IList<ExamScheduleRow> rows = await _repo.GetSchedulesAsync(request.ExamId, null, ct).ConfigureAwait(false);
        HashSet<Guid> createdIds = entities.Select(e => e.Id).ToHashSet();
        IList<ExamScheduleItemDto> created = rows
            .Where(r => createdIds.Contains(r.Id))
            .Select(MapSchedule)
            .ToList();

        return Result<BulkCreateExamSchedulesResultDto>.Success(
            new BulkCreateExamSchedulesResultDto(created.Count, created));
    }

    public async Task<Result<ExamScheduleItemDto>> UpdateScheduleAsync(
        Guid id,
        SaveExamScheduleRequestDto request,
        CancellationToken ct = default)
    {
        ExamScheduleEntity? schedule = await _repo.GetScheduleByIdAsync(id, ct).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result<ExamScheduleItemDto>.Failure("Schedule slot not found.");
        }

        string? validationError = ValidateSchedule(request);
        if (validationError is not null)
        {
            return Result<ExamScheduleItemDto>.Failure(validationError);
        }

        if (await _repo.ScheduleExistsAsync(request.ExamId, request.ClassId, request.SubjectId, id, ct).ConfigureAwait(false))
        {
            return Result<ExamScheduleItemDto>.Failure("This subject is already scheduled for the selected exam and class.");
        }

        schedule.ClassId = request.ClassId;
        schedule.SubjectId = request.SubjectId;
        schedule.ExamDate = request.ExamDate;
        schedule.StartTime = NormalizeTime(request.StartTime);
        schedule.EndTime = NormalizeTime(request.EndTime);
        schedule.RoomNo = request.RoomNo?.Trim();
        schedule.InvigilatorId = request.InvigilatorId;

        await _repo.UpdateScheduleAsync(schedule, ct).ConfigureAwait(false);
        await SyncExamCalendarEventAsync(schedule.ExamId, ct).ConfigureAwait(false);
        return await FindScheduleDtoAsync(id, schedule.ExamId, ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteScheduleAsync(Guid id, CancellationToken ct = default)
    {
        ExamScheduleEntity? schedule = await _repo.GetScheduleByIdAsync(id, ct).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result<bool>.Failure("Schedule slot not found.");
        }

        Guid examId = schedule.ExamId;
        await _repo.SoftDeleteScheduleAsync(id, ct).ConfigureAwait(false);
        await SyncExamCalendarEventAsync(examId, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    private static string? ValidateSchedule(SaveExamScheduleRequestDto request)
    {
        if (request.ExamId == Guid.Empty)
        {
            return "Exam is required.";
        }

        if (request.ClassId == Guid.Empty)
        {
            return "Class is required.";
        }

        if (request.SubjectId == Guid.Empty)
        {
            return "Subject is required.";
        }

        return null;
    }

    private static string? NormalizeTime(string? time) =>
        string.IsNullOrWhiteSpace(time) ? null : time.Trim();

    private async Task<Result<ExamScheduleItemDto>> FindScheduleDtoAsync(Guid id, Guid examId, CancellationToken ct)
    {
        IList<ExamScheduleRow> rows = await _repo.GetSchedulesAsync(examId, null, ct).ConfigureAwait(false);
        ExamScheduleRow? row = rows.FirstOrDefault(r => r.Id == id);
        return row is null
            ? Result<ExamScheduleItemDto>.Failure("Schedule slot not found.")
            : Result<ExamScheduleItemDto>.Success(MapSchedule(row));
    }

    private static ExamScheduleItemDto MapSchedule(ExamScheduleRow row)
    {
        DateOnly today = SchoolLocalTime.Today(null);
        string status = row.ExamDate < today ? "Completed"
            : row.ExamDate == today ? "Today"
            : "Upcoming";

        return new ExamScheduleItemDto(
            row.Id,
            row.ExamId,
            row.ExamName,
            row.ClassId,
            row.ClassName,
            row.SubjectId,
            row.SubjectName,
            row.ExamDate,
            row.StartTime,
            row.EndTime,
            row.RoomNo,
            row.InvigilatorId,
            row.InvigilatorName,
            row.MaxMarks,
            status);
    }

    /// <summary>
    /// Upserts one calendar event spanning all active schedule dates for the exam,
    /// or removes it when no slots remain.
    /// </summary>
    private async Task SyncExamCalendarEventAsync(Guid examId, CancellationToken ct)
    {
        try
        {
            ExamEntity? exam = await _repo.GetExamByIdAsync(examId, ct).ConfigureAwait(false);
            if (exam is null)
            {
                await RemoveExamCalendarEventAsync(examId, ct).ConfigureAwait(false);
                return;
            }

            IList<ExamScheduleRow> slots = await _repo.GetSchedulesAsync(examId, null, ct).ConfigureAwait(false);
            if (slots.Count == 0)
            {
                await RemoveExamCalendarEventAsync(examId, ct).ConfigureAwait(false);
                return;
            }

            var examType = (await _calendarRepo.GetEventTypesAsync(ct).ConfigureAwait(false))
                .FirstOrDefault(t => string.Equals(t.Code, "EXAM", StringComparison.OrdinalIgnoreCase));
            if (examType is null)
            {
                _logger.LogWarning("Calendar event type EXAM not found; skipped sync for exam {ExamId}", examId);
                return;
            }

            DateOnly start = slots.Min(s => s.ExamDate);
            DateOnly end = slots.Max(s => s.ExamDate);
            List<Guid> classIds = slots.Select(s => s.ClassId).Distinct().ToList();
            string classLabel = string.Join(", ",
                slots.Select(s => s.ClassName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            CalendarEventEntity? existing = await _calendarRepo
                .GetEventBySourceExamIdAsync(examId, ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var created = new CalendarEventEntity
                {
                    BranchId = exam.BranchId,
                    AcademicYearId = exam.AcademicYearId,
                    EventTypeId = examType.Id,
                    Title = exam.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(classLabel) ? null : $"Classes: {classLabel}",
                    StartDate = start,
                    EndDate = end,
                    AppliesToStudents = true,
                    AppliesToTeachers = true,
                    AppliesToStaff = false,
                    IsNonWorkingDay = false,
                    Color = examType.Color,
                    SourceExamId = examId
                };
                await _calendarRepo.CreateEventAsync(created, classIds, ct).ConfigureAwait(false);
                return;
            }

            existing.Title = exam.Name.Trim();
            existing.Description = string.IsNullOrWhiteSpace(classLabel) ? null : $"Classes: {classLabel}";
            existing.StartDate = start;
            existing.EndDate = end;
            existing.EventTypeId = examType.Id;
            existing.AppliesToStudents = true;
            existing.AppliesToTeachers = true;
            existing.AppliesToStaff = false;
            existing.IsNonWorkingDay = false;
            existing.Color = examType.Color;
            existing.SourceExamId = examId;
            existing.AcademicYearId = exam.AcademicYearId;
            await _calendarRepo.UpdateEventAsync(existing, classIds, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync academic calendar event for exam {ExamId}", examId);
        }
    }

    private async Task RemoveExamCalendarEventAsync(Guid examId, CancellationToken ct)
    {
        try
        {
            CalendarEventEntity? existing = await _calendarRepo
                .GetEventBySourceExamIdAsync(examId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return;
            }

            await _calendarRepo.DeleteEventAsync(existing.Id, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove academic calendar event for exam {ExamId}", examId);
        }
    }
}
