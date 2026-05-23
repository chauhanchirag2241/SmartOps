namespace SmartOps.Domain.Modules.Salary;

public enum SalaryComponentType
{
    Earning = 0,
    Deduction = 1
}

public enum SalaryCalculationType
{
    PercentOfBasic = 0,
    PercentOfGross = 1,
    Fixed = 2
}

public enum SalaryStructureVersionStatus
{
    Draft = 0,
    Published = 1,
    Active = 2,
    Archived = 3
}

public enum PayrollRunStatus
{
    Draft = 0,
    Processed = 1
}

public enum PayrollEntryStatus
{
    Draft = 0,
    Processed = 1,
    Paid = 2
}
