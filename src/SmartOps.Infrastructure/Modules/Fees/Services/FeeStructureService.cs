using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class FeeStructureService : IFeeStructureService
{
    private readonly IFeeStructureRepository _repo;

    public FeeStructureService(IFeeStructureRepository repo) => _repo = repo;

    public async Task<Result<IList<FeeTypeDto>>> GetFeeTypesAsync(CancellationToken ct = default)
    {
        IList<FeeTypeEntity> entities = await _repo.GetFeeTypesAsync(ct).ConfigureAwait(false);
        IList<FeeTypeDto> dtos = entities.Select(MapFeeType).ToList();
        return Result<IList<FeeTypeDto>>.Success(dtos);
    }

    public async Task<Result<FeeStructureStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        FeeSettingsEntity? settings = await _repo.GetSettingsAsync(ct).ConfigureAwait(false);
        int feeTypeCount = await _repo.CountActiveFeeTypesAsync(ct).ConfigureAwait(false);
        int classesConfigured = await _repo.CountClassesWithAmountsAsync(settings?.DefaultAcademicYearId, ct).ConfigureAwait(false);
        FeePaymentCycle cycle = settings?.PaymentCycle ?? FeePaymentCycle.Quarterly;
        decimal lateFee = settings?.LateFeePerDay ?? 0;

        return Result<FeeStructureStatsDto>.Success(new FeeStructureStatsDto(
            feeTypeCount,
            classesConfigured,
            FeeLabelHelper.PaymentCycleLabel(cycle),
            lateFee));
    }

    public async Task<Result<FeeSettingsDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        FeeSettingsEntity? settings = await _repo.GetSettingsAsync(ct).ConfigureAwait(false);
        if (settings is null)
        {
            return Result<FeeSettingsDto>.Success(new FeeSettingsDto(
                Guid.Empty,
                FeePaymentCycle.Quarterly,
                FeeLabelHelper.PaymentCycleLabel(FeePaymentCycle.Quarterly),
                0,
                null));
        }

        return Result<FeeSettingsDto>.Success(MapSettings(settings));
    }

    public async Task<Result<FeeSettingsDto>> UpsertSettingsAsync(UpsertFeeSettingsRequestDto request, CancellationToken ct = default)
    {
        var entity = new FeeSettingsEntity
        {
            PaymentCycle = request.PaymentCycle,
            LateFeePerDay = request.LateFeePerDay,
            DefaultAcademicYearId = request.DefaultAcademicYearId
        };
        await _repo.UpsertSettingsAsync(entity, ct).ConfigureAwait(false);
        FeeSettingsEntity? saved = await _repo.GetSettingsAsync(ct).ConfigureAwait(false);
        return saved is null
            ? Result<FeeSettingsDto>.Failure("Failed to save fee settings.")
            : Result<FeeSettingsDto>.Success(MapSettings(saved));
    }

    public async Task<Result<FeeTypeDto>> CreateFeeTypeAsync(CreateFeeTypeRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeTypeDto>.Failure("Fee type name is required.");
        }

        var entity = new FeeTypeEntity
        {
            Name = request.Name.Trim(),
            Category = request.Category,
            Frequency = request.Frequency,
            IsMandatory = request.IsMandatory,
            IsRefundable = request.IsRefundable
        };
        Guid id = await _repo.CreateFeeTypeAsync(entity, ct).ConfigureAwait(false);
        FeeTypeEntity? saved = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        return saved is null
            ? Result<FeeTypeDto>.Failure("Failed to create fee type.")
            : Result<FeeTypeDto>.Success(MapFeeType(saved));
    }

    public async Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(Guid id, UpdateFeeTypeRequestDto request, CancellationToken ct = default)
    {
        FeeTypeEntity? existing = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result<FeeTypeDto>.Failure("Fee type not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<FeeTypeDto>.Failure("Fee type name is required.");
        }

        existing.Name = request.Name.Trim();
        existing.Category = request.Category;
        existing.Frequency = request.Frequency;
        existing.IsMandatory = request.IsMandatory;
        existing.IsRefundable = request.IsRefundable;
        await _repo.UpdateFeeTypeAsync(existing, ct).ConfigureAwait(false);
        return Result<FeeTypeDto>.Success(MapFeeType(existing));
    }

    public async Task<Result<bool>> DeleteFeeTypeAsync(Guid id, CancellationToken ct = default)
    {
        FeeTypeEntity? existing = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result<bool>.Failure("Fee type not found.");
        }

        await _repo.SoftDeleteFeeTypeAsync(id, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<FeeTypeDto>> SetActiveAsync(Guid id, ToggleFeeTypeActiveRequestDto request, CancellationToken ct = default)
    {
        FeeTypeEntity? existing = await _repo.GetFeeTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result<FeeTypeDto>.Failure("Fee type not found.");
        }

        await _repo.SetFeeTypeActiveAsync(id, request.IsActive, ct).ConfigureAwait(false);
        existing.IsActive = request.IsActive;
        return Result<FeeTypeDto>.Success(MapFeeType(existing));
    }

    private static FeeTypeDto MapFeeType(FeeTypeEntity e) => new(
        e.Id,
        e.Name,
        e.Category,
        FeeLabelHelper.CategoryLabel(e.Category),
        e.Frequency,
        FeeLabelHelper.FrequencyLabel(e.Frequency),
        e.IsMandatory,
        e.IsRefundable,
        e.IsActive);

    private static FeeSettingsDto MapSettings(FeeSettingsEntity s) => new(
        s.Id,
        s.PaymentCycle,
        FeeLabelHelper.PaymentCycleLabel(s.PaymentCycle),
        s.LateFeePerDay,
        s.DefaultAcademicYearId);
}
