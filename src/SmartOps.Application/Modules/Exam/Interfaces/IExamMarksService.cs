using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamMarksService
{
    Task<Result<ExamMarksGridDto>> GetGridAsync(Guid examScheduleId, CancellationToken ct = default);
    Task<Result<IList<ExamSubjectProgressDto>>> GetSubjectProgressAsync(Guid examId, Guid classId, CancellationToken ct = default);
    Task<Result<ExamMarksGridDto>> SaveMarksAsync(SaveExamMarksRequestDto request, CancellationToken ct = default);
}
