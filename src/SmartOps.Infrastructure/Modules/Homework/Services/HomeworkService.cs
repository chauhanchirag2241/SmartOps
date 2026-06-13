using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Homework;
using SmartOps.Application.Modules.Homework.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.Homework;
using SmartOps.Domain.Modules.Student;
using SmartOps.Infrastructure.Modules.Homework;

namespace SmartOps.Infrastructure.Modules.Homework.Services;

public sealed class HomeworkService : IHomeworkService
{
    private readonly IHomeworkRepository _homeworkRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HomeworkService> _logger;

    public HomeworkService(
        IHomeworkRepository homeworkRepo,
        IStudentRepository studentRepo,
        ICurrentUserService currentUser,
        ILogger<HomeworkService> logger)
    {
        _homeworkRepo = homeworkRepo;
        _studentRepo = studentRepo;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<IList<HomeworkListItemDto>>> GetListAsync(
        Guid? classId,
        Guid? subjectId,
        string? statusFilter,
        string? searchTerm,
        CancellationToken ct = default)
    {
        IList<HomeworkListRow> rows =
            await _homeworkRepo.GetListAsync(classId, subjectId, statusFilter, searchTerm, ct)
                .ConfigureAwait(false);

        IList<HomeworkListItemDto> items = rows
            .Select(MapListItem)
            .ToList();

        return Result<IList<HomeworkListItemDto>>.Success(items);
    }

    public async Task<Result<HomeworkStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        HomeworkStatsRow row = await _homeworkRepo.GetStatsAsync(ct).ConfigureAwait(false);
        return Result<HomeworkStatsDto>.Success(new HomeworkStatsDto(
            row.TotalAssigned,
            row.DueToday,
            row.TotalSubmissions,
            row.Overdue));
    }

    public async Task<Result<HomeworkDetailResponseDto>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        HomeworkEntity? homework = await _homeworkRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (homework is null)
        {
            return Result<HomeworkDetailResponseDto>.Failure("Homework not found.");
        }

        bool isSubmitted = await _homeworkRepo.HasSubmissionsAsync(id, ct).ConfigureAwait(false);
        IList<HomeworkDetailEntity> details =
            await _homeworkRepo.GetDetailsByHomeworkIdAsync(id, ct).ConfigureAwait(false);

        HomeworkDetailResponseDto response =
            await BuildDetailResponseAsync(homework, details, isSubmitted, ct).ConfigureAwait(false);

        return Result<HomeworkDetailResponseDto>.Success(response);
    }

    public async Task<Result<HomeworkDetailResponseDto>> CreateAsync(
        CreateHomeworkRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result<HomeworkDetailResponseDto>.Failure("Title is required.");
        }

        if (request.DueDate < request.AssignDate)
        {
            return Result<HomeworkDetailResponseDto>.Failure("Due date cannot be before assign date.");
        }

        Guid employeeid = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);

        var entity = new HomeworkEntity
        {
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            EmployeeId = employeeid,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            AssignDate = request.AssignDate,
            DueDate = request.DueDate,
            Priority = request.Priority,
            Marks = request.Marks,
            SubmissionType = request.SubmissionType
        };

        Guid id = await _homeworkRepo.CreateAsync(entity, ct).ConfigureAwait(false);
        entity.Id = id;

        _logger.LogInformation("Homework {HomeworkId} created for class {ClassId}", id, request.ClassId);

        HomeworkDetailResponseDto response =
            await BuildDetailResponseAsync(entity, [], false, ct).ConfigureAwait(false);

        return Result<HomeworkDetailResponseDto>.Success(response);
    }

    public async Task<Result<HomeworkDetailResponseDto>> UpdateAsync(
        Guid id,
        UpdateHomeworkRequestDto request,
        CancellationToken ct = default)
    {
        HomeworkEntity? homework = await _homeworkRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (homework is null)
        {
            return Result<HomeworkDetailResponseDto>.Failure("Homework not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result<HomeworkDetailResponseDto>.Failure("Title is required.");
        }

        homework.ClassId = request.ClassId;
        homework.SubjectId = request.SubjectId;
        homework.Title = request.Title.Trim();
        homework.Description = request.Description?.Trim();
        homework.AssignDate = request.AssignDate;
        homework.DueDate = request.DueDate;
        homework.Priority = request.Priority;
        homework.Marks = request.Marks;
        homework.SubmissionType = request.SubmissionType;

        await _homeworkRepo.UpdateAsync(homework, ct).ConfigureAwait(false);

        bool isSubmitted = await _homeworkRepo.HasSubmissionsAsync(id, ct).ConfigureAwait(false);
        IList<HomeworkDetailEntity> details =
            await _homeworkRepo.GetDetailsByHomeworkIdAsync(id, ct).ConfigureAwait(false);

        HomeworkDetailResponseDto response =
            await BuildDetailResponseAsync(homework, details, isSubmitted, ct).ConfigureAwait(false);

        return Result<HomeworkDetailResponseDto>.Success(response);
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        HomeworkEntity? homework = await _homeworkRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (homework is null)
        {
            return Result<bool>.Failure("Homework not found.");
        }

        await _homeworkRepo.SoftDeleteAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<HomeworkDetailResponseDto>> SubmitSubmissionsAsync(
        Guid homeworkId,
        SubmitHomeworkSubmissionsRequestDto request,
        CancellationToken ct = default)
    {
        HomeworkEntity? homework = await _homeworkRepo.GetByIdAsync(homeworkId, ct).ConfigureAwait(false);
        if (homework is null)
        {
            return Result<HomeworkDetailResponseDto>.Failure("Homework not found.");
        }

        if (await _homeworkRepo.HasSubmissionsAsync(homeworkId, ct).ConfigureAwait(false))
        {
            return Result<HomeworkDetailResponseDto>.Failure(
                "Submissions already recorded. Use update to change statuses.");
        }

        IList<HomeworkStudentRow> roster =
            await _homeworkRepo.GetClassStudentsForHomeworkAsync(homework.ClassId, ct).ConfigureAwait(false);

        if (roster.Count == 0)
        {
            var paged = await _studentRepo.GetAllStudentsAsync(
                1, 500, null, null, null, StudentFilter.Active, homework.ClassId, null, ct).ConfigureAwait(false);
            roster = paged.Items
                .Select((s, idx) => new HomeworkStudentRow
                {
                    StudentId = s.Id,
                    StudentName = s.Name,
                    RollNo = (idx + 1).ToString().PadLeft(2, '0')
                })
                .ToList();
        }

        Dictionary<Guid, StudentHomeworkSubmissionItemDto> selected =
            request.Students.ToDictionary(s => s.StudentId);

        IList<HomeworkDetailEntity> details = roster.Select(student =>
        {
            HomeworkSubmissionStatus status = HomeworkSubmissionStatus.Pending;
            DateOnly? submittedOn = null;
            int? marks = null;
            string? remark = null;

            if (selected.TryGetValue(student.StudentId, out StudentHomeworkSubmissionItemDto? item))
            {
                status = item.Status;
                submittedOn = item.SubmittedOn;
                marks = item.Marks;
                remark = item.Remark;
            }

            return new HomeworkDetailEntity
            {
                HomeworkId = homeworkId,
                ClassId = homework.ClassId,
                SubjectId = homework.SubjectId,
                StudentId = student.StudentId,
                Status = status,
                SubmittedOn = submittedOn,
                Marks = marks,
                Remark = remark
            };
        }).ToList();

        await _homeworkRepo.BulkInsertDetailsAsync(details, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Homework submissions submitted for {HomeworkId}. Students: {Count}",
            homeworkId,
            details.Count);

        IList<HomeworkDetailEntity> saved =
            await _homeworkRepo.GetDetailsByHomeworkIdAsync(homeworkId, ct).ConfigureAwait(false);

        HomeworkDetailResponseDto response =
            await BuildDetailResponseAsync(homework, saved, true, ct).ConfigureAwait(false);

        return Result<HomeworkDetailResponseDto>.Success(response);
    }

    public async Task<Result<HomeworkDetailResponseDto>> UpdateSubmissionsAsync(
        Guid homeworkId,
        UpdateHomeworkSubmissionsRequestDto request,
        CancellationToken ct = default)
    {
        HomeworkEntity? homework = await _homeworkRepo.GetByIdAsync(homeworkId, ct).ConfigureAwait(false);
        if (homework is null)
        {
            return Result<HomeworkDetailResponseDto>.Failure("Homework not found.");
        }

        if (!await _homeworkRepo.HasSubmissionsAsync(homeworkId, ct).ConfigureAwait(false))
        {
            return Result<HomeworkDetailResponseDto>.Failure(
                "No submissions recorded yet. Use submit first.");
        }

        IList<HomeworkDetailEntity> details = request.Students.Select(item => new HomeworkDetailEntity
        {
            HomeworkId = homeworkId,
            ClassId = homework.ClassId,
            SubjectId = homework.SubjectId,
            StudentId = item.StudentId,
            Status = item.Status,
            SubmittedOn = item.SubmittedOn,
            Marks = item.Marks,
            Remark = item.Remark
        }).ToList();

        await _homeworkRepo.BulkUpsertDetailsAsync(details, ct).ConfigureAwait(false);

        IList<HomeworkDetailEntity> saved =
            await _homeworkRepo.GetDetailsByHomeworkIdAsync(homeworkId, ct).ConfigureAwait(false);

        HomeworkDetailResponseDto response =
            await BuildDetailResponseAsync(homework, saved, true, ct).ConfigureAwait(false);

        return Result<HomeworkDetailResponseDto>.Success(response);
    }

    private async Task<HomeworkDetailResponseDto> BuildDetailResponseAsync(
        HomeworkEntity homework,
        IList<HomeworkDetailEntity> details,
        bool isSubmitted,
        CancellationToken ct)
    {
        HomeworkMetaRow? meta =
            await _homeworkRepo.GetMetaByHomeworkIdAsync(homework.Id, ct).ConfigureAwait(false);
        string className = meta?.ClassName ?? string.Empty;
        string subjectName = meta?.SubjectName ?? string.Empty;

        IList<HomeworkStudentRow> roster =
            await _homeworkRepo.GetClassStudentsForHomeworkAsync(homework.ClassId, ct).ConfigureAwait(false);

        if (roster.Count == 0)
        {
            var paged = await _studentRepo.GetAllStudentsAsync(
                1, 500, null, null, null, StudentFilter.Active, homework.ClassId, null, ct).ConfigureAwait(false);
            roster = paged.Items
                .Select((s, idx) => new HomeworkStudentRow
                {
                    StudentId = s.Id,
                    StudentName = s.Name,
                    RollNo = (idx + 1).ToString().PadLeft(2, '0')
                })
                .ToList();
        }

        Dictionary<Guid, HomeworkDetailEntity> detailByStudent = details
            .GroupBy(d => d.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        IList<HomeworkStudentSubmissionDto> students = roster.Select(student =>
        {
            if (detailByStudent.TryGetValue(student.StudentId, out HomeworkDetailEntity? detail))
            {
                return new HomeworkStudentSubmissionDto(
                    student.StudentId,
                    student.StudentName,
                    student.RollNo,
                    detail.Status,
                    detail.Status.ToDisplayString(),
                    detail.SubmittedOn,
                    detail.Marks,
                    detail.Remark);
            }

            return new HomeworkStudentSubmissionDto(
                student.StudentId,
                student.StudentName,
                student.RollNo,
                HomeworkSubmissionStatus.Pending,
                HomeworkSubmissionStatus.Pending.ToDisplayString(),
                null,
                null,
                null);
        }).ToList();

        int submitted = students.Count(s => s.Status == HomeworkSubmissionStatus.Submitted);
        int pending = students.Count(s => s.Status == HomeworkSubmissionStatus.Pending);
        int late = students.Count(s => s.Status == HomeworkSubmissionStatus.Late);
        int total = students.Count;

        string status = HomeworkRepository.ComputeHomeworkStatus(
            homework.DueDate, submitted, pending, late, isSubmitted ? total : 0);

        return new HomeworkDetailResponseDto(
            homework.Id,
            homework.Title,
            homework.Description,
            homework.ClassId,
            className,
            homework.SubjectId,
            subjectName,
            homework.AssignDate,
            homework.DueDate,
            homework.Priority,
            homework.Priority.ToDisplayString(),
            homework.Marks,
            homework.SubmissionType,
            homework.SubmissionType.ToDisplayString(),
            status,
            submitted,
            pending,
            late,
            total,
            isSubmitted,
            students);
    }

    private static HomeworkListItemDto MapListItem(HomeworkListRow row)
    {
        string status = HomeworkRepository.ComputeHomeworkStatus(
            row.DueDate, row.Submitted, row.Pending, row.Late, row.Total);

        return new HomeworkListItemDto(
            row.Id,
            row.Title,
            row.Description,
            row.ClassId,
            row.ClassName,
            row.SubjectId,
            row.SubjectName,
            row.AssignDate,
            row.DueDate,
            (HomeworkPriority)row.Priority,
            ((HomeworkPriority)row.Priority).ToDisplayString(),
            row.Marks,
            (HomeworkSubmissionType)row.SubmissionType,
            ((HomeworkSubmissionType)row.SubmissionType).ToDisplayString(),
            status,
            row.Submitted,
            row.Pending,
            row.Late,
            row.Total);
    }
}
