namespace SmartOps.Application.Modules.Branch;

public interface IBranchScopedWriteHelper
{
    Task<Guid> ResolveWriteBranchIdAsync(Guid currentBranchId, CancellationToken cancellationToken = default);
}
