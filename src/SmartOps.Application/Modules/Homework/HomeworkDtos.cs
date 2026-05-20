using SmartOps.Domain.Modules.Homework;

namespace SmartOps.Application.Modules.Homework;

public record CreateHomeworkRequestDto(
    Guid ClassId,
    Guid SubjectId,
    string Title,
    string? Description,
    DateOnly AssignDate,
    DateOnly DueDate,
    HomeworkPriority Priority,
    int? Marks,
    HomeworkSubmissionType SubmissionType
);

public record UpdateHomeworkRequestDto(
    Guid ClassId,
    Guid SubjectId,
    string Title,
    string? Description,
    DateOnly AssignDate,
    DateOnly DueDate,
    HomeworkPriority Priority,
    int? Marks,
    HomeworkSubmissionType SubmissionType
);

public record HomeworkListItemDto(
    Guid Id,
    string Title,
    string? Description,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    DateOnly AssignDate,
    DateOnly DueDate,
    HomeworkPriority Priority,
    string PriorityLabel,
    int? Marks,
    HomeworkSubmissionType SubmissionType,
    string SubmissionTypeLabel,
    string Status,
    int Submitted,
    int Pending,
    int Late,
    int Total
);

public record HomeworkStatsDto(
    int TotalAssigned,
    int DueToday,
    int TotalSubmissions,
    int Overdue
);

public record HomeworkStudentSubmissionDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    HomeworkSubmissionStatus Status,
    string StatusLabel,
    DateOnly? SubmittedOn,
    int? Marks,
    string? Remark
);

public record HomeworkDetailResponseDto(
    Guid Id,
    string Title,
    string? Description,
    Guid ClassId,
    string ClassName,
    Guid SubjectId,
    string SubjectName,
    DateOnly AssignDate,
    DateOnly DueDate,
    HomeworkPriority Priority,
    string PriorityLabel,
    int? Marks,
    HomeworkSubmissionType SubmissionType,
    string SubmissionTypeLabel,
    string Status,
    int Submitted,
    int Pending,
    int Late,
    int Total,
    bool IsSubmissionsSubmitted,
    IList<HomeworkStudentSubmissionDto> Students
);

public record StudentHomeworkSubmissionItemDto(
    Guid StudentId,
    HomeworkSubmissionStatus Status,
    DateOnly? SubmittedOn,
    int? Marks,
    string? Remark
);

public record SubmitHomeworkSubmissionsRequestDto(
    IList<StudentHomeworkSubmissionItemDto> Students
);

public record UpdateHomeworkSubmissionsRequestDto(
    IList<StudentHomeworkSubmissionItemDto> Students
);
