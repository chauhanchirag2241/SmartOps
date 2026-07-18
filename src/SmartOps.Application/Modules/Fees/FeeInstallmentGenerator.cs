using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public static class FeeInstallmentGenerator
{
    public sealed record InstallmentPeriod(
        int PeriodIndex,
        string PeriodLabel,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        decimal Amount);

    public sealed record PeriodWindow(int PeriodIndex, string Label, DateOnly Start, DateOnly End);
    public sealed record PeriodAmount(int PeriodIndex, decimal Amount);

    public static IList<InstallmentPeriod> Generate(
        FeeCollectionType collectionType,
        decimal oneTimeAmount,
        IList<PeriodAmount> periodAmounts,
        IList<PeriodWindow> periods,
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        return collectionType switch
        {
            FeeCollectionType.OneTime when oneTimeAmount > 0 => new List<InstallmentPeriod>
            {
                new(1, $"One-time {FormatYearRange(yearStart, yearEnd)}", yearStart, yearEnd, oneTimeAmount)
            },
            FeeCollectionType.PeriodWise => GeneratePeriodWise(periodAmounts, periods),
            _ => Array.Empty<InstallmentPeriod>()
        };
    }

    private static IList<InstallmentPeriod> GeneratePeriodWise(
        IList<PeriodAmount> periodAmounts,
        IList<PeriodWindow> periods)
    {
        Dictionary<int, decimal> amountByPeriod = periodAmounts
            .GroupBy(x => x.PeriodIndex)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Amount));
        return periods
            .OrderBy(period => period.PeriodIndex)
            .Where(period => amountByPeriod.GetValueOrDefault(period.PeriodIndex) > 0)
            .Select(period => new InstallmentPeriod(
                period.PeriodIndex,
                period.Label,
                period.Start,
                period.End,
                amountByPeriod[period.PeriodIndex]))
            .ToList();
    }

    private static string FormatYearRange(DateOnly start, DateOnly end) =>
        start.Year == end.Year ? start.Year.ToString() : $"{start.Year}-{end.Year % 100:D2}";
}
