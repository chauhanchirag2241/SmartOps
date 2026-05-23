using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureService
{
    Task<Result<IList<FeeStructureVersionListItemDto>>> GetVersionsAsync(
        Guid? academicYearId,
        string? statusFilter,
        CancellationToken ct = default);

    Task<Result<FeeStructureVersionDetailDto>> GetVersionDetailAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<FeeStructureVersionListItemDto>> CreateVersionAsync(
        CreateFeeStructureVersionRequestDto request,
        CancellationToken ct = default);

    Task<Result<FeeStructureVersionListItemDto>> PublishVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<FeeStructureVersionListItemDto>> ActivateVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<FeeStructureVersionListItemDto>> CreateNewVersionFromAsync(
        Guid sourceVersionId,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<FeeTypeDto>> CreateFeeTypeAsync(CreateFeeTypeRequestDto request, CancellationToken ct = default);

    Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(Guid id, UpdateFeeTypeRequestDto request, CancellationToken ct = default);

    Task<Result<bool>> DeleteFeeTypeAsync(Guid id, CancellationToken ct = default);

    Task<Result<FeeStructureStatsDto>> GetStatsAsync(CancellationToken ct = default);

    Task<Result<FeeSettingsDto>> GetSettingsAsync(CancellationToken ct = default);

    Task<Result<FeeSettingsDto>> UpsertSettingsAsync(UpsertFeeSettingsRequestDto request, CancellationToken ct = default);

    Task<Guid?> ResolveActiveVersionIdForYearAsync(Guid academicYearId, CancellationToken ct = default);
}
