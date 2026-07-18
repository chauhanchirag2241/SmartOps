using SmartOps.Domain.Modules.AcademicPeriod;

namespace SmartOps.Application.Modules.AcademicPeriod;

public sealed record AcademicPeriodClassSummaryDto(
    Guid ClassId,
    string ClassName,
    Guid AcademicYearId,
    int PeriodCount,
    AcademicPeriodType? PeriodType);

public sealed record ClassAcademicPeriodDto(
    Guid Id,
    int PeriodIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record ClassAcademicPeriodSetupDto(
    Guid ClassId,
    Guid AcademicYearId,
    AcademicPeriodType? PeriodType,
    IReadOnlyList<ClassAcademicPeriodDto> Periods);

public sealed record SaveClassAcademicPeriodsRequest(
    Guid AcademicYearId,
    AcademicPeriodType PeriodType,
    IReadOnlyList<SaveClassAcademicPeriodItem> Periods);

public sealed record SaveClassAcademicPeriodItem(
    int PeriodIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public static class AcademicPeriodMapping
{
    public static ClassAcademicPeriodDto ToDto(this ClassAcademicPeriodEntity entity) =>
        new(entity.Id, entity.PeriodIndex, entity.Name, entity.StartDate, entity.EndDate);

    public static AcademicPeriodClassSummaryDto ToDto(this AcademicPeriodClassSummary row) =>
        new(row.ClassId, row.ClassName, row.AcademicYearId, row.PeriodCount, row.PeriodType);
}

public static class AcademicPeriodValidation
{
    public static string? Validate(
        DateOnly yearStart,
        DateOnly yearEnd,
        AcademicPeriodType periodType,
        IReadOnlyList<SaveClassAcademicPeriodItem>? periods)
    {
        if (!Enum.IsDefined(periodType))
        {
            return "Select a valid period type.";
        }

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

        foreach (SaveClassAcademicPeriodItem period in periods)
        {
            if (period.EndDate < period.StartDate)
            {
                return $"Period '{period.Name.Trim()}': end date cannot be earlier than start date.";
            }

            if (period.StartDate < yearStart || period.EndDate > yearEnd)
            {
                return $"Period '{period.Name.Trim()}': dates must fall within the academic year range.";
            }
        }

        SaveClassAcademicPeriodItem[] ordered = periods.OrderBy(p => p.StartDate).ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].StartDate <= ordered[i - 1].EndDate)
            {
                return $"Periods '{ordered[i - 1].Name.Trim()}' and '{ordered[i].Name.Trim()}' overlap.";
            }
        }

        return null;
    }
}
