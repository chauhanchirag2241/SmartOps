namespace SmartOps.Domain.Modules.Notice;

public enum NoticeStatus : short
{
    Draft = 0,
    Published = 1,
    Closed = 2
}

public enum NoticeTargetType : short
{
    AllStaff = 1,
    ClassParents = 2,
    SingleUser = 3
}
