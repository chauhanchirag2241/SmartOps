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

    public static IList<InstallmentPeriod> Generate(
        DateOnly yearStart,
        DateOnly yearEnd,
        FeeFrequency frequency,
        FeeAmountBasis amountBasis,
        decimal classAmount)
    {
        if (classAmount <= 0)
        {
            return Array.Empty<InstallmentPeriod>();
        }

        IList<(DateOnly Start, DateOnly End, string Label)> windows = BuildPeriodWindows(yearStart, yearEnd, frequency);
        if (windows.Count == 0)
        {
            return Array.Empty<InstallmentPeriod>();
        }

        return amountBasis == FeeAmountBasis.PerInstallment
            ? BuildPerInstallmentAmounts(windows, classAmount)
            : BuildAnnualTotalAmounts(windows, classAmount);
    }

    private static IList<InstallmentPeriod> BuildPerInstallmentAmounts(
        IList<(DateOnly Start, DateOnly End, string Label)> windows,
        decimal perPeriodAmount)
    {
        var result = new List<InstallmentPeriod>(windows.Count);
        for (int i = 0; i < windows.Count; i++)
        {
            (DateOnly start, DateOnly end, string label) = windows[i];
            result.Add(new InstallmentPeriod(i + 1, label, start, end, perPeriodAmount));
        }

        return result;
    }

    private static IList<InstallmentPeriod> BuildAnnualTotalAmounts(
        IList<(DateOnly Start, DateOnly End, string Label)> windows,
        decimal annualTotal)
    {
        int count = windows.Count;
        decimal baseShare = Math.Floor(annualTotal / count * 100m) / 100m;
        decimal assigned = 0m;
        var result = new List<InstallmentPeriod>(count);

        for (int i = 0; i < count; i++)
        {
            (DateOnly start, DateOnly end, string label) = windows[i];
            decimal amount = i == count - 1 ? annualTotal - assigned : baseShare;
            assigned += amount;
            result.Add(new InstallmentPeriod(i + 1, label, start, end, amount));
        }

        return result;
    }

    internal static IList<(DateOnly Start, DateOnly End, string Label)> BuildPeriodWindows(
        DateOnly yearStart,
        DateOnly yearEnd,
        FeeFrequency frequency) =>
        frequency switch
        {
            FeeFrequency.Monthly => BuildMonthlyWindows(yearStart, yearEnd),
            FeeFrequency.Quarterly => BuildChunkedWindows(yearStart, yearEnd, 3, "Q"),
            FeeFrequency.SemiAnnual => BuildChunkedWindows(yearStart, yearEnd, 6, "H"),
            FeeFrequency.Annual => new List<(DateOnly, DateOnly, string)>
            {
                (yearStart, yearEnd, $"Annual {FormatYearRange(yearStart, yearEnd)}")
            },
            FeeFrequency.OneTime => new List<(DateOnly, DateOnly, string)>
            {
                (yearStart, yearEnd, $"One-time {FormatYearRange(yearStart, yearEnd)}")
            },
            _ => new List<(DateOnly, DateOnly, string)>
            {
                (yearStart, yearEnd, $"Annual {FormatYearRange(yearStart, yearEnd)}")
            }
        };

    private static IList<(DateOnly Start, DateOnly End, string Label)> BuildMonthlyWindows(
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        var windows = new List<(DateOnly, DateOnly, string)>();
        var cursor = new DateOnly(yearStart.Year, yearStart.Month, 1);
        while (cursor <= yearEnd)
        {
            DateOnly periodStart = cursor < yearStart ? yearStart : cursor;
            DateOnly monthEnd = new DateOnly(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            DateOnly periodEnd = monthEnd > yearEnd ? yearEnd : monthEnd;
            if (periodStart <= periodEnd)
            {
                windows.Add((periodStart, periodEnd, cursor.ToString("MMM yyyy")));
            }

            cursor = cursor.AddMonths(1);
        }

        return windows;
    }

    private static IList<(DateOnly Start, DateOnly End, string Label)> BuildChunkedWindows(
        DateOnly yearStart,
        DateOnly yearEnd,
        int monthsPerChunk,
        string prefix)
    {
        IList<(DateOnly Start, DateOnly End, string Label)> months = BuildMonthlyWindows(yearStart, yearEnd);
        if (months.Count == 0)
        {
            return Array.Empty<(DateOnly, DateOnly, string)>();
        }

        var chunks = new List<(DateOnly, DateOnly, string)>();
        int chunkIndex = 1;
        for (int i = 0; i < months.Count; i += monthsPerChunk)
        {
            DateOnly start = months[i].Start;
            DateOnly end = months[Math.Min(i + monthsPerChunk - 1, months.Count - 1)].End;
            string label = prefix switch
            {
                "Q" => $"{prefix}{chunkIndex} {FormatYearRange(yearStart, yearEnd)}",
                "H" => $"{prefix}{chunkIndex} {FormatYearRange(yearStart, yearEnd)}",
                _ => $"{prefix}{chunkIndex}"
            };
            chunks.Add((start, end, label));
            chunkIndex++;
        }

        return chunks;
    }

    private static string FormatYearRange(DateOnly start, DateOnly end) =>
        start.Year == end.Year ? start.Year.ToString() : $"{start.Year}-{end.Year % 100:D2}";
}
