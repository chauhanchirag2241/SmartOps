namespace SmartOps.Domain.Modules.FrontOffice;

public enum CallType : short
{
    Incoming = 0,
    Outgoing = 1
}

public enum ComplaintStatus : short
{
    Pending = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum InquiryStatus : short
{
    New = 0,
    FollowUp = 1,
    VisitScheduled = 2,
    AdmissionForm = 3,
    Enrolled = 4,
    NotInterested = 5
}
