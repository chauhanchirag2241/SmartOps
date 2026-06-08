namespace SmartOps.Domain.Modules.Workflow;

public enum WorkflowItemType : short
{
    LeaveApproval = 1,
    NoticeResponse = 2,
    FormFill = 3
}

public enum WorkflowItemStatus : short
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
    Expired = 3
}

public enum WorkflowReferenceType : short
{
    LeaveRequest = 1,
    Notice = 2,
    FormInstance = 3
}

public static class WorkflowActionCodes
{
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string Respond = "Respond";
    public const string Acknowledge = "Acknowledge";
    public const string SubmitForm = "SubmitForm";
}
