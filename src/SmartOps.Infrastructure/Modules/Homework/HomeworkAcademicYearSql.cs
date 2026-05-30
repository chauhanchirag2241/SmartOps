namespace SmartOps.Infrastructure.Modules.Homework;

/// <summary>
/// Homework is stored per class; academic year is resolved via the class row.
/// </summary>
internal static class HomeworkAcademicYearSql
{
    public static string FilterOnClass(string classTableAlias) =>
        $" AND (@ScopeAcademicYearId IS NULL OR {classTableAlias}.academicyearid = @ScopeAcademicYearId)";
}
