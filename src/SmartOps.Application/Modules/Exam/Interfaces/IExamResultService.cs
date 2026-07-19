using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamResultService
{
    Task<Result<ExamResultSheetDto>> CalculateAsync(CalculateExamResultRequestDto request, CancellationToken ct = default);
    Task<Result<ExamResultSheetDto>> DeclareAsync(DeclareExamResultRequestDto request, CancellationToken ct = default);
    Task<Result<ExamResultSheetDto>> GetSheetAsync(Guid examId, Guid classId, CancellationToken ct = default);
    Task<Result<ReportCardDto>> GetReportCardAsync(Guid examId, Guid studentId, CancellationToken ct = default);

    Task<Result<IList<HallTicketDto>>> GenerateHallTicketsAsync(GenerateHallTicketsRequestDto request, CancellationToken ct = default);
    Task<Result<IList<HallTicketDto>>> GetHallTicketsAsync(Guid examId, Guid classId, CancellationToken ct = default);
}
