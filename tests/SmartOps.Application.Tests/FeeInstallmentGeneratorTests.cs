using SmartOps.Application.Modules.Fees;
using SmartOps.Domain.Modules.Fees;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class FeeInstallmentGeneratorTests
{
    private static readonly DateOnly YearStart = new(2025, 4, 1);
    private static readonly DateOnly YearEnd = new(2026, 3, 31);

    private static readonly IList<FeeInstallmentGenerator.PeriodWindow> Semesters =
    [
        new(1, "Semester 1", new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)),
        new(2, "Semester 2", new DateOnly(2025, 10, 1), new DateOnly(2026, 3, 31)),
    ];

    [Fact]
    public void SemesterWise_creates_two_periods_with_configured_amounts()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.PeriodWise,
            oneTimeAmount: 0,
            [new(1, 5000m), new(2, 6000m)],
            Semesters,
            YearStart,
            YearEnd);

        Assert.Equal(2, periods.Count);
        Assert.Equal(5000m, periods[0].Amount);
        Assert.Equal(6000m, periods[1].Amount);
        Assert.Equal("Semester 1", periods[0].PeriodLabel);
        Assert.Equal("Semester 2", periods[1].PeriodLabel);
    }

    [Fact]
    public void OneTime_creates_single_period()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.OneTime,
            oneTimeAmount: 15000m,
            [],
            Semesters,
            YearStart,
            YearEnd);

        Assert.Single(periods);
        Assert.Equal(15000m, periods[0].Amount);
    }

    [Fact]
    public void Zero_amounts_return_empty()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.PeriodWise,
            0,
            [],
            Semesters,
            YearStart,
            YearEnd);

        Assert.Empty(periods);
    }

    [Fact]
    public void PeriodWise_without_period_config_returns_empty()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.PeriodWise,
            0,
            [new(1, 4000m), new(2, 4000m)],
            Array.Empty<FeeInstallmentGenerator.PeriodWindow>(),
            YearStart,
            YearEnd);

        Assert.Empty(periods);
    }

    [Fact]
    public void PeriodWise_supports_single_term()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.PeriodWise,
            0,
            [new(1, 8000m)],
            [new(1, "Term 1", YearStart, YearEnd)],
            YearStart,
            YearEnd);

        Assert.Single(periods);
        Assert.Equal("Term 1", periods[0].PeriodLabel);
        Assert.Equal(8000m, periods[0].Amount);
    }

    [Fact]
    public void PeriodWise_supports_four_quarters()
    {
        IList<FeeInstallmentGenerator.PeriodWindow> quarters =
        [
            new(1, "Quarter 1", new(2025, 4, 1), new(2025, 6, 30)),
            new(2, "Quarter 2", new(2025, 7, 1), new(2025, 9, 30)),
            new(3, "Quarter 3", new(2025, 10, 1), new(2025, 12, 31)),
            new(4, "Quarter 4", new(2026, 1, 1), new(2026, 3, 31)),
        ];
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.PeriodWise,
            0,
            [new(1, 2000m), new(2, 2000m), new(3, 2000m), new(4, 2000m)],
            quarters,
            YearStart,
            YearEnd);

        Assert.Equal(4, periods.Count);
        Assert.Equal(8000m, periods.Sum(p => p.Amount));
    }
}
