using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Operational tables cloned from the <c>school</c> template into each tenant schema.
/// </summary>
internal static class SchoolSchemaCatalog
{
    internal static readonly string[] TemplateTables =
    [
        DatabaseConfig.TableAcademicYears,
        DatabaseConfig.TableClasses,
        DatabaseConfig.TableSubjects,
        DatabaseConfig.TableTeachers,
        DatabaseConfig.TableStudents,
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentFeeConfigs,
        DatabaseConfig.TableAttendance,
        DatabaseConfig.TableSettings,
        DatabaseConfig.TableAlerts,
        DatabaseConfig.TableDepartments,
        DatabaseConfig.TableTeacherClassAssignments,
        DatabaseConfig.TableTeacherSubjectAssignments,
        DatabaseConfig.TableHodDepartmentAssignments,
        DatabaseConfig.TableParentStudentMappings,
        DatabaseConfig.TableStaffScopeAssignments,
    ];

    /// <summary>
    /// Unique constraints required for upsert SQL (ON CONFLICT ON CONSTRAINT ...).
    /// </summary>
    internal static readonly TenantUniqueConstraint[] RequiredUniqueConstraints =
    [
        new(
            "uq_teacherclassassignments",
            DatabaseConfig.TableTeacherClassAssignments,
            "teacherid",
            "classid",
            "academicyearid"),
        new(
            "uq_teachersubjectassignments",
            DatabaseConfig.TableTeacherSubjectAssignments,
            "teacherid",
            "subjectid",
            "classid",
            "academicyearid"),
        new(
            "uq_hoddepartmentassignments",
            DatabaseConfig.TableHodDepartmentAssignments,
            "userid",
            "departmentid"),
        new(
            "uq_parentstudentmappings",
            DatabaseConfig.TableParentStudentMappings,
            "parentuserid",
            "studentid"),
    ];
}

internal readonly record struct TenantUniqueConstraint(
    string Name,
    string Table,
    params string[] Columns);
