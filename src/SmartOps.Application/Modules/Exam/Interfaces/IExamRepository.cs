using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamRepository
{
    // Grade scales
    Task<IList<ExamGradeScaleEntity>> GetGradeScalesAsync(CancellationToken ct = default);
    Task<ExamGradeScaleEntity?> GetGradeScaleByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<ExamGradeScaleDetailEntity>> GetGradeScaleDetailsAsync(IReadOnlyCollection<Guid> scaleIds, CancellationToken ct = default);
    Task<Guid> CreateGradeScaleAsync(ExamGradeScaleEntity scale, IList<ExamGradeScaleDetailEntity> details, CancellationToken ct = default);
    Task UpdateGradeScaleAsync(ExamGradeScaleEntity scale, IList<ExamGradeScaleDetailEntity> details, CancellationToken ct = default);
    Task SoftDeleteGradeScaleAsync(Guid id, CancellationToken ct = default);
    Task<bool> GradeScaleInUseAsync(Guid id, CancellationToken ct = default);

    // Exam groups
    Task<IList<ExamGroupRow>> GetGroupsAsync(CancellationToken ct = default);
    Task<ExamGroupEntity?> GetGroupByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateGroupAsync(ExamGroupEntity group, CancellationToken ct = default);
    Task UpdateGroupAsync(ExamGroupEntity group, CancellationToken ct = default);
    Task SoftDeleteGroupAsync(Guid id, CancellationToken ct = default);
    Task<bool> GroupHasExamsAsync(Guid id, CancellationToken ct = default);

    // Exams
    Task<IList<ExamRow>> GetExamsAsync(Guid? groupId, Guid? classId, int? status, string? search, CancellationToken ct = default);
    Task<ExamEntity?> GetExamByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<ExamClassRow>> GetExamClassesAsync(IReadOnlyCollection<Guid> examIds, CancellationToken ct = default);
    Task<IList<ExamMarkComponentEntity>> GetComponentsAsync(IReadOnlyCollection<Guid> examIds, CancellationToken ct = default);
    Task<Guid> CreateExamAsync(ExamEntity exam, IList<Guid> classIds, IList<ExamMarkComponentEntity> components, CancellationToken ct = default);
    Task UpdateExamAsync(ExamEntity exam, IList<Guid> classIds, IList<ExamMarkComponentEntity> components, CancellationToken ct = default);
    Task SoftDeleteExamAsync(Guid id, CancellationToken ct = default);
    Task UpdateExamStatusAsync(Guid id, ExamStatus status, CancellationToken ct = default);
    Task MarkResultDeclaredAsync(Guid examId, DateTime declaredOn, Guid declaredBy, CancellationToken ct = default);

    // Schedules
    Task<IList<ExamScheduleRow>> GetSchedulesAsync(Guid? examId, Guid? classId, CancellationToken ct = default);
    Task<ExamScheduleEntity?> GetScheduleByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateScheduleAsync(ExamScheduleEntity schedule, CancellationToken ct = default);
    Task UpdateScheduleAsync(ExamScheduleEntity schedule, CancellationToken ct = default);
    Task SoftDeleteScheduleAsync(Guid id, CancellationToken ct = default);
    Task<bool> ScheduleExistsAsync(Guid examId, Guid classId, Guid subjectId, Guid? excludeId, CancellationToken ct = default);
}

public sealed class ExamGroupRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid AcademicYearId { get; init; }
    public string AcademicYearTitle { get; init; } = string.Empty;
    public Guid? GradeScaleId { get; init; }
    public string? GradeScaleName { get; init; }
    public int EvaluationType { get; init; }
    public int ExamCount { get; init; }
}

public sealed class ExamRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ExamType { get; init; } = string.Empty;
    public Guid ExamGroupId { get; init; }
    public string ExamGroupName { get; init; } = string.Empty;
    public Guid? AcademicPeriodId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal MinPassPercent { get; init; }
    public Guid? GradeScaleId { get; init; }
    public int Status { get; init; }
    public bool ResultDeclared { get; init; }
    public string? Description { get; init; }
    public decimal TotalMaxMarks { get; init; }
    public int SubjectCount { get; init; }
}

public sealed class ExamClassRow
{
    public Guid ExamId { get; init; }
    public Guid ClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
}

public sealed class ExamScheduleRow
{
    public Guid Id { get; init; }
    public Guid ExamId { get; init; }
    public string ExamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public DateOnly ExamDate { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public string? RoomNo { get; init; }
    public Guid? InvigilatorId { get; init; }
    public string? InvigilatorName { get; init; }
    public decimal MaxMarks { get; init; }
}
