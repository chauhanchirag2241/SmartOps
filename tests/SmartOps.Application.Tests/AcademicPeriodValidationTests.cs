using SmartOps.Application.Modules.AcademicPeriod;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class AcademicPeriodValidationTests
{
    [Fact]
    public void Single_period_is_valid()
    {
        string? error = AcademicPeriodValidation.Validate(
            [new(1, "Period 1")]);

        Assert.Null(error);
    }

    [Fact]
    public void Duplicate_names_are_rejected()
    {
        string? error = AcademicPeriodValidation.Validate(
            [
                new(1, "Term 1"),
                new(2, "Term 1"),
            ]);

        Assert.Contains("unique", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_sequential_indexes_are_rejected()
    {
        string? error = AcademicPeriodValidation.Validate(
            [
                new(1, "Period 1"),
                new(3, "Period 2"),
            ]);

        Assert.Contains("sequential", error, StringComparison.OrdinalIgnoreCase);
    }
}
