using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface ISalaryStructureService
{
    Task<Result<IList<SalaryStructureVersionListItemDto>>> GetVersionsAsync(
        string? statusFilter,
        CancellationToken ct = default);

    Task<Result<SalaryStructureVersionDetailDto>> GetVersionDetailAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<SalaryStructureVersionListItemDto>> CreateVersionAsync(
        CreateSalaryStructureVersionRequestDto request,
        CancellationToken ct = default);

    Task<Result<SalaryStructureVersionListItemDto>> PublishVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<SalaryStructureVersionListItemDto>> ActivateVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<SalaryStructureVersionListItemDto>> CreateNewVersionFromAsync(
        Guid sourceVersionId,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteVersionAsync(Guid versionId, CancellationToken ct = default);

    Task<Result<SalaryVersionComponentDto>> CreateComponentAsync(
        CreateSalaryVersionComponentRequestDto request,
        CancellationToken ct = default);

    Task<Result<SalaryVersionComponentDto>> UpdateComponentAsync(
        Guid id,
        UpdateSalaryVersionComponentRequestDto request,
        CancellationToken ct = default);

    Task<Result<bool>> DeleteComponentAsync(Guid id, CancellationToken ct = default);

    Task<Guid?> ResolveActiveVersionIdAsync(CancellationToken ct = default);
}
