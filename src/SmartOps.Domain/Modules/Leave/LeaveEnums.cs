namespace SmartOps.Domain.Modules.Leave;

public enum LeaveRequestType : short
{
    Staff = 1,
    Student = 2
}

public enum LeaveRequestStatus : short
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum LeaveType : short
{
    Casual = 1,
    Sick = 2,
    Other = 3
}
