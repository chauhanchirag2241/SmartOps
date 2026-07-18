using SmartOps.Application.Modules.AcademicPeriod;
using SmartOps.Domain.Modules.AcademicPeriod;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class AcademicPeriodValidationTests
{
    private static readonly DateOnly YearStart = new(2026, 4, 1);
    private static readonly DateOnly YearEnd = new(2027, 3, 31);

    [Fact]
    public void Single_term_is_valid()
    {
        string? error = AcademicPeriodValidation.Validate(
            YearStart,
            YearEnd,
            AcademicPeriodType.Term,
            [new(1, "Term 1", YearStart, YearEnd)]);

        Assert.Null(error);
    }

    [Fact]
    public void Overlapping_periods_are_rejected()
    {
        string? error = AcademicPeriodValidation.Validate(
            YearStart,
            YearEnd,
            AcademicPeriodType.Semester,
            [
                new(1, "Semester 1", YearStart, new DateOnly(2026, 10, 1)),
                new(2, "Semester 2", new DateOnly(2026, 10, 1), YearEnd),
            ]);

        Assert.Contains("overlap", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Period_outside_academic_year_is_rejected()
    {
        string? error = AcademicPeriodValidation.Validate(
            YearStart,
            YearEnd,
            AcademicPeriodType.Custom,
            [new(1, "Orientation", YearStart.AddDays(-1), YearStart)]);

        Assert.Contains("academic year range", error, StringComparison.OrdinalIgnoreCase);
    }
}
