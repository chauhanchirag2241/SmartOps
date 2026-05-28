namespace SmartOps.Domain.Modules.Class;

/// <summary>
/// Section: A = 1, B = 2, C = 3, D = 4
/// </summary>
public enum Section
{
    A = 1,
    B = 2,
    C = 3,
    D = 4
}

/// <summary>
/// Stream / Group: None (legacy unset) = 1, Science = 2, Commerce = 3, Arts = 4, Regional = 5, Primary = 6
/// </summary>
public enum StreamGroup
{
    None = 1,
    Science = 2,
    Commerce = 3,
    Arts = 4,
    Regional = 5,
    Primary = 6
}

/// <summary>
/// Shift: Morning = 1, Afternoon = 2, Evening = 3
/// </summary>
public enum Shift
{
    Morning = 1,
    Afternoon = 2,
    Evening = 3
}

/// <summary>
/// Medium: English = 1, Hindi = 2, Gujarati = 3
/// </summary>
public enum Medium
{
    English = 1,
    Hindi = 2,
    Gujarati = 3
}
