using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeCollectionService
{
    Task<Result<IList<FeeCollectionStudentListItemDto>>> GetStudentsAsync(
        Guid? classId,
        Guid? academicYearId,
        string? search,
        string? statusFilter,
        CancellationToken ct = default);

    Task<Result<FeeCollectionStudentDetailDto>> GetStudentDetailAsync(
        Guid studentId,
        Guid? academicYearId,
        CancellationToken ct = default);

    Task<Result<CollectFeeResponseDto>> CollectFeeAsync(CollectFeeRequestDto request, CancellationToken ct = default);
}
