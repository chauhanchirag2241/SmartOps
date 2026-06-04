using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Workflow;

namespace SmartOps.Domain.Modules.Workflow.Entities;

public sealed class WorkflowItemEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid AssigneeUserId { get; set; }
    public WorkflowItemType ItemType { get; set; }
    public WorkflowItemStatus Status { get; set; }
    public WorkflowReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public DateOnly? DueDate { get; set; }
    public int Priority { get; set; }
    public string? PayloadJson { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public string? Outcome { get; set; }
}
