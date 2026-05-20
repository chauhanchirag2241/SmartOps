namespace SmartOps.Domain.Common.Enums;

public enum DataScopeType
{
    None = 0,
    Global = 1,
    Department = 2,
    Class = 3,
    SubjectClass = 4,
    Self = 5,
    LinkedStudents = 6,
    ModuleOnly = 7,
    Custom = 8
}

public enum AccessLevel
{
    View,
    Edit,
    Delete
}
