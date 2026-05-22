using SmartOps.Domain.Common.Configuration;

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
        DatabaseConfig.TableStudentCustomFields,
        DatabaseConfig.TableAttendance,
        DatabaseConfig.TableHomework,
        DatabaseConfig.TableHomeworkDetails,
        DatabaseConfig.TableSettings,
        DatabaseConfig.TableAlerts,
        DatabaseConfig.TableDepartments,
        DatabaseConfig.TableClassSubjectTeacherMappings,
        DatabaseConfig.TableHodDepartmentAssignments,
        DatabaseConfig.TableParentStudentMappings,
        DatabaseConfig.TableStaffScopeAssignments,
    ];

    /// <summary>
    /// Unique constraints required for tenant schema sync.
    /// </summary>
    internal static readonly TenantUniqueConstraint[] RequiredUniqueConstraints =
    [
        new(
            "uq_classsubjectteachermappings",
            DatabaseConfig.TableClassSubjectTeacherMappings,
            "classid",
            "subjectid",
            "teacherid",
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
        new(
            "uq_attendance_class_student_date",
            DatabaseConfig.TableAttendance,
            "classid",
            "studentid",
            "attendancedate"),
        new(
            "uq_homeworkdetails_homework_student",
            DatabaseConfig.TableHomeworkDetails,
            "homeworkid",
            "studentid"),
    ];
}

internal readonly record struct TenantUniqueConstraint(
    string Name,
    string Table,
    params string[] Columns);
