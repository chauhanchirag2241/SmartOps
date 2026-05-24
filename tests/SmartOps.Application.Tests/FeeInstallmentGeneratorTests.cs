using SmartOps.Application.Modules.Fees;
using SmartOps.Domain.Modules.Fees;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class FeeInstallmentGeneratorTests
{
    private static readonly DateOnly YearStart = new(2025, 4, 1);
    private static readonly DateOnly YearEnd = new(2026, 3, 31);

    [Fact]
    public void Monthly_PerInstallment_uses_amount_each_month()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.Monthly,
            FeeAmountBasis.PerInstallment,
            2000m);

        Assert.Equal(12, periods.Count);
        Assert.All(periods, p => Assert.Equal(2000m, p.Amount));
        Assert.Equal(24000m, periods.Sum(p => p.Amount));
    }

    [Fact]
    public void Monthly_AnnualTotal_splits_across_months()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.Monthly,
            FeeAmountBasis.AnnualTotal,
            12000m);

        Assert.Equal(12, periods.Count);
        Assert.Equal(12000m, periods.Sum(p => p.Amount));
    }

    [Fact]
    public void Quarterly_AnnualTotal_creates_four_periods()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.Quarterly,
            FeeAmountBasis.AnnualTotal,
            8000m);

        Assert.Equal(4, periods.Count);
        Assert.Equal(8000m, periods.Sum(p => p.Amount));
    }

    [Fact]
    public void SemiAnnual_PerInstallment_creates_two_periods()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.SemiAnnual,
            FeeAmountBasis.PerInstallment,
            5000m);

        Assert.Equal(2, periods.Count);
        Assert.All(periods, p => Assert.Equal(5000m, p.Amount));
    }

    [Fact]
    public void OneTime_creates_single_period()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.OneTime,
            FeeAmountBasis.PerInstallment,
            15000m);

        Assert.Single(periods);
        Assert.Equal(15000m, periods[0].Amount);
    }

    [Fact]
    public void Zero_amount_returns_empty()
    {
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            YearStart,
            YearEnd,
            FeeFrequency.Monthly,
            FeeAmountBasis.AnnualTotal,
            0m);

        Assert.Empty(periods);
    }

    [Fact]
    public void Short_academic_year_monthly_counts_partial_months()
    {
        var start = new DateOnly(2025, 6, 1);
        var end = new DateOnly(2025, 8, 31);
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            start,
            end,
            FeeFrequency.Monthly,
            FeeAmountBasis.AnnualTotal,
            3000m);

        Assert.Equal(3, periods.Count);
        Assert.Equal(3000m, periods.Sum(p => p.Amount));
    }
}
