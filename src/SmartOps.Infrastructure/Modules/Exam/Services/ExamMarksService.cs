using Microsoft.Extensions.Logging;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Infrastructure.Modules.Exam.Services;

public sealed class ExamMarksService : IExamMarksService
{
    private readonly IExamRepository _examRepo;
    private readonly IExamMarksRepository _marksRepo;
    private readonly ILogger<ExamMarksService> _logger;

    public ExamMarksService(
        IExamRepository examRepo,
        IExamMarksRepository marksRepo,
        ILogger<ExamMarksService> logger)
    {
        _examRepo = examRepo;
        _marksRepo = marksRepo;
        _logger = logger;
    }

    public async Task<Result<ExamMarksGridDto>> GetGridAsync(Guid examScheduleId, CancellationToken ct = default)
    {
        ExamScheduleEntity? schedule = await _examRepo.GetScheduleByIdAsync(examScheduleId, ct).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result<ExamMarksGridDto>.Failure("Schedule slot not found.");
        }

        ExamEntity? exam = await _examRepo.GetExamByIdAsync(schedule.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamMarksGridDto>.Failure("Exam not found.");
        }

        return Result<ExamMarksGridDto>.Success(
            await BuildGridAsync(exam, schedule, ct).ConfigureAwait(false));
    }

    public async Task<Result<IList<ExamSubjectProgressDto>>> GetSubjectProgressAsync(
        Guid examId,
        Guid classId,
        CancellationToken ct = default)
    {
        IList<ExamSubjectProgressRow> rows = await _marksRepo
            .GetSubjectProgressAsync(examId, classId, ct)
            .ConfigureAwait(false);
        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(classId, ct).ConfigureAwait(false);

        IList<ExamSubjectProgressDto> result = rows
            .Select(r => new ExamSubjectProgressDto(r.ExamScheduleId, r.SubjectId, r.SubjectName, r.Entered, roster.Count))
            .ToList();

        return Result<IList<ExamSubjectProgressDto>>.Success(result);
    }

    public async Task<Result<ExamMarksGridDto>> SaveMarksAsync(SaveExamMarksRequestDto request, CancellationToken ct = default)
    {
        ExamScheduleEntity? schedule = await _examRepo.GetScheduleByIdAsync(request.ExamScheduleId, ct).ConfigureAwait(false);
        if (schedule is null)
        {
            return Result<ExamMarksGridDto>.Failure("Schedule slot not found.");
        }

        ExamEntity? exam = await _examRepo.GetExamByIdAsync(schedule.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamMarksGridDto>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared)
        {
            return Result<ExamMarksGridDto>.Failure("Result already declared. Marks are locked.");
        }

        IList<ExamMarkComponentEntity> components = await _examRepo.GetComponentsAsync([exam.Id], ct).ConfigureAwait(false);
        Dictionary<Guid, ExamMarkComponentEntity> componentById = components.ToDictionary(c => c.Id);

        var marks = new List<ExamStudentMarkEntity>();
        foreach (SaveStudentMarksDto student in request.Students)
        {
            foreach (ExamComponentMarkDto mark in student.Marks)
            {
                if (!componentById.TryGetValue(mark.ComponentId, out ExamMarkComponentEntity? component))
                {
                    return Result<ExamMarksGridDto>.Failure("Unknown mark component submitted.");
                }

                if (!student.IsAbsent && mark.MarksObtained is { } value && (value < 0 || value > component.MaxMarks))
                {
                    return Result<ExamMarksGridDto>.Failure(
                        $"Marks for component '{component.Name}' must be between 0 and {component.MaxMarks}.");
                }

                marks.Add(new ExamStudentMarkEntity
                {
                    ExamScheduleId = request.ExamScheduleId,
                    ComponentId = mark.ComponentId,
                    StudentId = student.StudentId,
                    MarksObtained = student.IsAbsent ? null : mark.MarksObtained,
                    IsAbsent = student.IsAbsent,
                    Remark = student.Remark?.Trim()
                });
            }
        }

        await _marksRepo.BulkUpsertMarksAsync(request.ExamScheduleId, marks, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Exam marks saved for schedule {ScheduleId}: {Students} students",
            request.ExamScheduleId,
            request.Students.Count);

        return Result<ExamMarksGridDto>.Success(
            await BuildGridAsync(exam, schedule, ct).ConfigureAwait(false));
    }

    private async Task<ExamMarksGridDto> BuildGridAsync(
        ExamEntity exam,
        ExamScheduleEntity schedule,
        CancellationToken ct)
    {
        IList<ExamMarkComponentEntity> components = await _examRepo.GetComponentsAsync([exam.Id], ct).ConfigureAwait(false);
        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(schedule.ClassId, ct).ConfigureAwait(false);
        IList<ExamStudentMarkEntity> existing = await _marksRepo.GetMarksByScheduleAsync(schedule.Id, ct).ConfigureAwait(false);
        IList<ExamScheduleRow> scheduleRows = await _examRepo.GetSchedulesAsync(exam.Id, schedule.ClassId, ct).ConfigureAwait(false);

        ExamScheduleRow? meta = scheduleRows.FirstOrDefault(r => r.Id == schedule.Id);
        ILookup<Guid, ExamStudentMarkEntity> marksByStudent = existing.ToLookup(m => m.StudentId);

        IList<ExamStudentMarksRowDto> students = roster.Select(student =>
        {
            List<ExamStudentMarkEntity> studentMarks = marksByStudent[student.StudentId].ToList();
            bool isAbsent = studentMarks.Count > 0 && studentMarks.All(m => m.IsAbsent);
            string? remark = studentMarks.Select(m => m.Remark).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));

            IList<ExamComponentMarkDto> markDtos = components.Select(component =>
            {
                ExamStudentMarkEntity? mark = studentMarks.FirstOrDefault(m => m.ComponentId == component.Id);
                return new ExamComponentMarkDto(component.Id, mark?.MarksObtained);
            }).ToList();

            return new ExamStudentMarksRowDto(
                student.StudentId,
                student.StudentName,
                student.RollNo,
                isAbsent,
                remark,
                markDtos);
        }).ToList();

        return new ExamMarksGridDto(
            schedule.Id,
            exam.Id,
            exam.Name,
            schedule.ClassId,
            meta?.ClassName ?? string.Empty,
            schedule.SubjectId,
            meta?.SubjectName ?? string.Empty,
            exam.MinPassPercent,
            components
                .Select(c => new ExamMarkComponentDto(c.Id, c.Name, c.MaxMarks, c.PassingMarks, c.DisplayOrder))
                .ToList(),
            students);
    }
}
