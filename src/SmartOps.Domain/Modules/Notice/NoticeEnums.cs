namespace SmartOps.Domain.Modules.Notice;

public enum NoticeStatus : short
{
    Draft = 0,
    Published = 1,
    Closed = 2
}

public enum NoticeContentType : short
{
    Announcement = 1,
    Form = 2,
    FeeReminder = 3,
    Document = 4
}

public enum NoticeTargetType : short
{
    AllStaff = 1,
    ClassParents = 2,
    SingleUser = 3,
    SingleParent = 4,
    PendingFeeParents = 5,
    SingleTeacher = 6
}
