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
        DatabaseConfig.TableShifts,
        DatabaseConfig.TableFeeMaster,
        DatabaseConfig.TableFeeHead,
        DatabaseConfig.TableFeeHeadPeriodAmount,
        DatabaseConfig.TableFeeStudentAmount,
        DatabaseConfig.TableFeeMasterClassGroup,
        DatabaseConfig.TableFeePayment,
        DatabaseConfig.TableFeePaymentLine,
        DatabaseConfig.TableClassGroups,
        DatabaseConfig.TableClasses,
        DatabaseConfig.TableClassAcademicPeriods,
        DatabaseConfig.TableSubjects,
        DatabaseConfig.TableEmployees,
        DatabaseConfig.TableStudents,
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentCustomFields,
        DatabaseConfig.TableAttendance,
        DatabaseConfig.TableStaffAttendance,
        DatabaseConfig.TableEmployeeFaceEnrollments,
        DatabaseConfig.TableHomework,
        DatabaseConfig.TableHomeworkDetails,
        DatabaseConfig.TableSalaryStructure,
        DatabaseConfig.TableSalaryVersionComponents,
        DatabaseConfig.TableEmployeeSalaries,
        DatabaseConfig.TableEmployeeSalaryComponents,
        DatabaseConfig.TablePayrollRuns,
        DatabaseConfig.TablePayrollEntries,
        DatabaseConfig.TablePayrollEntryLines,
        DatabaseConfig.TableSettings,
        DatabaseConfig.TableDepartments,
        DatabaseConfig.TableClassSubjectTeacherMappings,
        DatabaseConfig.TableClassSettings,
        DatabaseConfig.TableHodDepartmentAssignments,
        DatabaseConfig.TableStaffScopeAssignments,
        DatabaseConfig.TableEntityAuditLogs,
        DatabaseConfig.TableLeaveRequests,
        DatabaseConfig.TableWorkflowItems,
        DatabaseConfig.TableWorkflowItemActions,
        DatabaseConfig.TableNotices,
        DatabaseConfig.TableNoticeResponses,
    ];

    /// <summary>
    /// Unique constraints required for tenant schema sync.
    /// </summary>
    internal static readonly TenantUniqueConstraint[] RequiredUniqueConstraints =
    [
        new(
            "uq_hoddepartmentassignments",
            DatabaseConfig.TableHodDepartmentAssignments,
            "userid",
            "departmentid"),
        new(
            "uq_attendance_class_student_date",
            DatabaseConfig.TableAttendance,
            "classid",
            "studentid",
            "attendancedate"),
        new(
            "uq_staffattendance_employee_date",
            DatabaseConfig.TableStaffAttendance,
            "employeeid",
            "attendancedate"),
        new(
            "uq_homeworkdetails_homework_student",
            DatabaseConfig.TableHomeworkDetails,
            "homeworkid",
            "studentid"),
        new(
            "uq_payrollruns_year_month",
            DatabaseConfig.TablePayrollRuns,
            "payyear",
            "paymonth"),
        new(
            "uq_salarystructure_branch_version",
            DatabaseConfig.TableSalaryStructure,
            "branchid",
            "versionnumber"),
        new(
            "uq_employeesalarycomponents_assignment_version_component",
            DatabaseConfig.TableEmployeeSalaryComponents,
            "employeesalaryid",
            "salaryversioncomponentid"),
        new(
            "uq_noticeresponses_notice_user",
            DatabaseConfig.TableNoticeResponses,
            "noticeid",
            "respondentuserid"),
    ];
}

internal readonly record struct TenantUniqueConstraint(
    string Name,
    string Table,
    params string[] Columns);
