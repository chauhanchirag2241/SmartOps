using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Exam;

namespace SmartOps.Application.Modules.Exam.Interfaces;

public interface IExamService
{
    // Grade scales
    Task<Result<IList<ExamGradeScaleDto>>> GetGradeScalesAsync(CancellationToken ct = default);
    Task<Result<ExamGradeScaleDto>> GetGradeScaleAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExamGradeScaleDto>> CreateGradeScaleAsync(SaveExamGradeScaleRequestDto request, CancellationToken ct = default);
    Task<Result<ExamGradeScaleDto>> UpdateGradeScaleAsync(Guid id, SaveExamGradeScaleRequestDto request, CancellationToken ct = default);
    Task<Result<bool>> DeleteGradeScaleAsync(Guid id, CancellationToken ct = default);

    // Exam groups
    Task<Result<IList<ExamGroupDto>>> GetGroupsAsync(CancellationToken ct = default);
    Task<Result<ExamGroupDto>> CreateGroupAsync(SaveExamGroupRequestDto request, CancellationToken ct = default);
    Task<Result<ExamGroupDto>> UpdateGroupAsync(Guid id, SaveExamGroupRequestDto request, CancellationToken ct = default);
    Task<Result<bool>> DeleteGroupAsync(Guid id, CancellationToken ct = default);

    // Exams
    Task<Result<IList<ExamListItemDto>>> GetExamsAsync(Guid? groupId, Guid? classId, int? status, string? search, CancellationToken ct = default);
    Task<Result<ExamStatsDto>> GetExamStatsAsync(CancellationToken ct = default);
    Task<Result<ExamDetailDto>> GetExamAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExamDetailDto>> CreateExamAsync(SaveExamRequestDto request, CancellationToken ct = default);
    Task<Result<ExamDetailDto>> UpdateExamAsync(Guid id, SaveExamRequestDto request, CancellationToken ct = default);
    Task<Result<bool>> DeleteExamAsync(Guid id, CancellationToken ct = default);
    Task<Result<bool>> UpdateExamStatusAsync(Guid id, ExamStatus status, CancellationToken ct = default);

    // Schedules
    Task<Result<IList<ExamScheduleItemDto>>> GetSchedulesAsync(Guid? examId, Guid? classId, CancellationToken ct = default);
    Task<Result<ExamScheduleItemDto>> CreateScheduleAsync(SaveExamScheduleRequestDto request, CancellationToken ct = default);
    Task<Result<ExamScheduleItemDto>> UpdateScheduleAsync(Guid id, SaveExamScheduleRequestDto request, CancellationToken ct = default);
    Task<Result<bool>> DeleteScheduleAsync(Guid id, CancellationToken ct = default);
}
