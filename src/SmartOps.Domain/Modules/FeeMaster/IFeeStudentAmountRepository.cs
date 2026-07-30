using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Domain.Modules.FeeMaster;

public interface IFeeStudentAmountRepository
{
    Task<PagedResult<FeeStudentListModel>> GetStudentsAsync(
        Guid feeMasterId,
        string applicableTo,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        IReadOnlyList<Guid>? classIds,
        string? sortColumn,
        string? sortDirection,
        CancellationToken cancellationToken = default);

    Task<FeeStudentDetailModel?> GetStudentDetailAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task UpsertOverridesAsync(
        Guid feeMasterId,
        Guid studentId,
        Guid branchId,
        IReadOnlyList<FeeStudentAmountEntity> rows,
        CancellationToken cancellationToken = default);

    Task SoftDeleteByStudentAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<bool> StudentExistsOnMasterAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}
