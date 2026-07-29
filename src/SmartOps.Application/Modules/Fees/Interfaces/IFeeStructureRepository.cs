using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureRepository
{
    Task<IList<FeeStructureVersionListRow>> GetVersionsAsync(
        FeeStructureVersionStatus? status,
        CancellationToken ct = default);

    Task<FeeStructureEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default);

    Task<FeeStructureEntity?> GetActiveFeeStructureAsync(CancellationToken ct = default);

    /// <summary>Active structure if any, otherwise latest published (never draft).</summary>
    Task<FeeStructureEntity?> GetAdmissionFeeStructureAsync(CancellationToken ct = default);

    Task<int> GetNextVersionNumberAsync(CancellationToken ct = default);

    Task<Guid> CreateVersionAsync(FeeStructureEntity entity, CancellationToken ct = default);

    Task UpdateVersionAsync(FeeStructureEntity entity, CancellationToken ct = default);

    Task SoftDeleteVersionAsync(Guid id, CancellationToken ct = default);

    Task ArchiveActiveStructuresAsync(Guid exceptVersionId, CancellationToken ct = default);

    Task ArchivePublishedStructuresAsync(Guid exceptVersionId, CancellationToken ct = default);

    Task<bool> VersionHasPaymentsAsync(Guid versionId, CancellationToken ct = default);

    Task<bool> VersionHasAssignedStudentsAsync(Guid versionId, CancellationToken ct = default);

    Task<Guid> CloneVersionAsync(Guid sourceVersionId, Guid newVersionId, CancellationToken ct = default);

    Task<IList<FeeHeadListRow>> GetFeeHeadsAsync(Guid feeStructureId, CancellationToken ct = default);

    Task<FeeHeadEntity?> GetFeeHeadByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CreateFeeHeadAsync(FeeHeadEntity entity, CancellationToken ct = default);

    Task UpdateFeeHeadAsync(FeeHeadEntity entity, CancellationToken ct = default);

    Task SoftDeleteFeeHeadAsync(Guid id, CancellationToken ct = default);

    Task<bool> FeeHeadHasPaymentsAsync(Guid feeHeadId, CancellationToken ct = default);

    Task<int> CountActiveFeeHeadsForStructureAsync(Guid versionId, CancellationToken ct = default);

    Task<int> CountClassesWithAmountsForVersionAsync(Guid versionId, CancellationToken ct = default);
}

public sealed class FeeStructureVersionListRow
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public FeeStructureVersionStatus Status { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateTime? PublishedOn { get; init; }
    public DateTime? ActivatedOn { get; init; }
    public int FeeHeadCount { get; init; }
    public bool HasStudentPayments { get; init; }
}

public sealed class FeeHeadListRow
{
    public Guid Id { get; init; }
    public Guid FeeStructureId { get; init; }
    public string Name { get; init; } = string.Empty;
    public FeeCategory Category { get; init; }
    public FeeCollectionType CollectionType { get; init; }
    public bool IsMandatory { get; init; }
    public bool IsRefundable { get; init; }
    public bool StudentWiseDifferentAmount { get; init; }
    public bool IsActive { get; init; }
    public bool HasStudentPayments { get; init; }
}
