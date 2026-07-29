namespace SmartOps.Infrastructure.Modules.Homework;

/// <summary>
/// Homework is stored per class. Class groups are timeless; year scoping uses
/// student enrollment / scope parameters elsewhere, not classgroups.academicyearid.
/// </summary>
internal static class HomeworkAcademicYearSql
{
    public static string FilterOnClassGroup(string classGroupAlias = "cg") => "";
}
