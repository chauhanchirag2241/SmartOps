namespace SmartOps.Domain.Modules.Exam;

public enum ExamStatus
{
    Draft = 0,
    Scheduled = 1,
    Ongoing = 2,
    Completed = 3,
    ResultDeclared = 4
}

public enum ExamEvaluationType
{
    Marks = 0,
    Grade = 1,
    Both = 2
}

public enum ExamResultStatus
{
    Pending = 0,
    Pass = 1,
    Fail = 2,
    Absent = 3
}

public static class ExamEnumExtensions
{
    public static string ToDisplayString(this ExamStatus status) =>
        status switch
        {
            ExamStatus.Draft => "Draft",
            ExamStatus.Scheduled => "Scheduled",
            ExamStatus.Ongoing => "Ongoing",
            ExamStatus.Completed => "Completed",
            ExamStatus.ResultDeclared => "Result Declared",
            _ => "Draft"
        };

    public static string ToDisplayString(this ExamEvaluationType type) =>
        type switch
        {
            ExamEvaluationType.Grade => "Grade",
            ExamEvaluationType.Both => "Marks & Grade",
            _ => "Marks"
        };

    public static string ToDisplayString(this ExamResultStatus status) =>
        status switch
        {
            ExamResultStatus.Pass => "Pass",
            ExamResultStatus.Fail => "Fail",
            ExamResultStatus.Absent => "Absent",
            _ => "Pending"
        };
}
