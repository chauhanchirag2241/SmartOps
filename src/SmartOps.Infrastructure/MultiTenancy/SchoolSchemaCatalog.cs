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
        DatabaseConfig.TableStudentFeeHeadAssignments,
        DatabaseConfig.TableStudentFeeInstallments,
        DatabaseConfig.TableStudentCustomFields,
        DatabaseConfig.TableAttendance,
        DatabaseConfig.TableHomework,
        DatabaseConfig.TableHomeworkDetails,
        DatabaseConfig.TableFeeStructureVersions,
        DatabaseConfig.TableFeeTypes,
        DatabaseConfig.TableFeeSettings,
        DatabaseConfig.TableClassFeeAmounts,
        DatabaseConfig.TableClassFeeInstallments,
        DatabaseConfig.TableFeePayments,
        DatabaseConfig.TableFeePaymentAllocations,
        DatabaseConfig.TableSalaryStructureVersions,
        DatabaseConfig.TableSalaryVersionComponents,
        DatabaseConfig.TableEmployeeSalaries,
        DatabaseConfig.TableEmployeeSalaryComponents,
        DatabaseConfig.TablePayrollRuns,
        DatabaseConfig.TablePayrollEntries,
        DatabaseConfig.TablePayrollEntryLines,
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
        new(
            "uq_classfeeamounts_class_feetype_version",
            DatabaseConfig.TableClassFeeAmounts,
            "classid",
            "feetypeid",
            "feestructureversionid"),
        new(
            "uq_classfeeinstallments_class_feetype_version_period",
            DatabaseConfig.TableClassFeeInstallments,
            "classid",
            "feetypeid",
            "feestructureversionid",
            "periodindex"),
        new(
            "uq_studentfeeheadassignments_student_feetype_version",
            DatabaseConfig.TableStudentFeeHeadAssignments,
            "studentid",
            "feetypeid",
            "feestructureversionid"),
        new(
            "uq_studentfeeinstallments_student_feetype_version_period",
            DatabaseConfig.TableStudentFeeInstallments,
            "studentid",
            "feetypeid",
            "feestructureversionid",
            "periodindex"),
        new(
            "uq_payrollruns_year_month",
            DatabaseConfig.TablePayrollRuns,
            "payyear",
            "paymonth"),
        new(
            "uq_salarystructureversions_year_version",
            DatabaseConfig.TableSalaryStructureVersions,
            "academicyearid",
            "versionnumber"),
        new(
            "uq_employeesalarycomponents_assignment_version_component",
            DatabaseConfig.TableEmployeeSalaryComponents,
            "employeesalaryid",
            "salaryversioncomponentid"),
    ];
}

internal readonly record struct TenantUniqueConstraint(
    string Name,
    string Table,
    params string[] Columns);
