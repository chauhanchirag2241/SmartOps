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
        Guid? academicYearId,
        string? statusFilter,
        CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        Guid? effectiveYearId = academicYearId ?? _scope.ActiveAcademicYearId;

        FeeStructureVersionStatus? status = ParseStatusFilter(statusFilter);
        IList<FeeStructureVersionListRow> rows = await _repo.GetVersionsAsync(effectiveYearId, status, ct).ConfigureAwait(false);
        IList<FeeStructureVersionListItemDto> dtos = rows.Select(MapVersionListItem).ToList();
        return Result<IList<FeeStructureVersionListItemDto>>.Success(dtos);
    }

    public async Task<Result<FeeStructureVersionDetailDto>> GetVersionDetailAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionDetailDto>.Failure("Fee structure version not found.");
        }

        IList<FeeTypeListRow> types = await _repo.GetFeeTypesAsync(versionId, ct).ConfigureAwait(false);
        string? yearTitle = await _repo.GetAcademicYearTitleAsync(version.AcademicYearId, ct).ConfigureAwait(false);
        bool hasPayments = await _repo.VersionHasPaymentsAsync(versionId, ct).ConfigureAwait(false);

        return Result<FeeStructureVersionDetailDto>.Success(new FeeStructureVersionDetailDto(
            version.Id,
            version.AcademicYearId,
            yearTitle ?? string.Empty,
            version.VersionNumber,
            version.Status,
            FeeLabelHelper.VersionStatusLabel(version.Status),
            version.EffectiveDate,
            version.PublishedOn,
            version.ActivatedOn,
            hasPayments,
            IsVersionLocked(version.Status),
            types.Select(MapFeeType).ToList()));
    }

    public async Task<Result<FeeStructureVersionListItemDto>> CreateVersionAsync(
        CreateFeeStructureVersionRequestDto request,
        CancellationToken ct = default)
    {
        if (request.AcademicYearId == Guid.Empty)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Academic year is required.");
        }

        int versionNumber = await _repo.GetNextVersionNumberAsync(request.AcademicYearId, ct).ConfigureAwait(false);
        var entity = new FeeStructureVersionEntity
        {
            AcademicYearId = request.AcademicYearId,
            VersionNumber = versionNumber,
            Status = FeeStructureVersionStatus.Draft,
            EffectiveDate = request.EffectiveDate
        };
        Guid versionId = await _repo.CreateVersionAsync(entity, ct).ConfigureAwait(false);

        if (request.CloneFromVersionId.HasValue && request.CloneFromVersionId.Value != Guid.Empty)
        {
            FeeStructureVersionEntity? source = await _repo.GetVersionByIdAsync(request.CloneFromVersionId.Value, ct).ConfigureAwait(false);
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
        FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Only draft fee structures can be published.");
        }

        int typeCount = await _repo.CountActiveFeeTypesForVersionAsync(versionId, ct).ConfigureAwait(false);
        if (typeCount == 0)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Add at least one fee type before publishing.");
        }

        await _repo.ArchivePublishedVersionsForYearAsync(version.AcademicYearId, versionId, ct).ConfigureAwait(false);
        version.Status = FeeStructureVersionStatus.Published;
        version.PublishedOn = DateTime.UtcNow;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<FeeStructureVersionListItemDto>> ActivateVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Published)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Only published fee structures can be activated.");
        }

        await _repo.ArchiveActiveVersionsForYearAsync(version.AcademicYearId, versionId, ct).ConfigureAwait(false);
        version.Status = FeeStructureVersionStatus.Active;
        version.ActivatedOn = DateTime.UtcNow;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        await _installmentService
            .RegenerateForVersionAsync(versionId, version.AcademicYearId, ct)
            .ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<FeeStructureVersionListItemDto>> CreateNewVersionFromAsync(
        Guid sourceVersionId,
        CancellationToken ct = default)
    {
        FeeStructureVersionEntity? source = await _repo.GetVersionByIdAsync(sourceVersionId, ct).ConfigureAwait(false);
        if (source is null)
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Source fee structure version not found.");
        }

        if (source.Status is not (FeeStructureVersionStatus.Published or FeeStructureVersionStatus.Active or FeeStructureVersionStatus.Archived))
        {
            return Result<FeeStructureVersionListItemDto>.Failure("Create a new version only from a published or active structure.");
        }

        return await CreateVersionAsync(new CreateFeeStructureVersionRequestDto(
            source.AcademicYearId,
            source.EffectiveDate,
            source.Id), ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
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

    public async Task<Result<FeeTypeDto>> CreateFeeTypeAsync(CreateFeeTypeRequestDto request, CancellationToken ct = default)
    {
        Result<FeeStructureVersionEntity> versionResult = await RequireEditableVersionAsync(request.FeeStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<FeeTypeDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeTypeDto>.Failure("Fee type name is required.");
        }

        var entity = new FeeTypeEntity
        {
            FeeStructureVersionId = request.FeeStructureVersionId,
            Name = request.Name.Trim(),
            Category = request.Category,
            CollectionType = request.CollectionType,
            IsMandatory = request.IsMandatory,
            IsRefundable = request.IsRefundable,
            StudentWiseDifferentAmount = request.StudentWiseDifferentAmount
        };
        Guid id = await _repo.CreateFeeTypeAsync(entity, ct).ConfigureAwait(false);
        FeeTypeEntity? saved = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        return saved is null
            ? Result<FeeTypeDto>.Failure("Failed to create fee type.")
            : Result<FeeTypeDto>.Success(MapFeeType(saved, false));
    }

    public async Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(Guid id, UpdateFeeTypeRequestDto request, CancellationToken ct = default)
    {
        FeeTypeEntity? existing = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<FeeTypeDto>.Failure("Fee type not found.");
        }

        Result<FeeStructureVersionEntity> versionResult = await RequireEditableVersionAsync(existing.FeeStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<FeeTypeDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeTypeDto>.Failure("Fee type name is required.");
        }

        existing.Name = request.Name.Trim();
        existing.Category = request.Category;
        existing.CollectionType = request.CollectionType;
        existing.IsMandatory = request.IsMandatory;
        existing.IsRefundable = request.IsRefundable;
        existing.StudentWiseDifferentAmount = request.StudentWiseDifferentAmount;
        await _repo.UpdateFeeTypeAsync(existing, ct).ConfigureAwait(false);
        bool hasPayments = await _repo.FeeTypeHasPaymentsAsync(id, ct).ConfigureAwait(false);
        return Result<FeeTypeDto>.Success(MapFeeType(existing, hasPayments));
    }

    public async Task<Result<bool>> DeleteFeeTypeAsync(Guid id, CancellationToken ct = default)
    {
        FeeTypeEntity? existing = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<bool>.Failure("Fee type not found.");
        }

        Result<FeeStructureVersionEntity> versionResult = await RequireEditableVersionAsync(existing.FeeStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<bool>.Failure(versionResult.Error!);
        }

        if (await _repo.FeeTypeHasPaymentsAsync(id, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("This fee type has payment records and cannot be deleted.");
        }

        await _repo.SoftDeleteFeeTypeAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<FeeSettingsDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        FeeSettingsEntity? settings = await _repo.GetSettingsAsync(ct).ConfigureAwait(false);
        if (settings is null)
        {
            return Result<FeeSettingsDto>.Success(new FeeSettingsDto(Guid.Empty, 0, null));
        }

        return Result<FeeSettingsDto>.Success(MapSettings(settings));
    }

    public async Task<Result<FeeSettingsDto>> UpsertSettingsAsync(UpsertFeeSettingsRequestDto request, CancellationToken ct = default)
    {
        var entity = new FeeSettingsEntity
        {
            LateFeePerDay = request.LateFeePerDay,
            DefaultAcademicYearId = request.DefaultAcademicYearId
        };
        await _repo.UpsertSettingsAsync(entity, ct).ConfigureAwait(false);
        FeeSettingsEntity? saved = await _repo.GetSettingsAsync(ct).ConfigureAwait(false);
        return saved is null
            ? Result<FeeSettingsDto>.Failure("Failed to save fee settings.")
            : Result<FeeSettingsDto>.Success(MapSettings(saved));
    }

    public async Task<Guid?> ResolveActiveVersionIdForYearAsync(Guid academicYearId, CancellationToken ct = default)
    {
        FeeStructureVersionEntity? active = await _repo.GetActiveVersionForYearAsync(academicYearId, ct).ConfigureAwait(false);
        return active?.Id;
    }

    private async Task<Result<FeeStructureVersionListItemDto>> GetVersionListItemByIdAsync(Guid versionId, CancellationToken ct)
    {
        IList<FeeStructureVersionListRow> rows = await _repo.GetVersionsAsync(null, null, ct).ConfigureAwait(false);
        FeeStructureVersionListRow? row = rows.FirstOrDefault(r => r.Id == versionId);
        if (row is null)
        {
            FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
            if (version is null)
            {
                return Result<FeeStructureVersionListItemDto>.Failure("Fee structure version not found.");
            }

            string? title = await _repo.GetAcademicYearTitleAsync(version.AcademicYearId, ct).ConfigureAwait(false);
            bool hasPayments = await _repo.VersionHasPaymentsAsync(versionId, ct).ConfigureAwait(false);
            int typeCount = await _repo.CountActiveFeeTypesForVersionAsync(versionId, ct).ConfigureAwait(false);
            row = new FeeStructureVersionListRow
            {
                Id = version.Id,
                AcademicYearId = version.AcademicYearId,
                AcademicYearTitle = title ?? string.Empty,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                EffectiveDate = version.EffectiveDate,
                PublishedOn = version.PublishedOn,
                ActivatedOn = version.ActivatedOn,
                FeeTypeCount = typeCount,
                HasStudentPayments = hasPayments
            };
        }

        return Result<FeeStructureVersionListItemDto>.Success(MapVersionListItem(row));
    }

    private async Task<Result<FeeStructureVersionEntity>> RequireEditableVersionAsync(Guid versionId, CancellationToken ct)
    {
        FeeStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<FeeStructureVersionEntity>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<FeeStructureVersionEntity>.Failure("Only draft fee structures can be edited.");
        }

        return Result<FeeStructureVersionEntity>.Success(version);
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
        row.AcademicYearId,
        row.AcademicYearTitle,
        row.VersionNumber,
        row.Status,
        FeeLabelHelper.VersionStatusLabel(row.Status),
        row.EffectiveDate,
        row.PublishedOn,
        row.ActivatedOn,
        row.FeeTypeCount,
        row.HasStudentPayments,
        IsVersionLocked(row.Status));

    private static FeeTypeDto MapFeeType(FeeTypeListRow row) => new(
        row.Id,
        row.FeeStructureVersionId,
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

    private static FeeTypeDto MapFeeType(FeeTypeEntity entity, bool hasStudentPayments) => new(
        entity.Id,
        entity.FeeStructureVersionId,
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

    private static FeeSettingsDto MapSettings(FeeSettingsEntity settings) => new(
        settings.Id,
        settings.LateFeePerDay,
        settings.DefaultAcademicYearId);
}
