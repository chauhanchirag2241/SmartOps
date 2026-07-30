using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Application.Modules.FeeMaster;

public sealed class UpdateFeeMasterBasicDto
{
    public string FeeName { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? DefaultDueDate { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<Guid>? ClassGroupIds { get; set; }
}

public sealed class FeeHeadPeriodAmountDto
{
    public Guid ClassGroupId { get; set; }
    public Guid AcademicPeriodId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class CreateFeeHeadDto
{
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsEditable { get; set; }
    public decimal? Amount { get; set; }
    public IReadOnlyList<int>? ApplicableMonths { get; set; }
    public IReadOnlyList<FeeHeadPeriodAmountDto>? PeriodAmounts { get; set; }
}

public sealed record CreateFeeHeadResponse(string Message, Guid FeeHeadId);

public static class FeeHeadMappingExtensions
{
    public static string? SerializeMonths(IReadOnlyList<int>? months)
    {
        if (months is null || months.Count == 0)
        {
            return null;
        }

        var normalized = months
            .Where(m => m is >= 1 and <= 12)
            .Distinct()
            .OrderBy(m => m)
            .ToArray();
        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }

    public static IReadOnlyList<int> ParseMonths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var n) ? n : 0)
            .Where(n => n is >= 1 and <= 12)
            .Distinct()
            .OrderBy(n => n)
            .ToArray();
    }

    public static FeeHeadEntity ToEntity(this CreateFeeHeadDto dto, Guid feeMasterId) => new()
    {
        FeeMasterId = feeMasterId,
        FeeHeadName = (dto.FeeHeadName ?? string.Empty).Trim(),
        IsMandatory = dto.IsMandatory,
        IsEditable = dto.IsEditable,
        Amount = dto.Amount,
        ApplicableMonths = SerializeMonths(dto.ApplicableMonths),
    };

    public static IReadOnlyList<FeeHeadPeriodAmountEntity> ToPeriodEntities(this CreateFeeHeadDto dto) =>
        (dto.PeriodAmounts ?? [])
            .Where(p => p.ClassGroupId != Guid.Empty && p.AcademicPeriodId != Guid.Empty)
            .Select(p => new FeeHeadPeriodAmountEntity
            {
                ClassGroupId = p.ClassGroupId,
                AcademicPeriodId = p.AcademicPeriodId,
                Amount = p.Amount,
            })
            .ToList();
}
