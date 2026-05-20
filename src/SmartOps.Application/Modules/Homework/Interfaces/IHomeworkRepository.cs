using SmartOps.Domain.Modules.Homework;

namespace SmartOps.Application.Modules.Homework.Interfaces;

public interface IHomeworkRepository
{
    Task<Guid> CreateAsync(HomeworkEntity homework, CancellationToken ct = default);
    Task UpdateAsync(HomeworkEntity homework, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<HomeworkEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<HomeworkListRow>> GetListAsync(
        Guid? classId,
        Guid? subjectId,
        string? statusFilter,
        string? searchTerm,
        CancellationToken ct = default);
    Task<HomeworkStatsRow> GetStatsAsync(CancellationToken ct = default);
    Task<IList<HomeworkDetailEntity>> GetDetailsByHomeworkIdAsync(Guid homeworkId, CancellationToken ct = default);
    Task<bool> HasSubmissionsAsync(Guid homeworkId, CancellationToken ct = default);
    Task BulkInsertDetailsAsync(IList<HomeworkDetailEntity> details, CancellationToken ct = default);
    Task BulkUpsertDetailsAsync(IList<HomeworkDetailEntity> details, CancellationToken ct = default);
    Task<IList<HomeworkStudentRow>> GetClassStudentsForHomeworkAsync(Guid classId, CancellationToken ct = default);
    Task<HomeworkMetaRow?> GetMetaByHomeworkIdAsync(Guid homeworkId, CancellationToken ct = default);
}

public sealed class HomeworkMetaRow
{
    public string ClassName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
}

public sealed class HomeworkListRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid ClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public DateOnly AssignDate { get; init; }
    public DateOnly DueDate { get; init; }
    public int Priority { get; init; }
    public int? Marks { get; init; }
    public int SubmissionType { get; init; }
    public int Submitted { get; init; }
    public int Pending { get; init; }
    public int Late { get; init; }
    public int Total { get; init; }
}

public sealed class HomeworkStatsRow
{
    public int TotalAssigned { get; init; }
    public int DueToday { get; init; }
    public int TotalSubmissions { get; init; }
    public int Overdue { get; init; }
}

public sealed class HomeworkStudentRow
{
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string RollNo { get; init; } = string.Empty;
}
