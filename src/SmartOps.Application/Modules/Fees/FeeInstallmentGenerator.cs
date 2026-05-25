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

    public sealed record SemesterWindow(string Label, DateOnly Start, DateOnly End);

    public static IList<InstallmentPeriod> Generate(
        FeeCollectionType collectionType,
        decimal oneTimeAmount,
        decimal semester1Amount,
        decimal semester2Amount,
        IList<SemesterWindow> semesters,
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        return collectionType switch
        {
            FeeCollectionType.OneTime when oneTimeAmount > 0 => new List<InstallmentPeriod>
            {
                new(1, $"One-time {FormatYearRange(yearStart, yearEnd)}", yearStart, yearEnd, oneTimeAmount)
            },
            FeeCollectionType.SemesterWise => GenerateSemesterWise(semester1Amount, semester2Amount, semesters, yearStart, yearEnd),
            _ => Array.Empty<InstallmentPeriod>()
        };
    }

    private static IList<InstallmentPeriod> GenerateSemesterWise(
        decimal semester1Amount,
        decimal semester2Amount,
        IList<SemesterWindow> semesters,
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        var periods = new List<InstallmentPeriod>();
        if (semesters.Count >= 2)
        {
            if (semester1Amount > 0)
            {
                SemesterWindow s1 = semesters[0];
                periods.Add(new InstallmentPeriod(1, s1.Label, s1.Start, s1.End, semester1Amount));
            }

            if (semester2Amount > 0)
            {
                SemesterWindow s2 = semesters[1];
                periods.Add(new InstallmentPeriod(2, s2.Label, s2.Start, s2.End, semester2Amount));
            }

            return periods;
        }

        // Fallback when semesters are not configured: split academic year in half.
        int totalDays = Math.Max(1, yearEnd.DayNumber - yearStart.DayNumber);
        DateOnly mid = yearStart.AddDays(totalDays / 2);
        if (semester1Amount > 0)
        {
            periods.Add(new InstallmentPeriod(1, "Semester 1", yearStart, mid, semester1Amount));
        }

        if (semester2Amount > 0)
        {
            periods.Add(new InstallmentPeriod(2, "Semester 2", mid.AddDays(1), yearEnd, semester2Amount));
        }

        return periods;
    }

    private static string FormatYearRange(DateOnly start, DateOnly end) =>
        start.Year == end.Year ? start.Year.ToString() : $"{start.Year}-{end.Year % 100:D2}";
}
