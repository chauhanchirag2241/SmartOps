using SmartOps.Domain.Modules.AcademicPeriod;

namespace SmartOps.Application.Modules.AcademicPeriod;

public sealed record AcademicPeriodClassSummaryDto(
    Guid ClassId,
    string ClassName,
    int PeriodCount);

public sealed record ClassAcademicPeriodDto(
    Guid Id,
    int PeriodIndex,
    string Name);

public sealed record ClassAcademicPeriodSetupDto(
    Guid ClassId,
    IReadOnlyList<ClassAcademicPeriodDto> Periods);

public sealed record SaveClassAcademicPeriodsRequest(
    IReadOnlyList<SaveClassAcademicPeriodItem> Periods);

public sealed record SaveClassAcademicPeriodItem(
    int PeriodIndex,
    string Name);

public static class AcademicPeriodMapping
{
    public static ClassAcademicPeriodDto ToDto(this ClassAcademicPeriodEntity entity) =>
        new(entity.Id, entity.PeriodIndex, entity.Name);

    public static AcademicPeriodClassSummaryDto ToDto(this AcademicPeriodClassSummary row) =>
        new(row.ClassId, row.ClassName, row.PeriodCount);
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
