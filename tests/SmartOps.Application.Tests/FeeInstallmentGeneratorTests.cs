using SmartOps.Application.Modules.Fees;
using SmartOps.Domain.Modules.Fees;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class FeeInstallmentGeneratorTests
{
    private static readonly DateOnly YearStart = new(2025, 4, 1);
    private static readonly DateOnly YearEnd = new(2026, 3, 31);

    private static readonly IList<FeeInstallmentGenerator.SemesterWindow> Semesters =
    [
        new("Semester 1", new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)),
        new("Semester 2", new DateOnly(2025, 10, 1), new DateOnly(2026, 3, 31)),
    ];

    [Fact]
    public void SemesterWise_creates_two_periods_with_configured_amounts()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.SemesterWise,
            oneTimeAmount: 0,
            semester1Amount: 5000m,
            semester2Amount: 6000m,
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
            semester1Amount: 0,
            semester2Amount: 0,
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
            FeeCollectionType.SemesterWise,
            0,
            0,
            0,
            Semesters,
            YearStart,
            YearEnd);

        Assert.Empty(periods);
    }

    [Fact]
    public void SemesterWise_without_semester_config_splits_year_in_half()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            FeeCollectionType.SemesterWise,
            0,
            4000m,
            4000m,
            Array.Empty<FeeInstallmentGenerator.SemesterWindow>(),
            YearStart,
            YearEnd);

        Assert.Equal(2, periods.Count);
        Assert.Equal(8000m, periods.Sum(p => p.Amount));
    }
}
