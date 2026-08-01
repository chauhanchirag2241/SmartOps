using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Infrastructure.Modules.Salary.Services;

public sealed class SalaryStructureService : ISalaryStructureService
{
    private readonly ISalaryStructureRepository _repo;

    public SalaryStructureService(ISalaryStructureRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<IList<SalaryStructureVersionListItemDto>>> GetVersionsAsync(
        string? statusFilter,
        CancellationToken ct = default)
    {
        SalaryStructureVersionStatus? status = ParseStatusFilter(statusFilter);
        IList<SalaryStructureVersionListRow> rows = await _repo.GetVersionsAsync(status, ct).ConfigureAwait(false);
        IList<SalaryStructureVersionListItemDto> dtos = rows.Select(MapVersionListItem).ToList();
        return Result<IList<SalaryStructureVersionListItemDto>>.Success(dtos);
    }

    public async Task<Result<SalaryStructureVersionDetailDto>> GetVersionDetailAsync(Guid versionId, CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<SalaryStructureVersionDetailDto>.Failure("Salary structure version not found.");
        }

        IList<SalaryVersionComponentListRow> components = await _repo.GetComponentsAsync(versionId, ct).ConfigureAwait(false);
        bool hasEmployees = await _repo.VersionHasAssignedEmployeesAsync(versionId, ct).ConfigureAwait(false);

        return Result<SalaryStructureVersionDetailDto>.Success(new SalaryStructureVersionDetailDto(
            version.Id,
            version.VersionNumber,
            version.Status,
            SalaryLabelHelper.VersionStatusLabel(version.Status),
            version.EffectiveDate,
            hasEmployees,
            IsVersionLocked(version),
            components.Select(MapComponent).ToList()));
    }

    public async Task<Result<SalaryStructureVersionListItemDto>> CreateVersionAsync(
        CreateSalaryStructureVersionRequestDto request,
        CancellationToken ct = default)
    {
        int versionNumber = await _repo.GetNextVersionNumberAsync(ct).ConfigureAwait(false);
        var entity = new SalaryStructureVersionEntity
        {
            VersionNumber = versionNumber,
            Status = SalaryStructureVersionStatus.Draft,
            EffectiveDate = request.EffectiveDate
        };
        Guid versionId = await _repo.CreateVersionAsync(entity, ct).ConfigureAwait(false);

        if (request.CloneFromVersionId.HasValue && request.CloneFromVersionId.Value != Guid.Empty)
        {
            SalaryStructureVersionEntity? source = await _repo.GetVersionByIdAsync(request.CloneFromVersionId.Value, ct).ConfigureAwait(false);
            if (source is null)
            {
                return Result<SalaryStructureVersionListItemDto>.Failure("Source salary structure version not found.");
            }

            await _repo.CloneVersionAsync(source.Id, versionId, ct).ConfigureAwait(false);
        }

        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<SalaryStructureVersionListItemDto>> UpdateVersionBasicAsync(
        Guid versionId,
        UpdateSalaryStructureVersionBasicRequestDto request,
        CancellationToken ct = default)
    {
        Result<SalaryStructureVersionEntity> versionResult = await RequireEditableVersionAsync(versionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure(versionResult.Error!);
        }

        SalaryStructureVersionEntity version = versionResult.Value!;
        string? dateError = ValidateEffectiveDate(request.EffectiveDate, version.EffectiveDate);
        if (dateError is not null)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure(dateError);
        }

        version.EffectiveDate = request.EffectiveDate;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<SalaryStructureVersionListItemDto>> PublishVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Salary structure version not found.");
        }

        if (version.Status != SalaryStructureVersionStatus.Draft)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Only draft salary structures can be published.");
        }

        int componentCount = await _repo.CountActiveComponentsForVersionAsync(versionId, ct).ConfigureAwait(false);
        if (componentCount == 0)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Add at least one salary component before publishing.");
        }

        await _repo.ArchivePublishedVersionsAsync(versionId, ct).ConfigureAwait(false);
        version.Status = SalaryStructureVersionStatus.Published;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<SalaryStructureVersionListItemDto>> ActivateVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Salary structure version not found.");
        }

        if (version.Status != SalaryStructureVersionStatus.Published)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Only published salary structures can be activated.");
        }

        await _repo.ArchiveActiveVersionsAsync(versionId, ct).ConfigureAwait(false);
        version.Status = SalaryStructureVersionStatus.Active;
        await _repo.UpdateVersionAsync(version, ct).ConfigureAwait(false);
        return await GetVersionListItemByIdAsync(versionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<SalaryStructureVersionListItemDto>> CreateNewVersionFromAsync(
        Guid sourceVersionId,
        CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? source = await _repo.GetVersionByIdAsync(sourceVersionId, ct).ConfigureAwait(false);
        if (source is null)
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Source salary structure version not found.");
        }

        if (source.Status is not (SalaryStructureVersionStatus.Published or SalaryStructureVersionStatus.Active or SalaryStructureVersionStatus.Archived))
        {
            return Result<SalaryStructureVersionListItemDto>.Failure("Create a new version only from a published or active structure.");
        }

        return await CreateVersionAsync(new CreateSalaryStructureVersionRequestDto(
            source.EffectiveDate,
            source.Id), ct).ConfigureAwait(false);
    }

    public async Task<Result<bool>> DeleteVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<bool>.Failure("Salary structure version not found.");
        }

        if (version.Status != SalaryStructureVersionStatus.Draft)
        {
            return Result<bool>.Failure("Only draft salary structures can be deleted.");
        }

        if (await _repo.VersionHasAssignedEmployeesAsync(versionId, ct).ConfigureAwait(false))
        {
            return Result<bool>.Failure("This salary structure is assigned to employees and cannot be deleted.");
        }

        await _repo.SoftDeleteVersionAsync(versionId, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<SalaryVersionComponentDto>> CreateComponentAsync(
        CreateSalaryVersionComponentRequestDto request,
        CancellationToken ct = default)
    {
        Result<SalaryStructureVersionEntity> versionResult = await RequireEditableVersionAsync(request.SalaryStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<SalaryVersionComponentDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<SalaryVersionComponentDto>.Failure("Component name is required.");
        }

        if (request.Value <= 0)
        {
            return Result<SalaryVersionComponentDto>.Failure("Component value must be greater than zero.");
        }

        var entity = new SalaryVersionComponentEntity
        {
            SalaryStructureVersionId = request.SalaryStructureVersionId,
            Name = request.Name.Trim(),
            ShortCode = string.IsNullOrWhiteSpace(request.ShortCode) ? null : request.ShortCode.Trim().ToUpperInvariant(),
            ComponentType = request.ComponentType,
            CalculationType = request.CalculationType,
            Value = request.Value,
            IsTaxable = request.IsTaxable
        };
        Guid id = await _repo.CreateComponentAsync(entity, ct).ConfigureAwait(false);
        SalaryVersionComponentEntity? saved = await _repo.GetComponentByIdAsync(id, ct).ConfigureAwait(false);
        return saved is null
            ? Result<SalaryVersionComponentDto>.Failure("Failed to create salary component.")
            : Result<SalaryVersionComponentDto>.Success(MapComponent(saved));
    }

    public async Task<Result<SalaryVersionComponentDto>> UpdateComponentAsync(
        Guid id,
        UpdateSalaryVersionComponentRequestDto request,
        CancellationToken ct = default)
    {
        SalaryVersionComponentEntity? existing = await _repo.GetComponentByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<SalaryVersionComponentDto>.Failure("Salary component not found.");
        }

        Result<SalaryStructureVersionEntity> versionResult = await RequireEditableVersionAsync(existing.SalaryStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<SalaryVersionComponentDto>.Failure(versionResult.Error!);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<SalaryVersionComponentDto>.Failure("Component name is required.");
        }

        if (request.Value <= 0)
        {
            return Result<SalaryVersionComponentDto>.Failure("Component value must be greater than zero.");
        }

        existing.Name = request.Name.Trim();
        existing.ShortCode = string.IsNullOrWhiteSpace(request.ShortCode) ? null : request.ShortCode.Trim().ToUpperInvariant();
        existing.ComponentType = request.ComponentType;
        existing.CalculationType = request.CalculationType;
        existing.Value = request.Value;
        existing.IsTaxable = request.IsTaxable;
        await _repo.UpdateComponentAsync(existing, ct).ConfigureAwait(false);
        return Result<SalaryVersionComponentDto>.Success(MapComponent(existing));
    }

    public async Task<Result<bool>> DeleteComponentAsync(Guid id, CancellationToken ct = default)
    {
        SalaryVersionComponentEntity? existing = await _repo.GetComponentByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<bool>.Failure("Salary component not found.");
        }

        Result<SalaryStructureVersionEntity> versionResult = await RequireEditableVersionAsync(existing.SalaryStructureVersionId, ct).ConfigureAwait(false);
        if (!versionResult.IsSuccess)
        {
            return Result<bool>.Failure(versionResult.Error!);
        }

        await _repo.SoftDeleteComponentAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Guid?> ResolveActiveVersionIdAsync(CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? active = await _repo.GetActiveVersionAsync(ct).ConfigureAwait(false);
        return active?.Id;
    }

    private async Task<Result<SalaryStructureVersionListItemDto>> GetVersionListItemByIdAsync(Guid versionId, CancellationToken ct)
    {
        IList<SalaryStructureVersionListRow> rows = await _repo.GetVersionsAsync(null, ct).ConfigureAwait(false);
        SalaryStructureVersionListRow? row = rows.FirstOrDefault(r => r.Id == versionId);
        if (row is null)
        {
            SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
            if (version is null)
            {
                return Result<SalaryStructureVersionListItemDto>.Failure("Salary structure version not found.");
            }

            bool hasEmployees = await _repo.VersionHasAssignedEmployeesAsync(versionId, ct).ConfigureAwait(false);
            int componentCount = await _repo.CountActiveComponentsForVersionAsync(versionId, ct).ConfigureAwait(false);
            row = new SalaryStructureVersionListRow
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                EffectiveDate = version.EffectiveDate,
                ComponentCount = componentCount,
                HasAssignedEmployees = hasEmployees
            };
        }

        return Result<SalaryStructureVersionListItemDto>.Success(MapVersionListItem(row));
    }

    private async Task<Result<SalaryStructureVersionEntity>> RequireEditableVersionAsync(Guid versionId, CancellationToken ct)
    {
        SalaryStructureVersionEntity? version = await _repo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<SalaryStructureVersionEntity>.Failure("Salary structure version not found.");
        }

        if (IsEffectiveDateStarted(version.EffectiveDate))
        {
            return Result<SalaryStructureVersionEntity>.Failure(
                "Salary structure cannot be changed after the effective date has started.");
        }

        if (version.Status == SalaryStructureVersionStatus.Archived)
        {
            return Result<SalaryStructureVersionEntity>.Failure("Archived salary structures cannot be edited.");
        }

        return Result<SalaryStructureVersionEntity>.Success(version);
    }

    private static string? ValidateEffectiveDate(DateOnly? effectiveDate, DateOnly? existingEffectiveDate)
    {
        if (!effectiveDate.HasValue)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (effectiveDate.Value < today && effectiveDate != existingEffectiveDate)
        {
            return "Effective date cannot be in the past.";
        }

        return null;
    }

    private static SalaryStructureVersionStatus? ParseStatusFilter(string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return statusFilter.Trim().ToLowerInvariant() switch
        {
            "draft" => SalaryStructureVersionStatus.Draft,
            "published" => SalaryStructureVersionStatus.Published,
            "active" => SalaryStructureVersionStatus.Active,
            "archived" => SalaryStructureVersionStatus.Archived,
            _ => null
        };
    }

    /// <summary>Locked once effective date has started (same idea as fee published-on lock).</summary>
    private static bool IsVersionLocked(SalaryStructureVersionEntity version) =>
        IsEffectiveDateStarted(version.EffectiveDate) || version.Status == SalaryStructureVersionStatus.Archived;

    private static bool IsVersionLocked(SalaryStructureVersionListRow row) =>
        IsEffectiveDateStarted(row.EffectiveDate) || row.Status == SalaryStructureVersionStatus.Archived;

    private static bool IsEffectiveDateStarted(DateOnly? effectiveDate)
    {
        if (!effectiveDate.HasValue)
        {
            return false;
        }

        return effectiveDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static SalaryStructureVersionListItemDto MapVersionListItem(SalaryStructureVersionListRow row) => new(
        row.Id,
        row.VersionNumber,
        row.Status,
        SalaryLabelHelper.VersionStatusLabel(row.Status),
        row.EffectiveDate,
        row.ComponentCount,
        row.HasAssignedEmployees,
        IsVersionLocked(row));

    private static SalaryVersionComponentDto MapComponent(SalaryVersionComponentListRow row) => new(
        row.Id,
        row.SalaryStructureVersionId,
        row.Name,
        row.ShortCode,
        row.ComponentType,
        SalaryLabelHelper.ComponentTypeLabel(row.ComponentType),
        row.CalculationType,
        SalaryLabelHelper.CalculationTypeLabel(row.CalculationType),
        row.Value,
        row.IsTaxable,
        row.IsActive);

    private static SalaryVersionComponentDto MapComponent(SalaryVersionComponentEntity entity) => new(
        entity.Id,
        entity.SalaryStructureVersionId,
        entity.Name,
        entity.ShortCode,
        entity.ComponentType,
        SalaryLabelHelper.ComponentTypeLabel(entity.ComponentType),
        entity.CalculationType,
        SalaryLabelHelper.CalculationTypeLabel(entity.CalculationType),
        entity.Value,
        entity.IsTaxable,
        entity.IsActive);
}
