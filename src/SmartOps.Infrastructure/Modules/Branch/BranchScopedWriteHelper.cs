using SmartOps.Application.Modules.Branch;

namespace SmartOps.Infrastructure.Modules.Branch;

public sealed class BranchScopedWriteHelper : IBranchScopedWriteHelper
{
    private readonly IBranchContext _branchContext;

    public BranchScopedWriteHelper(IBranchContext branchContext)
    {
        _branchContext = branchContext;
    }

    public async Task<Guid> ResolveWriteBranchIdAsync(
        Guid currentBranchId,
        CancellationToken cancellationToken = default)
    {
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        if (currentBranchId != Guid.Empty)
        {
            if (!_branchContext.HasBranchAccess(currentBranchId))
            {
                throw new UnauthorizedAccessException("You do not have access to the selected branch.");
            }

            return currentBranchId;
        }

        if (_branchContext.ActiveBranchId is not Guid activeBranchId || activeBranchId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Active branch is required. Select a branch from the header before creating records.");
        }

        if (!_branchContext.HasBranchAccess(activeBranchId))
        {
            throw new UnauthorizedAccessException("You do not have access to the active branch.");
        }

        return activeBranchId;
    }
}
