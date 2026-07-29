using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class FeeStructureService : IFeeStructureService
{
    private readonly IFeeStructureRepository _repo;
    private readonly IClassFeeInstallmentService _installmentService;
    private readonly IUserScopeContext _scope;

    public FeeStructureService(
        IFeeStructureRepository repo,
        IClassFeeInstallmentService installmentService,
        IUserScopeContext scope)
    {
        _repo = repo;
        _installmentService = installmentService;
        _scope = scope;
    }

    public async Task<Result<IList<FeeStructureVersionListItemDto>>> GetVersionsAsync(
        string? statusFilter,
        CancellationToken ct = default)
    {
        FeeStructureVersionStatus? status = ParseStatusFilter(statusFilter);
        IList<FeeStructureVersionListRow> rows = await _repo.GetVersionsAsync(status, ct).ConfigureAwait(false);
        IList<FeeStructureVersionListItemDto> dtos = rows.Select(MapVersionListItem).ToList();
        return Result<IList<FeeStructureVersionListItemDto>>.Success(dtos);
    }

    public async Task<Result<FeeStructureVersionDetailDto>> GetVersionDetailAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionDetailDto>.Failure("Fee structure version not found.");
        }

        IList<FeeHeadListRow> types = await _repo.GetFeeHeadsAsync(versionId, ct).ConfigureAwait(false);
        bool hasPayments = await _repo.VersionHasPaymentsAsync(versionId, ct).ConfigureAwait(false);

        return Result<FeeStructureVersionDetailDto>.Success(new FeeStructureVersionDetailDto(
            version.Id,
            version.VersionNumber,
            version.Status,
            FeeLabelHelper.VersionStatusLabel(version.Status),
            version.EffectiveDate,
            version.PublishedOn,
            version.ActivatedOn,
            hasPayments,
            IsVersionLocked(version.Status),
            types.Select(MapFeeHead).ToList()));
    }

    public async Task<Result<FeeStructureVersionListItemDto>> CreateVersionAsync(
        CreateFeeStructureVersionRequestDto request,
        CancellationToken ct = default)
    {
        int versionNumber = await _repo.GetNextVersionNumberAsync(ct).ConfigureAwait(false);
        var entity = new FeeStructureEntity
        {
            VersionNumber = versionNumber,
            Status = FeeStructureVersionStatus.Draft,
            EffectiveDate = request.EffectiveDate
        };
        Guid versionId = await _repo.CreateVersionAsync(entity, ct).ConfigureAwait(false);

        if (request.CloneFromVersionId.HasValue && request.CloneFromVersionId.Value != Guid.Empty)
        {
            FeeStructureEntity? source = await _repo.GetVersionByIdAsync(request.CloneFromVersionId.Value, ct).ConfigureAwait(false);
            if (source is null)
            {
                return Result<FeeStructureVersionListItemDto>.Failure("Source fee structure version not found.");
            }

            await _repo.CloneVersionAsync(source.Id, versionId, ct).ConfigureAwait(false);
        }

        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<FeeStructureVersionListItemDto>> PublishVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Only draft fee structures can be published.");
        }

        int typeCount = await _repo.CountActiveFeeHeadsForStructureAsync(versionId, ct).ConfigureAwait(false);
        if (typeCount == 0)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Add at least one fee type before publishing.");
        }

        await _repo.ArchivePublishedStructuresAsync(versionId, ct).ConfigureAwait(false);
        version.Status = FeeStructureVersionStatus.Published;
        version.PublishedOn = DateTime.UtcNow;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<FeeStructureVersionListItemDto>> ActivateVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Published)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Only published fee structures can be activated.");
        }

        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        if (!_scope.ActiveAcademicYearId.HasValue)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Active academic year is required to activate fee structure.");
        }

        await _repo.ArchiveActiveStructuresAsync(versionId, ct).ConfigureAwait(false);
        version.Status = FeeStructureVersionStatus.Active;
        version.ActivatedOn = DateTime.UtcNow;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        await _installmentService
            .RegenerateForVersionAsync(versionId, _scope.ActiveAcademicYearId.Value, ct)
            .ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<FeeStructureVersionListItemDto>> CreateNewVersionFromAsync(
        Guid sourceVersionId,
        CancellationToken ct = default)
    {
        FeeStructureEntity? source = await _repo.GetVersionByIdAsync(sourceVersionId, ct).ConfigureAwait(false);
        if (source is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Source fee structure version not found.");
        }

        if (source.Status is not (FeeStructureVersionStatus.Published or FeeStructureVersionStatus.Active or FeeStructureVersionStatus.Archived))
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Create a new version only from a published or active structure.");
        }

        return await CreateVersionAsync(new CreateFeeStructureVersionRequestDto(
            source.EffectiveDate,
            source.Id), ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<bool>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<bool>.Failure("Only draft fee structures can be deleted.");
        }

        if (await _repo.VersionHasPaymentsAsync(versionId, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("This fee structure has payment records and cannot be deleted.");
        }

        if (await _repo.VersionHasAssignedStudentsAsync(versionId, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("This fee structure is assigned to students and cannot be deleted.");
        }

        await _repo.SoftDeleteVersionAsync(versionId, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<FeeHeadDto>> CreateFeeHeadAsync(CreateFeeHeadRequestDto request, CancellationToken ct = default)
    {
        Result<FeeStructureEntity> versionResult = await RequireEditableVersionAsync(request.FeeStructureId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<FeeHeadDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeHeadDto>.Failure("Fee type name is required.");
        }

        var entity = new FeeHeadEntity
        {
            FeeStructureId = request.FeeStructureId,
            Name = request.Name.Trim(),
            Category = request.Category,
            CollectionType = request.CollectionType,
            IsMandatory = request.IsMandatory,
            IsRefundable = request.IsRefundable,
            StudentWiseDifferentAmount = request.StudentWiseDifferentAmount
        };
        Guid id = await _repo.CreateFeeHeadAsync(entity, ct).ConfigureAwait(false);
        FeeHeadEntity? saved = await _repo.GetFeeHeadByIdAsync(id, ct).ConfigureAwait(false);
        return saved is null
            ? Result<FeeHeadDto>.Failure("Failed to create fee type.")
            : Result<FeeHeadDto>.Success(MapFeeHead(saved, false));
    }

    public async Task<Result<FeeHeadDto>> UpdateFeeHeadAsync(Guid id, UpdateFeeHeadRequestDto request, CancellationToken ct = default)
    {
        FeeHeadEntity? existing = await _repo.GetFeeHeadByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<FeeHeadDto>.Failure("Fee type not found.");
        }

        Result<FeeStructureEntity> versionResult = await RequireEditableVersionAsync(existing.FeeStructureId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<FeeHeadDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeHeadDto>.Failure("Fee type name is required.");
        }

        existing.Name = request.Name.Trim();
        existing.Category = request.Category;
        existing.CollectionType = request.CollectionType;
        existing.IsMandatory = request.IsMandatory;
        existing.IsRefundable = request.IsRefundable;
        existing.StudentWiseDifferentAmount = request.StudentWiseDifferentAmount;
        await _repo.UpdateFeeHeadAsync(existing, ct).ConfigureAwait(false);
        bool hasPayments = await _repo.FeeHeadHasPaymentsAsync(id, ct).ConfigureAwait(false);
        return Result<FeeHeadDto>.Success(MapFeeHead(existing, hasPayments));
    }

    public async Task<Result<bool>> DeleteFeeHeadAsync(Guid id, CancellationToken ct = default)
    {
        FeeHeadEntity? existing = await _repo.GetFeeHeadByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<bool>.Failure("Fee type not found.");
        }

        Result<FeeStructureEntity> versionResult = await RequireEditableVersionAsync(existing.FeeStructureId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<bool>.Failure(versionResult.Error!);
        }

        if (await _repo.FeeHeadHasPaymentsAsync(id, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("This fee type has payment records and cannot be deleted.");
        }

        await _repo.SoftDeleteFeeHeadAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Guid?> ResolveActiveFeeStructureIdAsync(CancellationToken ct = default)
    {
        FeeStructureEntity? active = await _repo.GetActiveFeeStructureAsync(ct).ConfigureAwait(false);
        return active?.Id;
    }

    private async Task<Result<FeeStructureVersionListItemDto>> GetVersionListItemByIdAsync(Guid versionId, CancellationToken ct)
    {
        IList<FeeStructureVersionListRow> rows = await _repo.GetVersionsAsync(null, ct).ConfigureAwait(false);
        FeeStructureVersionListRow? row = rows.FirstOrDefault(r => r.Id == versionId);
        if (row is null)
        {
            FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
            if (version is null)
            {
                return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
            }

            bool hasPayments = await _repo.VersionHasPaymentsAsync(versionId, ct).ConfigureAwait(false);
            int typeCount = await _repo.CountActiveFeeHeadsForStructureAsync(versionId, ct).ConfigureAwait(false);
            row = new FeeStructureVersionListRow
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                EffectiveDate = version.EffectiveDate,
                PublishedOn = version.PublishedOn,
                ActivatedOn = version.ActivatedOn,
                FeeHeadCount = typeCount,
                HasStudentPayments = hasPayments
            };
        }

        return Result<FeeStructureVersionListItemDto>.Success(MapVersionListItem(row));
    }

    private async Task<Result<FeeStructureEntity>> RequireEditableVersionAsync(Guid versionId, CancellationToken ct)
    {
        FeeStructureEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureEntity>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<FeeStructureEntity>.Failure("Only draft fee structures can be edited.");
        }

        return Result<FeeStructureEntity>.Success(version);
    }

    private static FeeStructureVersionStatus? ParseStatusFilter(string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return statusFilter.Trim().ToLowerInvariant() switch
        {
            "draft" => FeeStructureVersionStatus.Draft,
            "published" => FeeStructureVersionStatus.Published,
            "active" => FeeStructureVersionStatus.Active,
            "archived" => FeeStructureVersionStatus.Archived,
            _ => null
        };
    }

    private static bool IsVersionLocked(FeeStructureVersionStatus status) =>
        status is FeeStructureVersionStatus.Published or FeeStructureVersionStatus.Active or FeeStructureVersionStatus.Archived;

    private static FeeStructureVersionListItemDto MapVersionListItem(FeeStructureVersionListRow row) => new(
        row.Id,
        row.VersionNumber,
        row.Status,
        FeeLabelHelper.VersionStatusLabel(row.Status),
        row.EffectiveDate,
        row.PublishedOn,
        row.ActivatedOn,
        row.FeeHeadCount,
        row.HasStudentPayments,
        IsVersionLocked(row.Status));

    private static FeeHeadDto MapFeeHead(FeeHeadListRow row) => new(
        row.Id,
        row.FeeStructureId,
        row.Name,
        row.Category,
        FeeLabelHelper.CategoryLabel(row.Category),
        row.CollectionType,
        FeeLabelHelper.CollectionTypeLabel(row.CollectionType),
        row.IsMandatory,
        row.IsRefundable,
        row.StudentWiseDifferentAmount,
        row.IsActive,
        row.HasStudentPayments);

    private static FeeHeadDto MapFeeHead(FeeHeadEntity entity, bool hasStudentPayments) => new(
        entity.Id,
        entity.FeeStructureId,
        entity.Name,
        entity.Category,
        FeeLabelHelper.CategoryLabel(entity.Category),
        entity.CollectionType,
        FeeLabelHelper.CollectionTypeLabel(entity.CollectionType),
        entity.IsMandatory,
        entity.IsRefundable,
        entity.StudentWiseDifferentAmount,
        entity.IsActive,
        hasStudentPayments);
}
