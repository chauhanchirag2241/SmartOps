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

    public static class AcademicSetup
    {
        public const string View = "Menu:ACADEMIC_SETUP:View";
        public const string Add = "Menu:ACADEMIC_SETUP:Add";
        public const string Edit = "Menu:ACADEMIC_SETUP:Edit";
        public const string Delete = "Menu:ACADEMIC_SETUP:Delete";
        public const string Export = "Menu:ACADEMIC_SETUP:Export";
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

    public static class Employees
    {
        public const string View = "Menu:EMPLOYEES:View";
        public const string Add = "Menu:EMPLOYEES:Add";
        public const string Edit = "Menu:EMPLOYEES:Edit";
        public const string Delete = "Menu:EMPLOYEES:Delete";
        public const string Export = "Menu:EMPLOYEES:Export";
    }

    [Obsolete("Use Employees instead.")]
    public static class Teachers
    {
        public const string View = Employees.View;
        public const string Add = Employees.Add;
        public const string Edit = Employees.Edit;
        public const string Delete = Employees.Delete;
        public const string Export = Employees.Export;
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

    public static class AcademicPeriods
    {
        public const string View = "Menu:ACADEMIC_PERIODS:View";
        public const string Add = "Menu:ACADEMIC_PERIODS:Add";
        public const string Edit = "Menu:ACADEMIC_PERIODS:Edit";
        public const string Delete = "Menu:ACADEMIC_PERIODS:Delete";
        public const string Export = "Menu:ACADEMIC_PERIODS:Export";
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

    public static class LeaveStaff
    {
        public const string View = "Menu:LEAVE_STAFF:View";
        public const string Add = "Menu:LEAVE_STAFF:Add";
        public const string Edit = "Menu:LEAVE_STAFF:Edit";
        public const string Delete = "Menu:LEAVE_STAFF:Delete";
        public const string Export = "Menu:LEAVE_STAFF:Export";
    }

    public static class LeaveStudent
    {
        public const string View = "Menu:LEAVE_STUDENT:View";
        public const string Add = "Menu:LEAVE_STUDENT:Add";
        public const string Edit = "Menu:LEAVE_STUDENT:Edit";
        public const string Delete = "Menu:LEAVE_STUDENT:Delete";
        public const string Export = "Menu:LEAVE_STUDENT:Export";
    }

    public static class MyActions
    {
        public const string View = "Menu:MY_ACTIONS:View";
        public const string Add = "Menu:MY_ACTIONS:Add";
        public const string Edit = "Menu:MY_ACTIONS:Edit";
        public const string Delete = "Menu:MY_ACTIONS:Delete";
        public const string Export = "Menu:MY_ACTIONS:Export";
    }

    public static class Notices
    {
        public const string View = "Menu:NOTICES:View";
        public const string Add = "Menu:NOTICES:Add";
        public const string Edit = "Menu:NOTICES:Edit";
        public const string Delete = "Menu:NOTICES:Delete";
        public const string Export = "Menu:NOTICES:Export";
    }

    public static class FrontOffice
    {
        public const string View = "Menu:FRONT_OFFICE:View";
        public const string Add = "Menu:FRONT_OFFICE:Add";
        public const string Edit = "Menu:FRONT_OFFICE:Edit";
        public const string Delete = "Menu:FRONT_OFFICE:Delete";
        public const string Export = "Menu:FRONT_OFFICE:Export";
    }

    public static class VisitorBook
    {
        public const string View = "Menu:VISITOR_BOOK:View";
        public const string Add = "Menu:VISITOR_BOOK:Add";
        public const string Edit = "Menu:VISITOR_BOOK:Edit";
        public const string Delete = "Menu:VISITOR_BOOK:Delete";
        public const string Export = "Menu:VISITOR_BOOK:Export";
    }

    public static class PhoneLogs
    {
        public const string View = "Menu:PHONE_LOGS:View";
        public const string Add = "Menu:PHONE_LOGS:Add";
        public const string Edit = "Menu:PHONE_LOGS:Edit";
        public const string Delete = "Menu:PHONE_LOGS:Delete";
        public const string Export = "Menu:PHONE_LOGS:Export";
    }

    public static class Complaints
    {
        public const string View = "Menu:COMPLAINTS:View";
        public const string Add = "Menu:COMPLAINTS:Add";
        public const string Edit = "Menu:COMPLAINTS:Edit";
        public const string Delete = "Menu:COMPLAINTS:Delete";
        public const string Export = "Menu:COMPLAINTS:Export";
    }

    public static class AdmissionInquiries
    {
        public const string View = "Menu:ADMISSION_INQUIRIES:View";
        public const string Add = "Menu:ADMISSION_INQUIRIES:Add";
        public const string Edit = "Menu:ADMISSION_INQUIRIES:Edit";
        public const string Delete = "Menu:ADMISSION_INQUIRIES:Delete";
        public const string Export = "Menu:ADMISSION_INQUIRIES:Export";
    }

    public static class FrontOfficeSetup
    {
        public const string View = "Menu:FRONT_OFFICE_SETUP:View";
        public const string Add = "Menu:FRONT_OFFICE_SETUP:Add";
        public const string Edit = "Menu:FRONT_OFFICE_SETUP:Edit";
        public const string Delete = "Menu:FRONT_OFFICE_SETUP:Delete";
        public const string Export = "Menu:FRONT_OFFICE_SETUP:Export";
    }

    public static class ExamGroups
    {
        public const string View = "Menu:EXAM_GROUPS:View";
        public const string Add = "Menu:EXAM_GROUPS:Add";
        public const string Edit = "Menu:EXAM_GROUPS:Edit";
        public const string Delete = "Menu:EXAM_GROUPS:Delete";
        public const string Export = "Menu:EXAM_GROUPS:Export";
    }

    public static class Exams
    {
        public const string View = "Menu:EXAMS:View";
        public const string Add = "Menu:EXAMS:Add";
        public const string Edit = "Menu:EXAMS:Edit";
        public const string Delete = "Menu:EXAMS:Delete";
        public const string Export = "Menu:EXAMS:Export";
    }

    public static class ExamSchedule
    {
        public const string View = "Menu:EXAM_SCHEDULE:View";
        public const string Add = "Menu:EXAM_SCHEDULE:Add";
        public const string Edit = "Menu:EXAM_SCHEDULE:Edit";
        public const string Delete = "Menu:EXAM_SCHEDULE:Delete";
        public const string Export = "Menu:EXAM_SCHEDULE:Export";
    }

    public static class ExamMarksEntry
    {
        public const string View = "Menu:EXAM_MARKS_ENTRY:View";
        public const string Add = "Menu:EXAM_MARKS_ENTRY:Add";
        public const string Edit = "Menu:EXAM_MARKS_ENTRY:Edit";
        public const string Delete = "Menu:EXAM_MARKS_ENTRY:Delete";
        public const string Export = "Menu:EXAM_MARKS_ENTRY:Export";
    }

    public static class ExamResults
    {
        public const string View = "Menu:EXAM_RESULTS:View";
        public const string Add = "Menu:EXAM_RESULTS:Add";
        public const string Edit = "Menu:EXAM_RESULTS:Edit";
        public const string Delete = "Menu:EXAM_RESULTS:Delete";
        public const string Export = "Menu:EXAM_RESULTS:Export";
    }

    public static class ExamHallTickets
    {
        public const string View = "Menu:EXAM_HALL_TICKETS:View";
        public const string Add = "Menu:EXAM_HALL_TICKETS:Add";
        public const string Edit = "Menu:EXAM_HALL_TICKETS:Edit";
        public const string Delete = "Menu:EXAM_HALL_TICKETS:Delete";
        public const string Export = "Menu:EXAM_HALL_TICKETS:Export";
    }

    public static class ExamGradeSetup
    {
        public const string View = "Menu:EXAM_GRADE_SETUP:View";
        public const string Add = "Menu:EXAM_GRADE_SETUP:Add";
        public const string Edit = "Menu:EXAM_GRADE_SETUP:Edit";
        public const string Delete = "Menu:EXAM_GRADE_SETUP:Delete";
        public const string Export = "Menu:EXAM_GRADE_SETUP:Export";
    }

    public static class Timetable
    {
        public const string View = "Menu:TIMETABLE:View";
        public const string Add = "Menu:TIMETABLE:Add";
        public const string Edit = "Menu:TIMETABLE:Edit";
        public const string Delete = "Menu:TIMETABLE:Delete";
        public const string Export = "Menu:TIMETABLE:Export";
    }

    public static class PeriodMaster
    {
        public const string View = "Menu:PERIOD_MASTER:View";
        public const string Add = "Menu:PERIOD_MASTER:Add";
        public const string Edit = "Menu:PERIOD_MASTER:Edit";
        public const string Delete = "Menu:PERIOD_MASTER:Delete";
        public const string Export = "Menu:PERIOD_MASTER:Export";
    }

    public static class ClassTimetable
    {
        public const string View = "Menu:CLASS_TIMETABLE:View";
        public const string Add = "Menu:CLASS_TIMETABLE:Add";
        public const string Edit = "Menu:CLASS_TIMETABLE:Edit";
        public const string Delete = "Menu:CLASS_TIMETABLE:Delete";
        public const string Export = "Menu:CLASS_TIMETABLE:Export";
    }

    public static class MyTimetable
    {
        public const string View = "Menu:MY_TIMETABLE:View";
        public const string Add = "Menu:MY_TIMETABLE:Add";
        public const string Edit = "Menu:MY_TIMETABLE:Edit";
        public const string Delete = "Menu:MY_TIMETABLE:Delete";
        public const string Export = "Menu:MY_TIMETABLE:Export";
    }

    /// <summary>
    /// Complaints.View OR AdmissionInquiries.View OR VisitorBook.View OR PhoneLogs.View OR FrontOfficeSetup.View
    /// — employee dropdown for front-office assign fields.
    /// </summary>
    public const string FrontOfficeEmployeeLookup = "Menu:FRONT_OFFICE:EmployeeLookup";
}
