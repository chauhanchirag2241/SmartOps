namespace SmartOps.Domain.Modules.Homework;

public enum HomeworkSubmissionStatus
{
    Pending = 0,
    Submitted = 1,
    Late = 2
}

public enum HomeworkPriority
{
    Normal = 0,
    High = 1,
    Low = 2
}

public enum HomeworkSubmissionType
{
    Physical = 0,
    Online = 1,
    Both = 2
}

public static class HomeworkEnumExtensions
{
    public static string ToDisplayString(this HomeworkSubmissionStatus status) =>
        status switch
        {
            HomeworkSubmissionStatus.Pending => "Pending",
            HomeworkSubmissionStatus.Submitted => "Submitted",
            HomeworkSubmissionStatus.Late => "Late",
            _ => "Pending"
        };

    public static string ToDisplayString(this HomeworkPriority priority) =>
        priority switch
        {
            HomeworkPriority.High => "High",
            HomeworkPriority.Low => "Low",
            _ => "Normal"
        };

    public static string ToDisplayString(this HomeworkSubmissionType type) =>
        type switch
        {
            HomeworkSubmissionType.Online => "Online (portal upload)",
            HomeworkSubmissionType.Both => "Both",
            _ => "Physical (in class)"
        };
}
