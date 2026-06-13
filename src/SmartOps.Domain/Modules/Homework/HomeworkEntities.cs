using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Homework;

public class HomeworkEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly AssignDate { get; set; }
    public DateOnly DueDate { get; set; }
    public HomeworkPriority Priority { get; set; }
    public int? Marks { get; set; }
    public HomeworkSubmissionType SubmissionType { get; set; }
}

public class HomeworkDetailEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid HomeworkId { get; set; }
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid StudentId { get; set; }
    public HomeworkSubmissionStatus Status { get; set; }
    public DateOnly? SubmittedOn { get; set; }
    public int? Marks { get; set; }
    public string? Remark { get; set; }
}
