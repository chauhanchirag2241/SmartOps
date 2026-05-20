using SmartOps.Application.Modules.Homework;
using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Homework.Interfaces;

public interface IHomeworkService
{
    Task<Result<IList<HomeworkListItemDto>>> GetListAsync(
        Guid? classId,
        Guid? subjectId,
        string? statusFilter,
        string? searchTerm,
        CancellationToken ct = default);

    Task<Result<HomeworkStatsDto>> GetStatsAsync(CancellationToken ct = default);

    Task<Result<HomeworkDetailResponseDto>> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<Result<HomeworkDetailResponseDto>> CreateAsync(CreateHomeworkRequestDto request, CancellationToken ct = default);

    Task<Result<HomeworkDetailResponseDto>> UpdateAsync(Guid id, UpdateHomeworkRequestDto request, CancellationToken ct = default);

    Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Result<HomeworkDetailResponseDto>> SubmitSubmissionsAsync(
        Guid homeworkId,
        SubmitHomeworkSubmissionsRequestDto request,
        CancellationToken ct = default);

    Task<Result<HomeworkDetailResponseDto>> UpdateSubmissionsAsync(
        Guid homeworkId,
        UpdateHomeworkSubmissionsRequestDto request,
        CancellationToken ct = default);
}
