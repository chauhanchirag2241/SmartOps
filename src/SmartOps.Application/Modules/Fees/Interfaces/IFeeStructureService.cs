using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureService
{
    Task<Result<IList<FeeStructureVersionListItemDto>>> GetVersionsAsync(
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

    Task<Result<FeeHeadDto>> CreateFeeHeadAsync(CreateFeeHeadRequestDto request, CancellationToken ct = default);

    Task<Result<FeeHeadDto>> UpdateFeeHeadAsync(Guid id, UpdateFeeHeadRequestDto request, CancellationToken ct = default);

    Task<Result<bool>> DeleteFeeHeadAsync(Guid id, CancellationToken ct = default);

    Task<Guid?> ResolveActiveFeeStructureIdAsync(CancellationToken ct = default);
}
