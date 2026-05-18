using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Authorization.DTOs;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using SmartOps.Shared.Constants;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Authorization.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly DapperContext _context;
    private readonly IUserScopeContext _scope;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(DapperContext context, IUserScopeContext scope, ICurrentUserService currentUser)
    {
        _context = context;
        _scope = scope;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        string schema = _context.OperationalSchema;
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string studentFilter = BuildStudentExistsFilter(schema, "s");
        string classFilter = BuildClassFilter(schema, "c");
        string teacherFilter = BuildTeacherFilter(schema, "t");

        string sql = $"""
SELECT
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableStudents} s WHERE s.isactive = true {studentFilter}) AS TotalStudents,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableTeachers} t WHERE t.isactive = true {teacherFilter}) AS TotalTeachers,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableClasses} c WHERE c.isactive = true {classFilter}) AS TotalClasses,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableAttendance} a
        WHERE a.attendancedate = CURRENT_DATE AND a.isactive = true
        {BuildAttendanceFilter(schema, "a")}) AS AttendanceMarkedToday
""";

        var row = await connection.QuerySingleAsync<DashboardRow>(
            new CommandDefinition(sql, BuildParameters(), cancellationToken: cancellationToken)).ConfigureAwait(false);

        string scopeLabel = _scope.ScopeType switch
        {
            DataScopeType.Global => "All school data",
            DataScopeType.Class => "Your assigned classes",
            DataScopeType.Department => "Your department",
            DataScopeType.LinkedStudents => "Your children",
            DataScopeType.Self => "Your profile",
            DataScopeType.ModuleOnly => "Accounts module",
            _ => "Limited access"
        };

        return new DashboardSummaryDto
        {
            TotalStudents = row.TotalStudents,
            TotalTeachers = row.TotalTeachers,
            TotalClasses = row.TotalClasses,
            AttendanceMarkedToday = row.AttendanceMarkedToday,
            AverageAttendancePercent = 0,
            ScopeLabel = scopeLabel
        };
    }

    private string BuildStudentExistsFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return string.Empty;
        }

        if (_scope.ScopeType is DataScopeType.Self or DataScopeType.LinkedStudents)
        {
            return _scope.AllowedStudentIds.Count > 0
                ? $" AND {alias}.id = ANY(@ScopeStudentIds)"
                : " AND 1 = 0";
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $"""
 AND EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
    WHERE sa.studentid = {alias}.id AND sa.isactive = true
      AND sa.classid = ANY(@ScopeClassIds)
      AND (@ScopeAcademicYearId IS NULL OR sa.academicyearid = @ScopeAcademicYearId)
)
""";
    }

    private string BuildClassFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return string.Empty;
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $" AND {alias}.id = ANY(@ScopeClassIds)";
    }

    private string BuildTeacherFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return string.Empty;
        }

        if (_scope.AllowedTeacherIds.Count > 0)
        {
            return $" AND {alias}.id = ANY(@ScopeTeacherIds)";
        }

        if (_scope.AllowedDepartmentIds.Count > 0)
        {
            return $" AND {alias}.departmentid = ANY(@ScopeDepartmentIds)";
        }

        return " AND 1 = 0";
    }

    private string BuildAttendanceFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return string.Empty;
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $" AND {alias}.classid = ANY(@ScopeClassIds)";
    }

    private object BuildParameters() => new
    {
        ScopeStudentIds = _scope.AllowedStudentIds.ToArray(),
        ScopeClassIds = _scope.AllowedClassIds.ToArray(),
        ScopeTeacherIds = _scope.AllowedTeacherIds.ToArray(),
        ScopeDepartmentIds = _scope.AllowedDepartmentIds.ToArray(),
        ScopeAcademicYearId = _scope.ActiveAcademicYearId
    };

    private sealed class DashboardRow
    {
        public int TotalStudents { get; init; }

        public int TotalTeachers { get; init; }

        public int TotalClasses { get; init; }

        public int AttendanceMarkedToday { get; init; }
    }
}
