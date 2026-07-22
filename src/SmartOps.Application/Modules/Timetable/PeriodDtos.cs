using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Application.Modules.Timetable;

public sealed class PeriodLineDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public int PeriodOrder { get; set; }
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
    public bool IsBreak { get; set; }
    /// <summary>Null = default (Mon–Sat unless day has override); 1–6 = day-specific schedule.</summary>
    public int? DayOfWeek { get; set; }
}

public sealed class CreatePeriodTemplateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<PeriodLineDto> Periods { get; set; } = [];
}

public sealed record CreatePeriodTemplateResponse(string Message, Guid TemplateId);

public sealed class PeriodTemplateDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PeriodLineDto> Periods { get; set; } = [];
}

public static class PeriodTemplateMappingExtensions
{
    public static PeriodTemplateEntity ToEntity(this CreatePeriodTemplateDto dto) => new()
    {
        Name = dto.Name.Trim(),
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
        IsActive = dto.IsActive,
    };

    public static List<PeriodEntity> ToPeriodEntities(this CreatePeriodTemplateDto dto, Guid templateId) =>
        (dto.Periods ?? [])
            .Select((p, index) => new PeriodEntity
            {
                Id = p.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
                TemplateId = templateId,
                Name = p.Name.Trim(),
                ShortName = p.ShortName.Trim(),
                PeriodOrder = p.PeriodOrder > 0 ? p.PeriodOrder : index + 1,
                StartTime = p.StartTime.Trim(),
                EndTime = p.EndTime.Trim(),
                IsBreak = p.IsBreak,
                DayOfWeek = NormalizeDayOfWeek(p.DayOfWeek),
            })
            .ToList();

    public static string? ValidatePeriodSchedules(IReadOnlyList<PeriodLineDto>? periods)
    {
        if (periods is null || periods.Count == 0)
            return "Add at least one period to the template.";

        var defaults = periods.Where(p => !p.DayOfWeek.HasValue).ToList();
        if (defaults.Count == 0)
            return "Default schedule needs at least one period (for days without an override).";

        foreach (var group in periods.GroupBy(p => p.DayOfWeek))
        {
            var day = group.Key;
            var label = day is null ? "Default" : DayLabel(day.Value);
            if (day is { } d && (d < 1 || d > 6))
                return $"Invalid day of week ({d}). Use Monday(1)–Saturday(6).";

            var ordered = group.OrderBy(p => p.PeriodOrder).ToList();
            if (ordered.Count == 0)
                return $"{label} schedule needs at least one period.";

            var orders = ordered.Select(p => p.PeriodOrder).ToList();
            if (orders.Distinct().Count() != orders.Count)
                return $"{label} schedule has duplicate period order values.";

            foreach (var p in ordered)
            {
                if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.ShortName))
                    return $"{label}: period name and short name are required.";
                if (string.IsNullOrWhiteSpace(p.StartTime) || string.IsNullOrWhiteSpace(p.EndTime))
                    return $"{label}: start and end time are required.";
            }
        }

        return null;
    }

    private static int? NormalizeDayOfWeek(int? day)
    {
        if (!day.HasValue) return null;
        if (day.Value < 1 || day.Value > 6)
            throw new InvalidOperationException("Day of week must be Monday(1)–Saturday(6).");
        return day.Value;
    }

    private static string DayLabel(int day) => day switch
    {
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        _ => $"Day {day}",
    };
}
