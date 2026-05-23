namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Compile-time policy names for [Authorize(Policy = ...)] attributes.
/// </summary>
public static class MenuPolicies
{
    public static class Dashboard
    {
        public const string View = "Menu:DASHBOARD:View";
        public const string Add = "Menu:DASHBOARD:Add";
        public const string Edit = "Menu:DASHBOARD:Edit";
        public const string Delete = "Menu:DASHBOARD:Delete";
        public const string Export = "Menu:DASHBOARD:Export";
    }

    public static class Schools
    {
        public const string View = "Menu:SCHOOLS:View";
        public const string Add = "Menu:SCHOOLS:Add";
        public const string Edit = "Menu:SCHOOLS:Edit";
        public const string Delete = "Menu:SCHOOLS:Delete";
        public const string Export = "Menu:SCHOOLS:Export";
    }

    public static class Users
    {
        public const string View = "Menu:USERS:View";
        public const string Add = "Menu:USERS:Add";
        public const string Edit = "Menu:USERS:Edit";
        public const string Delete = "Menu:USERS:Delete";
        public const string Export = "Menu:USERS:Export";
    }

    public static class Roles
    {
        public const string View = "Menu:ROLES:View";
        public const string Add = "Menu:ROLES:Add";
        public const string Edit = "Menu:ROLES:Edit";
        public const string Delete = "Menu:ROLES:Delete";
        public const string Export = "Menu:ROLES:Export";
    }

    public static class Settings
    {
        public const string View = "Menu:SETTINGS:View";
        public const string Add = "Menu:SETTINGS:Add";
        public const string Edit = "Menu:SETTINGS:Edit";
        public const string Delete = "Menu:SETTINGS:Delete";
        public const string Export = "Menu:SETTINGS:Export";
    }

    public static class Academics
    {
        public const string View = "Menu:ACADEMICS:View";
        public const string Add = "Menu:ACADEMICS:Add";
        public const string Edit = "Menu:ACADEMICS:Edit";
        public const string Delete = "Menu:ACADEMICS:Delete";
        public const string Export = "Menu:ACADEMICS:Export";
    }

    public static class Students
    {
        public const string View = "Menu:STUDENTS:View";
        public const string Add = "Menu:STUDENTS:Add";
        public const string Edit = "Menu:STUDENTS:Edit";
        public const string Delete = "Menu:STUDENTS:Delete";
        public const string Export = "Menu:STUDENTS:Export";

        /// <summary>STUDENTS.View or ATTENDANCE.View — list/roster for attendance module.</summary>
        public const string ListForAttendanceOrModule = "Menu:STUDENTS:ViewOrAttendanceView";
    }

    public static class Teachers
    {
        public const string View = "Menu:TEACHERS:View";
        public const string Add = "Menu:TEACHERS:Add";
        public const string Edit = "Menu:TEACHERS:Edit";
        public const string Delete = "Menu:TEACHERS:Delete";
        public const string Export = "Menu:TEACHERS:Export";
    }

    public static class Classes
    {
        public const string View = "Menu:CLASSES:View";
        public const string Add = "Menu:CLASSES:Add";
        public const string Edit = "Menu:CLASSES:Edit";
        public const string Delete = "Menu:CLASSES:Delete";
        public const string Export = "Menu:CLASSES:Export";

        /// <summary>CLASSES.View or ATTENDANCE.View — class dropdown on attendance page.</summary>
        public const string ListForAttendanceDropdown = "Menu:CLASSES:ViewOrAttendanceView";
    }

    public static class ClassMappings
    {
        public const string View = "Menu:CLASS_MAPPINGS:View";
        public const string Add = "Menu:CLASS_MAPPINGS:Add";
        public const string Edit = "Menu:CLASS_MAPPINGS:Edit";
        public const string Delete = "Menu:CLASS_MAPPINGS:Delete";
        public const string Export = "Menu:CLASS_MAPPINGS:Export";
    }

    public static class Subjects
    {
        public const string View = "Menu:SUBJECTS:View";
        public const string Add = "Menu:SUBJECTS:Add";
        public const string Edit = "Menu:SUBJECTS:Edit";
        public const string Delete = "Menu:SUBJECTS:Delete";
        public const string Export = "Menu:SUBJECTS:Export";
    }

    public static class AcademicYears
    {
        public const string View = "Menu:ACADEMIC_YEARS:View";
        public const string Add = "Menu:ACADEMIC_YEARS:Add";
        public const string Edit = "Menu:ACADEMIC_YEARS:Edit";
        public const string Delete = "Menu:ACADEMIC_YEARS:Delete";
        public const string Export = "Menu:ACADEMIC_YEARS:Export";
    }

    public static class Attendance
    {
        public const string View = "Menu:ATTENDANCE:View";
        public const string Add = "Menu:ATTENDANCE:Add";
        public const string Edit = "Menu:ATTENDANCE:Edit";
        public const string Delete = "Menu:ATTENDANCE:Delete";
        public const string Export = "Menu:ATTENDANCE:Export";
    }

    public static class Homework
    {
        public const string View = "Menu:HOMEWORK:View";
        public const string Add = "Menu:HOMEWORK:Add";
        public const string Edit = "Menu:HOMEWORK:Edit";
        public const string Delete = "Menu:HOMEWORK:Delete";
        public const string Export = "Menu:HOMEWORK:Export";
    }

    public static class FeesStructure
    {
        public const string View = "Menu:FEES_STRUCTURE:View";
        public const string Add = "Menu:FEES_STRUCTURE:Add";
        public const string Edit = "Menu:FEES_STRUCTURE:Edit";
        public const string Delete = "Menu:FEES_STRUCTURE:Delete";
        public const string Export = "Menu:FEES_STRUCTURE:Export";
    }

    public static class FeesClassAmounts
    {
        public const string View = "Menu:FEES_CLASS_AMOUNTS:View";
        public const string Add = "Menu:FEES_CLASS_AMOUNTS:Add";
        public const string Edit = "Menu:FEES_CLASS_AMOUNTS:Edit";
        public const string Delete = "Menu:FEES_CLASS_AMOUNTS:Delete";
        public const string Export = "Menu:FEES_CLASS_AMOUNTS:Export";
    }

    public static class FeesCollection
    {
        public const string View = "Menu:FEES_COLLECTION:View";
        public const string Add = "Menu:FEES_COLLECTION:Add";
        public const string Edit = "Menu:FEES_COLLECTION:Edit";
        public const string Delete = "Menu:FEES_COLLECTION:Delete";
        public const string Export = "Menu:FEES_COLLECTION:Export";
    }

    public static class SalaryStructure
    {
        public const string View = "Menu:SALARY_STRUCTURE:View";
        public const string Add = "Menu:SALARY_STRUCTURE:Add";
        public const string Edit = "Menu:SALARY_STRUCTURE:Edit";
        public const string Delete = "Menu:SALARY_STRUCTURE:Delete";
        public const string Export = "Menu:SALARY_STRUCTURE:Export";
    }

    public static class SalaryEmployees
    {
        public const string View = "Menu:SALARY_EMPLOYEES:View";
        public const string Add = "Menu:SALARY_EMPLOYEES:Add";
        public const string Edit = "Menu:SALARY_EMPLOYEES:Edit";
        public const string Delete = "Menu:SALARY_EMPLOYEES:Delete";
        public const string Export = "Menu:SALARY_EMPLOYEES:Export";
    }

    public static class SalaryPayroll
    {
        public const string View = "Menu:SALARY_PAYROLL:View";
        public const string Add = "Menu:SALARY_PAYROLL:Add";
        public const string Edit = "Menu:SALARY_PAYROLL:Edit";
        public const string Delete = "Menu:SALARY_PAYROLL:Delete";
        public const string Export = "Menu:SALARY_PAYROLL:Export";
    }
}
