namespace SmartOps.Application.Modules.Branch;

public interface IBranchContext
{
    bool IsResolved { get; }

    IReadOnlyList<Guid> AllowedBranchIds { get; }

    Guid? ActiveBranchId { get; }

    /// <summary>Branch IDs selected for multi-branch reports/dashboard (subset of allowed).</summary>
    IReadOnlyList<Guid> SelectedBranchIds { get; }

    bool CanViewAllBranches { get; }

    Task EnsureResolvedAsync(CancellationToken cancellationToken = default);

    bool HasBranchAccess(Guid branchId);
}
