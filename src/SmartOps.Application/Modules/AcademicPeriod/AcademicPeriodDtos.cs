using SmartOps.Domain.Modules.AcademicPeriod;

namespace SmartOps.Application.Modules.AcademicPeriod;

public sealed class ClassAcademicPeriodDto
{
    public Guid Id { get; init; }
    public int PeriodIndex { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class ClassAcademicPeriodSetupDto
{
    public Guid ClassId { get; init; }
    public IReadOnlyList<ClassAcademicPeriodDto> Periods { get; init; } = [];
}

public sealed class SaveClassAcademicPeriodsRequest
{
    public IReadOnlyList<SaveClassAcademicPeriodItem> Periods { get; init; } = [];
}

public sealed class SaveClassAcademicPeriodItem
{
    public Guid? Id { get; init; }
    public int PeriodIndex { get; init; }
    public string Name { get; init; } = string.Empty;
}

public static class AcademicPeriodMapping
{
    public static ClassAcademicPeriodDto ToDto(this ClassAcademicPeriodEntity entity) =>
        new()
        {
            Id = entity.Id,
            PeriodIndex = entity.PeriodIndex,
            Name = entity.Name,
        };

    public static ClassAcademicPeriodSetupDto ToSetupDto(
        Guid classId,
        IEnumerable<ClassAcademicPeriodEntity> periods) =>
        new()
        {
            ClassId = classId,
            Periods = periods.Select(p => p.ToDto()).ToList(),
        };
}

public static class AcademicPeriodValidation
{
    public static string? Validate(IReadOnlyList<SaveClassAcademicPeriodItem>? periods)
    {
        if (periods is null || periods.Count == 0)
        {
            return "At least one academic period is required.";
        }

        if (periods.Select(p => p.PeriodIndex).Distinct().Count() != periods.Count
            || periods.Any(p => p.PeriodIndex <= 0))
        {
            return "Period order must contain unique positive numbers.";
        }

        int[] expected = Enumerable.Range(1, periods.Count).ToArray();
        if (!periods.Select(p => p.PeriodIndex).OrderBy(i => i).SequenceEqual(expected))
        {
            return "Period order must be sequential starting from 1.";
        }

        if (periods.Any(p => string.IsNullOrWhiteSpace(p.Name)))
        {
            return "Every period name is required.";
        }

        if (periods.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != periods.Count)
        {
            return "Period names must be unique for the class.";
        }

        return null;
    }
}
