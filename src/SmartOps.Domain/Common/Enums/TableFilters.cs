namespace SmartOps.Domain.Common.Enums;

/// <summary>
/// Filters for Student Table
/// </summary>
public enum StudentFilter
{
    All = 0,
    Active = 1,
    Inactive = 2,
    FeeOverdue = 3
}

/// <summary>
/// Filters for Staff Table (Example for future use)
/// </summary>
public enum StaffFilter
{
    All = 0,
    Active = 1,
    Inactive = 2,
    OnLeave = 3
}

/// <summary>
/// Filters for Fees/Finance Table (Example for future use)
/// </summary>
public enum FeeFilter
{
    All = 0,
    Paid = 1,
    Pending = 2,
    Overdue = 3
}

/// <summary>
/// Filters for Class Table
/// </summary>
public enum ClassFilter
{
    All = 0,
    Active = 1,
    Inactive = 2
}

/// <summary>
/// Filters for School configuration table.
/// </summary>
public enum SchoolFilter
{
    All = 0,
    Active = 1,
    Inactive = 2
}
