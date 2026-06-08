namespace SmartOps.Application.Configuration;

public sealed class PerSchoolDatabaseOptions
{
    public const string SectionName = "PerSchoolDatabase";

    /// <summary>
    /// When true, new schools are provisioned with a dedicated PostgreSQL database.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Prefix for per-school database names (e.g. smartops_school_acme).
    /// </summary>
    public string DatabaseNamePrefix { get; set; } = "smartops_school_";
}
