using SmartOps.Application.Modules.Authorization.Interfaces;

namespace SmartOps.Infrastructure.Modules.Authorization.Sql;

public static class AcademicYearScopeSql
{
    public static string AppendAcademicYearFilter(
        IUserScopeContext scope,
        string columnExpression,
        ref string whereClause)
    {
        if (scope.ActiveAcademicYearId.HasValue)
        {
            whereClause += $" AND {columnExpression} = @ScopeAcademicYearId";
        }

        return whereClause;
    }

    /// <summary>
    /// When a header academic year is scoped, include inactive (promoted) enrollments for that year.
    /// When no year scope, only active enrollments apply.
    /// </summary>
    public static string StudentAcademicEnrollmentVisibilityClause(string tableAlias = "sa")
    {
        return $"""
            (@ScopeAcademicYearId IS NULL OR {tableAlias}.academicyearid = @ScopeAcademicYearId)
            AND (@ScopeAcademicYearId IS NOT NULL OR {tableAlias}.isactive = true)
            """;
    }
}
