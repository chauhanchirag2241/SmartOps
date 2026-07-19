using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamMarksRepository
{
    Task<IList<ExamStudentRosterRow>> GetClassStudentsAsync(Guid classId, CancellationToken ct = default);
    Task<IList<ExamStudentMarkEntity>> GetMarksByScheduleAsync(Guid scheduleId, CancellationToken ct = default);
    Task<IList<ExamMarkWithSubjectRow>> GetMarksByExamClassAsync(Guid examId, Guid classId, CancellationToken ct = default);
    Task BulkUpsertMarksAsync(Guid scheduleId, IList<ExamStudentMarkEntity> marks, CancellationToken ct = default);
    Task<IList<ExamSubjectProgressRow>> GetSubjectProgressAsync(Guid examId, Guid classId, CancellationToken ct = default);
}

public sealed class ExamStudentRosterRow
{
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string RollNo { get; init; } = string.Empty;
}

public sealed class ExamMarkWithSubjectRow
{
    public Guid ExamScheduleId { get; init; }
    public Guid SubjectId { get; init; }
    public Guid ComponentId { get; init; }
    public Guid StudentId { get; init; }
    public decimal? MarksObtained { get; init; }
    public bool IsAbsent { get; init; }
}

public sealed class ExamSubjectProgressRow
{
    public Guid ExamScheduleId { get; init; }
    public Guid SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public int Entered { get; init; }
}
