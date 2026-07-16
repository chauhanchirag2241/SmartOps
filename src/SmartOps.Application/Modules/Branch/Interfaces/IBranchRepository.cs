using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Modules.Branch.Interfaces;

public interface IBranchRepository
{
    Task<IReadOnlyList<BranchDropdownItemDto>> GetBranchesBySchoolAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetUserBranchIdsAsync(
        Guid userId,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchDropdownItemDto>> GetUserBranchesAsync(
        Guid userId,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task SetUserBranchesAsync(
        Guid userId,
        Guid schoolId,
        IReadOnlyList<Guid> branchIds,
        Guid? defaultBranchId,
        CancellationToken cancellationToken = default);

    Task SyncBranchesToSchoolDatabaseAsync(
        Guid schoolId,
        IReadOnlyList<SchoolBranchEntity> branches,
        CancellationToken cancellationToken = default);
}
