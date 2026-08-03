using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Infrastructure.Modules.Exam.Services;

public sealed class ExamResultService : IExamResultService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IExamRepository _examRepo;
    private readonly IExamMarksRepository _marksRepo;
    private readonly IExamResultRepository _resultRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ExamResultService> _logger;

    public ExamResultService(
        IExamRepository examRepo,
        IExamMarksRepository marksRepo,
        IExamResultRepository resultRepo,
        ICurrentUserService currentUser,
        ILogger<ExamResultService> logger)
    {
        _examRepo = examRepo;
        _marksRepo = marksRepo;
        _resultRepo = resultRepo;
        _currentUser = currentUser;
        _logger = logger;
    }

    private sealed record SubjectResultSnapshot(
        Guid SubjectId,
        decimal? Marks,
        decimal MaxMarks,
        bool IsAbsent,
        bool Pass);

    // ── Calculate / declare ──────────────────────────────────

    public async Task<Result<ExamResultSheetDto>> CalculateAsync(
        CalculateExamResultRequestDto request,
        CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(request.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamResultSheetDto>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared)
        {
            return Result<ExamResultSheetDto>.Failure("Result already declared. Recalculation is locked.");
        }

        IList<ExamScheduleRow> schedules = await _examRepo
            .GetSchedulesAsync(request.ExamId, request.ClassId, ct)
            .ConfigureAwait(false);
        if (schedules.Count == 0)
        {
            return Result<ExamResultSheetDto>.Failure("No subjects scheduled for this exam and class.");
        }

        IList<ExamMarkComponentEntity> components = await _examRepo.GetComponentsAsync([exam.Id], ct).ConfigureAwait(false);
        if (components.Count == 0)
        {
            return Result<ExamResultSheetDto>.Failure("Exam has no mark components configured.");
        }

        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(request.ClassId, ct).ConfigureAwait(false);
        if (roster.Count == 0)
        {
            return Result<ExamResultSheetDto>.Failure("No active students found in the selected class.");
        }

        IList<ExamMarkWithSubjectRow> marks = await _marksRepo
            .GetMarksByExamClassAsync(request.ExamId, request.ClassId, ct)
            .ConfigureAwait(false);

        IList<ExamGradeScaleDetailEntity> gradeRows = await ResolveGradeScaleAsync(exam, ct).ConfigureAwait(false);
        decimal subjectMax = components.Sum(c => c.MaxMarks);
        decimal examMax = subjectMax * schedules.Count;

        List<ComputedStudentResult> computed = ComputeStudentResults(
            exam, schedules, components, marks, roster, subjectMax);

        // Rank by total (absent students ranked last).
        var ranked = computed
            .OrderByDescending(c => c.Status != ExamResultStatus.Absent)
            .ThenByDescending(c => c.Total)
            .ToList();

        var entities = new List<ExamResultEntity>();
        for (int i = 0; i < ranked.Count; i++)
        {
            ComputedStudentResult row = ranked[i];
            decimal percentage = examMax > 0 ? Math.Round(row.Total / examMax * 100, 2) : 0;

            entities.Add(new ExamResultEntity
            {
                ExamId = exam.Id,
                ClassId = request.ClassId,
                StudentId = row.Student.StudentId,
                TotalMarks = row.Total,
                MaxMarks = examMax,
                Percentage = percentage,
                Grade = row.Status == ExamResultStatus.Absent ? null : ResolveGrade(gradeRows, percentage),
                Rank = i + 1,
                Result = row.Status,
                SubjectResults = JsonSerializer.Serialize(row.Subjects, JsonOptions)
            });
        }

        await _resultRepo.UpsertResultsAsync(exam.Id, request.ClassId, entities, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Exam results calculated for exam {ExamId}, class {ClassId}: {Count} students",
            exam.Id,
            request.ClassId,
            entities.Count);

        return await GetSheetAsync(exam.Id, request.ClassId, ct).ConfigureAwait(false);
    }

    public async Task<Result<ExamResultSheetDto>> DeclareAsync(
        DeclareExamResultRequestDto request,
        CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(request.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamResultSheetDto>.Failure("Exam not found.");
        }

        if (exam.ResultDeclared)
        {
            return Result<ExamResultSheetDto>.Failure("Result already declared.");
        }

        IList<ExamResultEntity> results = await _resultRepo
            .GetResultsAsync(request.ExamId, request.ClassId, ct)
            .ConfigureAwait(false);
        if (results.Count == 0)
        {
            return Result<ExamResultSheetDto>.Failure("Calculate the result before declaring it.");
        }

        // Persist latest marks into the snapshot before locking (covers marks entered after last Calculate).
        Result<ExamResultSheetDto> refreshed = await CalculateAsync(
                new CalculateExamResultRequestDto(request.ExamId, request.ClassId),
                ct)
            .ConfigureAwait(false);
        if (!refreshed.IsSuccess)
        {
            return refreshed;
        }

        Guid actorId = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);
        DateTime now = SchoolLocalTime.NowDateTime();

        await _resultRepo.MarkResultsDeclaredAsync(request.ExamId, request.ClassId, now, actorId, ct).ConfigureAwait(false);
        await _examRepo.MarkResultDeclaredAsync(request.ExamId, now, actorId, ct).ConfigureAwait(false);

        _logger.LogInformation("Exam result declared for exam {ExamId}, class {ClassId}", request.ExamId, request.ClassId);
        return await GetSheetAsync(request.ExamId, request.ClassId, ct).ConfigureAwait(false);
    }

    // ── Result sheet ─────────────────────────────────────────

    public async Task<Result<ExamResultSheetDto>> GetSheetAsync(Guid examId, Guid classId, CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(examId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ExamResultSheetDto>.Failure("Exam not found.");
        }

        IList<ExamScheduleRow> schedules = await _examRepo.GetSchedulesAsync(examId, classId, ct).ConfigureAwait(false);
        IList<ExamMarkComponentEntity> components = await _examRepo.GetComponentsAsync([examId], ct).ConfigureAwait(false);
        decimal subjectMax = components.Sum(c => c.MaxMarks);
        HashSet<Guid> allowedSubjectIds = schedules.Select(s => s.SubjectId).ToHashSet();

        IList<ExamResultSubjectColumnDto> subjectColumns = schedules
            .Select(s => new ExamResultSubjectColumnDto(s.SubjectId, s.SubjectName, subjectMax))
            .ToList();

        IList<ExamResultEntity> results = await _resultRepo.GetResultsAsync(examId, classId, ct).ConfigureAwait(false);
        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(classId, ct).ConfigureAwait(false);
        Dictionary<Guid, ExamStudentRosterRow> rosterById = roster.ToDictionary(r => r.StudentId);

        IList<ExamClassRow> classes = await _examRepo.GetExamClassesAsync([examId], ct).ConfigureAwait(false);
        string className = classes.FirstOrDefault(c => c.ClassId == classId)?.ClassName ?? string.Empty;

        decimal sheetMaxMarks = subjectMax * schedules.Count;
        bool declared = results.Count > 0 && results.All(r => r.DeclaredOn.HasValue);

        // Undeclared sheets always reflect latest marks so newly entered subjects (e.g. science)
        // do not stay stuck at 0 until the user recalculates.
        Dictionary<Guid, ComputedStudentResult>? liveByStudent = null;
        IList<ExamGradeScaleDetailEntity>? gradeRows = null;
        if (!declared && results.Count > 0 && schedules.Count > 0 && components.Count > 0)
        {
            IList<ExamMarkWithSubjectRow> marks = await _marksRepo
                .GetMarksByExamClassAsync(examId, classId, ct)
                .ConfigureAwait(false);
            liveByStudent = ComputeStudentResults(exam, schedules, components, marks, roster, subjectMax)
                .ToDictionary(c => c.Student.StudentId);
            gradeRows = await ResolveGradeScaleAsync(exam, ct).ConfigureAwait(false);
        }

        var rows = new List<ExamResultRowDto>();
        foreach (ExamResultEntity result in results.OrderBy(r => r.Rank))
        {
            rosterById.TryGetValue(result.StudentId, out ExamStudentRosterRow? student);

            List<SubjectResultSnapshot> subjects;
            decimal totalMarks;
            decimal percentage;
            string? grade = result.Grade;
            ExamResultStatus status = result.Result;

            if (!declared
                && liveByStudent is not null
                && liveByStudent.TryGetValue(result.StudentId, out ComputedStudentResult? live))
            {
                subjects = live.Subjects;
                totalMarks = live.Total;
                percentage = sheetMaxMarks > 0
                    ? Math.Round(totalMarks / sheetMaxMarks * 100, 2)
                    : 0;
                status = live.Status;
                grade = status == ExamResultStatus.Absent
                    ? null
                    : ResolveGrade(gradeRows ?? [], percentage);
            }
            else
            {
                subjects = DeserializeSubjects(result.SubjectResults)
                    .Where(s => allowedSubjectIds.Contains(s.SubjectId))
                    .ToList();
                totalMarks = subjects.Where(s => !s.IsAbsent).Sum(s => s.Marks ?? 0);
                percentage = sheetMaxMarks > 0
                    ? Math.Round(totalMarks / sheetMaxMarks * 100, 2)
                    : 0;
            }

            rows.Add(new ExamResultRowDto(
                result.StudentId,
                student?.StudentName ?? string.Empty,
                student?.RollNo ?? string.Empty,
                result.Rank,
                totalMarks,
                sheetMaxMarks,
                percentage,
                grade,
                status,
                status.ToDisplayString(),
                subjects
                    .Select(s => new ExamResultSubjectMarkDto(s.SubjectId, s.Marks, s.IsAbsent, s.Pass))
                    .ToList()));
        }

        int passCount = rows.Count(r => r.Result == ExamResultStatus.Pass);
        int failCount = rows.Count(r => r.Result == ExamResultStatus.Fail);
        int absentCount = rows.Count(r => r.Result == ExamResultStatus.Absent);
        decimal avgPercent = rows.Count > 0 ? Math.Round(rows.Average(r => r.Percentage), 2) : 0;
        decimal topScore = rows.Count > 0 ? rows.Max(r => r.TotalMarks) : 0;

        return Result<ExamResultSheetDto>.Success(new ExamResultSheetDto(
            exam.Id,
            exam.Name,
            classId,
            className,
            declared,
            rows.Count,
            passCount,
            failCount,
            absentCount,
            avgPercent,
            topScore,
            sheetMaxMarks,
            subjectColumns,
            rows));
    }

    // ── Report card ──────────────────────────────────────────

    public async Task<Result<ReportCardDto>> GetReportCardAsync(Guid examId, Guid studentId, CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(examId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<ReportCardDto>.Failure("Exam not found.");
        }

        ExamResultEntity? result = await _resultRepo.GetStudentResultAsync(examId, studentId, ct).ConfigureAwait(false);
        if (result is null)
        {
            return Result<ReportCardDto>.Failure("Result not calculated for this student yet.");
        }

        // Same class-group schedule filter as marks entry / result sheet.
        IList<ExamScheduleRow> schedules = await _examRepo
            .GetSchedulesAsync(examId, result.ClassId, ct)
            .ConfigureAwait(false);
        HashSet<Guid> allowedSubjectIds = schedules.Select(s => s.SubjectId).ToHashSet();
        Dictionary<Guid, string> subjectNames = schedules
            .GroupBy(s => s.SubjectId)
            .ToDictionary(g => g.Key, g => g.First().SubjectName);

        IList<ExamMarkComponentEntity> components = await _examRepo.GetComponentsAsync([examId], ct).ConfigureAwait(false);
        decimal subjectMax = components.Sum(c => c.MaxMarks);
        decimal sheetMaxMarks = subjectMax * schedules.Count;

        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(result.ClassId, ct).ConfigureAwait(false);
        ExamStudentRosterRow? student = roster.FirstOrDefault(r => r.StudentId == studentId);

        IList<ExamClassRow> classes = await _examRepo.GetExamClassesAsync([examId], ct).ConfigureAwait(false);
        string className = classes.FirstOrDefault(c => c.ClassId == result.ClassId)?.ClassName ?? string.Empty;

        IList<ExamResultEntity> allResults = await _resultRepo.GetResultsAsync(examId, result.ClassId, ct).ConfigureAwait(false);
        IList<ExamGradeScaleDetailEntity> gradeRows = await ResolveGradeScaleAsync(exam, ct).ConfigureAwait(false);

        bool declared = result.DeclaredOn.HasValue;
        List<SubjectResultSnapshot> subjects;
        decimal totalMarks;
        decimal percentage;
        string? grade = result.Grade;
        ExamResultStatus status = result.Result;

        if (!declared && schedules.Count > 0 && components.Count > 0)
        {
            IList<ExamMarkWithSubjectRow> marks = await _marksRepo
                .GetMarksByExamClassAsync(examId, result.ClassId, ct)
                .ConfigureAwait(false);
            ComputedStudentResult? live = ComputeStudentResults(
                    exam, schedules, components, marks, roster, subjectMax)
                .FirstOrDefault(c => c.Student.StudentId == studentId);

            if (live is not null)
            {
                subjects = live.Subjects;
                totalMarks = live.Total;
                percentage = sheetMaxMarks > 0
                    ? Math.Round(totalMarks / sheetMaxMarks * 100, 2)
                    : 0;
                status = live.Status;
                grade = status == ExamResultStatus.Absent
                    ? null
                    : ResolveGrade(gradeRows, percentage);
            }
            else
            {
                subjects = FilterSubjectsToSchedules(result, schedules, allowedSubjectIds, subjectMax);
                totalMarks = subjects.Where(s => !s.IsAbsent).Sum(s => s.Marks ?? 0);
                percentage = sheetMaxMarks > 0
                    ? Math.Round(totalMarks / sheetMaxMarks * 100, 2)
                    : 0;
            }
        }
        else
        {
            subjects = FilterSubjectsToSchedules(result, schedules, allowedSubjectIds, subjectMax);
            totalMarks = subjects.Where(s => !s.IsAbsent).Sum(s => s.Marks ?? 0);
            percentage = sheetMaxMarks > 0
                ? Math.Round(totalMarks / sheetMaxMarks * 100, 2)
                : 0;
            if (schedules.Count > 0)
            {
                status = subjects.All(s => s.IsAbsent)
                    ? ExamResultStatus.Absent
                    : subjects.Any(s => s.IsAbsent || !s.Pass)
                        ? ExamResultStatus.Fail
                        : ExamResultStatus.Pass;
                grade = status == ExamResultStatus.Absent
                    ? null
                    : ResolveGrade(gradeRows, percentage);
            }
        }

        IList<ReportCardSubjectRowDto> subjectRows = subjects.Select(s =>
        {
            decimal percent = s.MaxMarks > 0 && s.Marks.HasValue
                ? Math.Round(s.Marks.Value / s.MaxMarks * 100, 2)
                : 0;
            return new ReportCardSubjectRowDto(
                subjectNames.TryGetValue(s.SubjectId, out string? name) ? name : string.Empty,
                s.MaxMarks,
                s.Marks,
                percent,
                s.IsAbsent ? null : ResolveGrade(gradeRows, percent),
                s.IsAbsent,
                s.Pass);
        }).ToList();

        return Result<ReportCardDto>.Success(new ReportCardDto(
            exam.Id,
            exam.Name,
            exam.ExamType,
            string.Empty,
            studentId,
            student?.StudentName ?? string.Empty,
            student?.RollNo ?? string.Empty,
            className,
            totalMarks,
            sheetMaxMarks > 0 ? sheetMaxMarks : result.MaxMarks,
            percentage,
            grade,
            result.Rank,
            allResults.Count,
            status,
            status.ToDisplayString(),
            subjectRows));
    }

    private static List<SubjectResultSnapshot> FilterSubjectsToSchedules(
        ExamResultEntity result,
        IList<ExamScheduleRow> schedules,
        HashSet<Guid> allowedSubjectIds,
        decimal subjectMax)
    {
        Dictionary<Guid, SubjectResultSnapshot> bySubject = DeserializeSubjects(result.SubjectResults)
            .Where(s => allowedSubjectIds.Contains(s.SubjectId))
            .GroupBy(s => s.SubjectId)
            .ToDictionary(g => g.Key, g => g.First());

        // Schedule order only — drops other class-group subjects left in old snapshots.
        return schedules
            .Select(sc =>
                bySubject.TryGetValue(sc.SubjectId, out SubjectResultSnapshot? snap)
                    ? snap
                    : new SubjectResultSnapshot(
                        sc.SubjectId,
                        0,
                        subjectMax > 0 ? subjectMax : 0,
                        false,
                        false))
            .ToList();
    }

    // ── Hall tickets ─────────────────────────────────────────

    public async Task<Result<IList<HallTicketDto>>> GenerateHallTicketsAsync(
        GenerateHallTicketsRequestDto request,
        CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(request.ExamId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<IList<HallTicketDto>>.Failure("Exam not found.");
        }

        IList<ExamScheduleRow> schedules = await _examRepo
            .GetSchedulesAsync(request.ExamId, request.ClassId, ct)
            .ConfigureAwait(false);
        if (schedules.Count == 0)
        {
            return Result<IList<HallTicketDto>>.Failure(
                "Create the exam schedule first — hall tickets include the subject-wise timetable.");
        }

        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(request.ClassId, ct).ConfigureAwait(false);
        if (roster.Count == 0)
        {
            return Result<IList<HallTicketDto>>.Failure("No active students found in the selected class.");
        }

        IList<ExamHallTicketEntity> existing = await _resultRepo
            .GetHallTicketsAsync(request.ExamId, request.ClassId, ct)
            .ConfigureAwait(false);
        HashSet<Guid> existingStudents = existing.Select(t => t.StudentId).ToHashSet();

        string examCode = BuildExamCode(exam);
        var tickets = new List<ExamHallTicketEntity>();
        int seat = existing.Count;

        foreach (ExamStudentRosterRow student in roster.Where(s => !existingStudents.Contains(s.StudentId)))
        {
            seat++;
            string roll = string.IsNullOrWhiteSpace(student.RollNo)
                ? seat.ToString("D3")
                : student.RollNo.PadLeft(3, '0');

            tickets.Add(new ExamHallTicketEntity
            {
                ExamId = request.ExamId,
                ClassId = request.ClassId,
                StudentId = student.StudentId,
                TicketNo = $"HT-{examCode}-{roll}",
                SeatNo = seat.ToString("D3")
            });
        }

        if (tickets.Count > 0)
        {
            await _resultRepo.BulkInsertHallTicketsAsync(tickets, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Generated {Count} hall tickets for exam {ExamId}, class {ClassId}",
                tickets.Count,
                request.ExamId,
                request.ClassId);
        }

        return await GetHallTicketsAsync(request.ExamId, request.ClassId, ct).ConfigureAwait(false);
    }

    public async Task<Result<IList<HallTicketDto>>> GetHallTicketsAsync(
        Guid examId,
        Guid classId,
        CancellationToken ct = default)
    {
        ExamEntity? exam = await _examRepo.GetExamByIdAsync(examId, ct).ConfigureAwait(false);
        if (exam is null)
        {
            return Result<IList<HallTicketDto>>.Failure("Exam not found.");
        }

        IList<ExamHallTicketEntity> tickets = await _resultRepo.GetHallTicketsAsync(examId, classId, ct).ConfigureAwait(false);
        IList<ExamScheduleRow> schedules = await _examRepo.GetSchedulesAsync(examId, classId, ct).ConfigureAwait(false);
        IList<ExamStudentRosterRow> roster = await _marksRepo.GetClassStudentsAsync(classId, ct).ConfigureAwait(false);
        Dictionary<Guid, ExamStudentRosterRow> rosterById = roster.ToDictionary(r => r.StudentId);

        IList<ExamClassRow> classes = await _examRepo.GetExamClassesAsync([examId], ct).ConfigureAwait(false);
        string className = classes.FirstOrDefault(c => c.ClassId == classId)?.ClassName ?? string.Empty;

        IList<HallTicketScheduleDto> scheduleDtos = schedules
            .OrderBy(s => s.ExamDate)
            .Select(s => new HallTicketScheduleDto(s.SubjectName, s.ExamDate, s.StartTime, s.EndTime, s.RoomNo))
            .ToList();

        DateOnly? startDate = schedules.Count > 0 ? schedules.Min(s => s.ExamDate) : null;
        DateOnly? endDate = schedules.Count > 0 ? schedules.Max(s => s.ExamDate) : null;

        IList<HallTicketDto> result = tickets.Select(t =>
        {
            rosterById.TryGetValue(t.StudentId, out ExamStudentRosterRow? student);
            return new HallTicketDto(
                t.Id,
                t.StudentId,
                student?.StudentName ?? string.Empty,
                student?.RollNo ?? string.Empty,
                className,
                t.TicketNo,
                t.SeatNo,
                examId,
                exam.Name,
                startDate,
                endDate,
                scheduleDtos);
        }).ToList();

        return Result<IList<HallTicketDto>>.Success(result);
    }

    // ── Helpers ──────────────────────────────────────────────

    private sealed record ComputedStudentResult(
        ExamStudentRosterRow Student,
        decimal Total,
        List<SubjectResultSnapshot> Subjects,
        ExamResultStatus Status);

    private static List<ComputedStudentResult> ComputeStudentResults(
        ExamEntity exam,
        IList<ExamScheduleRow> schedules,
        IList<ExamMarkComponentEntity> components,
        IList<ExamMarkWithSubjectRow> marks,
        IList<ExamStudentRosterRow> roster,
        decimal subjectMax)
    {
        ILookup<Guid, ExamMarkWithSubjectRow> marksByStudent = marks.ToLookup(m => m.StudentId);
        var computed = new List<ComputedStudentResult>(roster.Count);

        foreach (ExamStudentRosterRow student in roster)
        {
            List<ExamMarkWithSubjectRow> studentMarks = marksByStudent[student.StudentId].ToList();
            ILookup<Guid, ExamMarkWithSubjectRow> bySchedule = studentMarks.ToLookup(m => m.ExamScheduleId);
            ILookup<Guid, ExamMarkWithSubjectRow> bySubject = studentMarks.ToLookup(m => m.SubjectId);

            var subjects = new List<SubjectResultSnapshot>(schedules.Count);
            decimal total = 0;
            bool anyFail = false;
            bool allAbsent = true;
            bool anyAbsent = false;

            foreach (ExamScheduleRow schedule in schedules)
            {
                List<ExamMarkWithSubjectRow> subjectMarks = ResolveSubjectMarks(schedule, bySchedule, bySubject);
                bool isAbsent = subjectMarks.Count > 0 && subjectMarks.All(m => m.IsAbsent);

                if (isAbsent)
                {
                    anyAbsent = true;
                    subjects.Add(new SubjectResultSnapshot(schedule.SubjectId, null, subjectMax, true, false));
                    continue;
                }

                allAbsent = false;
                decimal obtained = 0;
                bool componentFail = false;

                foreach (ExamMarkComponentEntity component in components)
                {
                    decimal value = subjectMarks
                        .FirstOrDefault(m => m.ComponentId == component.Id)?.MarksObtained ?? 0;
                    obtained += value;

                    if (component.PassingMarks.HasValue && value < component.PassingMarks.Value)
                    {
                        componentFail = true;
                    }
                }

                decimal subjectPercent = subjectMax > 0 ? Math.Round(obtained / subjectMax * 100, 2) : 0;
                bool pass = !componentFail && subjectPercent >= exam.MinPassPercent;
                if (!pass)
                {
                    anyFail = true;
                }

                total += obtained;
                subjects.Add(new SubjectResultSnapshot(schedule.SubjectId, obtained, subjectMax, false, pass));
            }

            ExamResultStatus status = allAbsent
                ? ExamResultStatus.Absent
                : anyFail || anyAbsent
                    ? ExamResultStatus.Fail
                    : ExamResultStatus.Pass;

            computed.Add(new ComputedStudentResult(student, total, subjects, status));
        }

        return computed;
    }

    /// <summary>
    /// Prefer marks keyed by schedule id; fall back to subject id when schedule rows were
    /// recreated or progress/result queries briefly diverge.
    /// </summary>
    private static List<ExamMarkWithSubjectRow> ResolveSubjectMarks(
        ExamScheduleRow schedule,
        ILookup<Guid, ExamMarkWithSubjectRow> bySchedule,
        ILookup<Guid, ExamMarkWithSubjectRow> bySubject)
    {
        List<ExamMarkWithSubjectRow> byId = bySchedule[schedule.Id].ToList();
        if (byId.Count > 0)
        {
            return byId;
        }

        return bySubject[schedule.SubjectId].ToList();
    }

    private async Task<IList<ExamGradeScaleDetailEntity>> ResolveGradeScaleAsync(ExamEntity exam, CancellationToken ct)
    {
        Guid? scaleId = exam.GradeScaleId;
        if (!scaleId.HasValue)
        {
            ExamGroupEntity? group = await _examRepo.GetGroupByIdAsync(exam.ExamGroupId, ct).ConfigureAwait(false);
            scaleId = group?.GradeScaleId;
        }

        if (!scaleId.HasValue)
        {
            IList<ExamGradeScaleEntity> scales = await _examRepo.GetGradeScalesAsync(ct).ConfigureAwait(false);
            scaleId = scales.FirstOrDefault(s => s.IsDefault)?.Id ?? scales.FirstOrDefault()?.Id;
        }

        if (!scaleId.HasValue)
        {
            return [];
        }

        return await _examRepo.GetGradeScaleDetailsAsync([scaleId.Value], ct).ConfigureAwait(false);
    }

    private static string? ResolveGrade(IList<ExamGradeScaleDetailEntity> gradeRows, decimal percentage)
    {
        ExamGradeScaleDetailEntity? match = gradeRows
            .FirstOrDefault(g => percentage >= g.MinPercent && percentage <= g.MaxPercent);

        // Percent on a boundary not covered exactly (e.g. 90.5 between 90 and 91) → nearest lower band.
        match ??= gradeRows
            .Where(g => g.MaxPercent <= percentage)
            .OrderByDescending(g => g.MaxPercent)
            .FirstOrDefault();

        return match?.Grade;
    }

    private static string BuildExamCode(ExamEntity exam)
    {
        string initials = new(exam.Name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]))
            .Take(4)
            .ToArray());
        return $"{initials}{SchoolLocalTime.NowDateTime().Year % 100:D2}";
    }

    private static List<SubjectResultSnapshot> DeserializeSubjects(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<SubjectResultSnapshot>>(json, JsonOptions) ?? [];
}
