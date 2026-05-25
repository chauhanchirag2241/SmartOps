using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureRepository
{
    Task<IList<FeeStructureVersionListRow>> GetVersionsAsync(
        Guid? academicYearId,
        FeeStructureVersionStatus? status,
        CancellationToken ct = default);

    Task<FeeStructureVersionEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default);

    Task<FeeStructureVersionEntity?> GetActiveVersionForYearAsync(Guid academicYearId, CancellationToken ct = default);

    /// <summary>Active version if any, otherwise latest published (never draft).</summary>
    Task<FeeStructureVersionEntity?> GetAdmissionVersionForYearAsync(Guid academicYearId, CancellationToken ct = default);

    Task<int> GetNextVersionNumberAsync(Guid academicYearId, CancellationToken ct = default);

    Task<Guid> CreateVersionAsync(FeeStructureVersionEntity entity, CancellationToken ct = default);

    Task UpdateVersionAsync(FeeStructureVersionEntity entity, CancellationToken ct = default);

    Task SoftDeleteVersionAsync(Guid id, CancellationToken ct = default);

    Task ArchiveActiveVersionsForYearAsync(Guid academicYearId, Guid exceptVersionId, CancellationToken ct = default);

    Task ArchivePublishedVersionsForYearAsync(Guid academicYearId, Guid exceptVersionId, CancellationToken ct = default);

    Task<bool> VersionHasPaymentsAsync(Guid versionId, CancellationToken ct = default);

    Task<bool> VersionHasAssignedStudentsAsync(Guid versionId, CancellationToken ct = default);

    Task<Guid> CloneVersionAsync(Guid sourceVersionId, Guid newVersionId, CancellationToken ct = default);

    Task<IList<FeeTypeListRow>> GetFeeTypesAsync(Guid feeStructureVersionId, CancellationToken ct = default);

    Task<FeeTypeEntity?> GetFeeTypeByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CreateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default);

    Task UpdateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default);

    Task SoftDeleteFeeTypeAsync(Guid id, CancellationToken ct = default);

    Task<bool> FeeTypeHasPaymentsAsync(Guid feeTypeId, CancellationToken ct = default);

    Task<int> CountActiveFeeTypesForVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<int> CountClassesWithAmountsForVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<FeeSettingsEntity?> GetSettingsAsync(CancellationToken ct = default);

    Task<Guid> UpsertSettingsAsync(FeeSettingsEntity entity, CancellationToken ct = default);

    Task<string?> GetAcademicYearTitleAsync(Guid academicYearId, CancellationToken ct = default);
}

public sealed class FeeStructureVersionListRow
{
    public Guid Id { get; init; }
    public Guid AcademicYearId { get; init; }
    public string AcademicYearTitle { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public FeeStructureVersionStatus Status { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateTime? PublishedOn { get; init; }
    public DateTime? ActivatedOn { get; init; }
    public int FeeTypeCount { get; init; }
    public bool HasStudentPayments { get; init; }
}

public sealed class FeeTypeListRow
{
    public Guid Id { get; init; }
    public Guid FeeStructureVersionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public FeeCategory Category { get; init; }
    public FeeCollectionType CollectionType { get; init; }
    public bool IsMandatory { get; init; }
    public bool IsRefundable { get; init; }
    public bool StudentWiseDifferentAmount { get; init; }
    public bool IsActive { get; init; }
    public bool HasStudentPayments { get; init; }
}
