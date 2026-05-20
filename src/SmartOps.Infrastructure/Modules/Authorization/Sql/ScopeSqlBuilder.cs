using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Authorization.Sql;

public static class ScopeSqlBuilder
{
    public static string AppendStudentScopeFilter(
        IUserScopeContext scope,
        string studentTableAlias,
        string schema,
        ref string whereClause)
    {
        if (!scope.ScopesEnabled || scope.IsGlobalScope)
        {
            return whereClause;
        }

        return scope.ScopeType switch
        {
            DataScopeType.ModuleOnly or DataScopeType.None => AppendDenyAll(whereClause),
            DataScopeType.Self or DataScopeType.LinkedStudents => AppendStudentIdFilter(
                whereClause, studentTableAlias, scope.AllowedStudentIds),
            DataScopeType.Class or DataScopeType.Department or DataScopeType.Custom or DataScopeType.SubjectClass =>
                AppendClassEnrollmentFilter(whereClause, studentTableAlias, schema, scope.AllowedClassIds, scope.ActiveAcademicYearId),
            _ => whereClause
        };
    }

    public static Guid? ResolveClassIdFilter(IUserScopeContext scope, Guid? requestedClassId)
    {
        if (!scope.ScopesEnabled || scope.IsGlobalScope)
        {
            return requestedClassId;
        }

        if (scope.ScopeType is DataScopeType.ModuleOnly or DataScopeType.None or DataScopeType.Self)
        {
            return null;
        }

        if (!requestedClassId.HasValue)
        {
            return null;
        }

        return scope.HasClassAccess(requestedClassId.Value) ? requestedClassId : Guid.Empty;
    }

    private static string AppendDenyAll(string whereClause) => $"{whereClause} AND 1 = 0";

    private static string AppendStudentIdFilter(string whereClause, string alias, IReadOnlyList<Guid> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return AppendDenyAll(whereClause);
        }

        return $"{whereClause} AND {alias}.id = ANY(@ScopeStudentIds)";
    }

    private static string AppendClassEnrollmentFilter(
        string whereClause,
        string alias,
        string schema,
        IReadOnlyList<Guid> classIds,
        Guid? academicYearId)
    {
        if (classIds.Count == 0)
        {
            return AppendDenyAll(whereClause);
        }

        return $"""
{whereClause} AND EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
    WHERE sa.studentid = {alias}.id
      AND sa.isactive = true
      AND sa.classid = ANY(@ScopeClassIds)
      AND (@ScopeAcademicYearId IS NULL OR sa.academicyearid = @ScopeAcademicYearId)
)
""";
    }
}
