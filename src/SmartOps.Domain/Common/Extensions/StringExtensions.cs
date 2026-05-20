namespace SmartOps.Domain.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    public static bool IsMissing(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}
