using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Configuration;

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

    /// <summary>
    /// Limits student lists to rows with an enrollment in the scoped academic year.
    /// </summary>
    public static string AppendStudentHasEnrollmentInScopeYear(
        IUserScopeContext scope,
        string studentTableAlias,
        string schema,
        ref string whereClause)
    {
        if (!scope.ActiveAcademicYearId.HasValue)
        {
            return whereClause;
        }

        return $"""
{whereClause} AND EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
    WHERE sa.studentid = {studentTableAlias}.id
      AND {StudentAcademicEnrollmentVisibilityClause("sa")}
)
""";
    }
}
