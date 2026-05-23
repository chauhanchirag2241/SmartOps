using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary;

public static class SalaryLabelHelper
{
    public static string ComponentTypeLabel(SalaryComponentType type) => type switch
    {
        SalaryComponentType.Earning => "Earning",
        SalaryComponentType.Deduction => "Deduction",
        _ => type.ToString()
    };

    public static string CalculationTypeLabel(SalaryCalculationType type) => type switch
    {
        SalaryCalculationType.PercentOfBasic => "% of basic",
        SalaryCalculationType.PercentOfGross => "% of gross",
        SalaryCalculationType.Fixed => "Fixed amount",
        _ => type.ToString()
    };

    public static string PayrollRunStatusLabel(PayrollRunStatus status) => status switch
    {
        PayrollRunStatus.Draft => "Draft",
        PayrollRunStatus.Processed => "Processed",
        _ => status.ToString()
    };

    public static string PayrollEntryStatusLabel(PayrollEntryStatus status) => status switch
    {
        PayrollEntryStatus.Draft => "Draft",
        PayrollEntryStatus.Processed => "Processed",
        PayrollEntryStatus.Paid => "Paid",
        _ => status.ToString()
    };

    public static string VersionStatusLabel(SalaryStructureVersionStatus status) => status switch
    {
        SalaryStructureVersionStatus.Draft => "Draft",
        SalaryStructureVersionStatus.Published => "Published",
        SalaryStructureVersionStatus.Active => "Active",
        SalaryStructureVersionStatus.Archived => "Archived",
        _ => status.ToString()
    };
}
