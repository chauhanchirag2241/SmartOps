using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface ISalaryStructureRepository
{
    Task<IList<SalaryStructureVersionListRow>> GetVersionsAsync(
        SalaryStructureVersionStatus? status,
        CancellationToken ct = default);

    Task<SalaryStructureVersionEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default);

    Task<SalaryStructureVersionEntity?> GetActiveVersionAsync(CancellationToken ct = default);

    /// <summary>Active version if any, otherwise latest published (never draft).</summary>
    Task<SalaryStructureVersionEntity?> GetAdmissionVersionAsync(CancellationToken ct = default);

    Task<int> GetNextVersionNumberAsync(CancellationToken ct = default);

    Task<Guid> CreateVersionAsync(SalaryStructureVersionEntity entity, CancellationToken ct = default);

    Task UpdateVersionAsync(SalaryStructureVersionEntity entity, CancellationToken ct = default);

    Task SoftDeleteVersionAsync(Guid id, CancellationToken ct = default);

    Task ArchiveActiveVersionsAsync(Guid exceptVersionId, CancellationToken ct = default);

    Task ArchivePublishedVersionsAsync(Guid exceptVersionId, CancellationToken ct = default);

    Task<bool> VersionHasAssignedEmployeesAsync(Guid versionId, CancellationToken ct = default);

    Task<Guid> CloneVersionAsync(Guid sourceVersionId, Guid newVersionId, CancellationToken ct = default);

    Task<IList<SalaryVersionComponentListRow>> GetComponentsAsync(Guid salaryStructureVersionId, CancellationToken ct = default);

    Task<SalaryVersionComponentEntity?> GetComponentByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CreateComponentAsync(SalaryVersionComponentEntity entity, CancellationToken ct = default);

    Task UpdateComponentAsync(SalaryVersionComponentEntity entity, CancellationToken ct = default);

    Task SoftDeleteComponentAsync(Guid id, CancellationToken ct = default);

    Task<int> CountActiveComponentsForVersionAsync(Guid versionId, CancellationToken ct = default);
}

public sealed class SalaryStructureVersionListRow
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public SalaryStructureVersionStatus Status { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public int ComponentCount { get; init; }
    public bool HasAssignedEmployees { get; init; }
}

public sealed class SalaryVersionComponentListRow
{
    public Guid Id { get; init; }
    public Guid SalaryStructureVersionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortCode { get; init; }
    public SalaryComponentType ComponentType { get; init; }
    public SalaryCalculationType CalculationType { get; init; }
    public decimal Value { get; init; }
    public bool IsTaxable { get; init; }
    public bool IsActive { get; init; }
}
