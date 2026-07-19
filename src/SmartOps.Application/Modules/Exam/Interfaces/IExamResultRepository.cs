using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamResultRepository
{
    Task UpsertResultsAsync(Guid examId, Guid classId, IList<ExamResultEntity> results, CancellationToken ct = default);
    Task<IList<ExamResultEntity>> GetResultsAsync(Guid examId, Guid? classId, CancellationToken ct = default);
    Task<ExamResultEntity?> GetStudentResultAsync(Guid examId, Guid studentId, CancellationToken ct = default);
    Task MarkResultsDeclaredAsync(Guid examId, Guid classId, DateTime declaredOn, Guid declaredBy, CancellationToken ct = default);

    Task<IList<ExamHallTicketEntity>> GetHallTicketsAsync(Guid examId, Guid classId, CancellationToken ct = default);
    Task BulkInsertHallTicketsAsync(IList<ExamHallTicketEntity> tickets, CancellationToken ct = default);
}
