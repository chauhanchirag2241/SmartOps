namespace SmartOps.Application.Modules.Branch;

public sealed class BranchDropdownItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsHeadOffice { get; init; }

    public bool IsDefault { get; init; }
}

public sealed class MyBranchesResponseDto
{
    public IReadOnlyList<BranchDropdownItemDto> Branches { get; init; } = [];

    public bool CanViewAllBranches { get; init; }
}

public sealed class UpdateUserBranchesDto
{
    public IReadOnlyList<Guid> BranchIds { get; set; } = [];

    public Guid? DefaultBranchId { get; set; }
}
