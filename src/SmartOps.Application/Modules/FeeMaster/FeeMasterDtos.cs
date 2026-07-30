using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Application.Modules.FeeMaster;

public sealed class CreateFeeMasterDto
{
    public string FeeName { get; set; } = string.Empty;
    public string FeeType { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? DefaultDueDate { get; set; }
    public string ApplicableTo { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<Guid>? ClassGroupIds { get; set; }
}

public static class FeeMasterMappingExtensions
{
    public static readonly HashSet<string> AllowedFeeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "OneTime",
        "Monthly",
        "PeriodWise",
    };

    public static readonly HashSet<string> AllowedApplicableTo = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClassWise",
        "StudentWise",
    };

    public static FeeMasterEntity ToEntity(this CreateFeeMasterDto dto) => new()
    {
        FeeName = (dto.FeeName ?? string.Empty).Trim(),
        FeeType = NormalizeToken(dto.FeeType),
        PublishedOn = dto.PublishedOn,
        DefaultDueDate = dto.DefaultDueDate,
        ApplicableTo = NormalizeToken(dto.ApplicableTo),
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
    };

    private static string NormalizeToken(string? value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (string.Equals(trimmed, "OneTime", StringComparison.OrdinalIgnoreCase)) return "OneTime";
        if (string.Equals(trimmed, "Monthly", StringComparison.OrdinalIgnoreCase)) return "Monthly";
        if (string.Equals(trimmed, "PeriodWise", StringComparison.OrdinalIgnoreCase)) return "PeriodWise";
        if (string.Equals(trimmed, "ClassWise", StringComparison.OrdinalIgnoreCase)) return "ClassWise";
        if (string.Equals(trimmed, "StudentWise", StringComparison.OrdinalIgnoreCase)) return "StudentWise";
        return trimmed;
    }
}

public sealed record CreateFeeMasterResponse(string Message, Guid FeeId);
