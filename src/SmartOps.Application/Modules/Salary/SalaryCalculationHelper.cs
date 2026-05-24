using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary;

public sealed class SalaryBreakdown
{
    public decimal BasicSalary { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetSalary { get; init; }
    public IList<SalaryLineItemDto> Earnings { get; init; } = [];
    public IList<SalaryLineItemDto> Deductions { get; init; } = [];
}

public static class SalaryCalculationHelper
{
    public static IList<SalaryVersionComponentEntity> MergeEmployeeValues(
        IList<SalaryVersionComponentEntity> versionComponents,
        IList<EmployeeSalaryComponentEntity> employeeValues)
    {
        return versionComponents
            .Where(c => c.IsActive)
            .Select(master =>
            {
                EmployeeSalaryComponentEntity? row = employeeValues
                    .FirstOrDefault(v => v.SalaryVersionComponentId == master.Id && v.IsActive);
                return new SalaryVersionComponentEntity
                {
                    Id = master.Id,
                    SalaryStructureVersionId = master.SalaryStructureVersionId,
                    Name = master.Name,
                    ShortCode = master.ShortCode,
                    ComponentType = master.ComponentType,
                    CalculationType = master.CalculationType,
                    Value = row?.Value ?? master.Value,
                    IsTaxable = master.IsTaxable,
                    IsActive = true
                };
            })
            .ToList();
    }

    public static SalaryBreakdown Calculate(IList<SalaryVersionComponentEntity> components)
    {
        var earnings = new List<SalaryLineItemDto>();
        var deductions = new List<SalaryLineItemDto>();

        IList<SalaryVersionComponentEntity> activeComponents = components
            .Where(c => c.IsActive)
            .OrderBy(c => c.ComponentType)
            .ThenBy(c => c.CalculationType)
            .ThenBy(c => c.Name)
            .ToList();

        decimal basicBase = ResolveBasicBase(activeComponents);

        foreach (SalaryVersionComponentEntity component in activeComponents.Where(c =>
                     c.ComponentType == SalaryComponentType.Earning
                     && c.CalculationType is SalaryCalculationType.PercentOfBasic or SalaryCalculationType.Fixed))
        {
            decimal amount = ResolveAmount(component, basicBase, SumEarnings(earnings));
            if (amount <= 0)
            {
                continue;
            }

            earnings.Add(ToLineItem(component, amount));
            if (IsBasicAnchor(component))
            {
                basicBase = amount;
            }
        }

        decimal gross = SumEarnings(earnings);

        foreach (SalaryVersionComponentEntity component in activeComponents.Where(c =>
                     c.ComponentType == SalaryComponentType.Earning && c.CalculationType == SalaryCalculationType.PercentOfGross))
        {
            decimal amount = ResolveAmount(component, basicBase, gross);
            if (amount <= 0)
            {
                continue;
            }

            earnings.Add(ToLineItem(component, amount));
            gross = SumEarnings(earnings);
        }

        foreach (SalaryVersionComponentEntity component in activeComponents.Where(c =>
                     c.ComponentType == SalaryComponentType.Deduction
                     && c.CalculationType is SalaryCalculationType.PercentOfBasic or SalaryCalculationType.Fixed))
        {
            decimal amount = ResolveAmount(component, basicBase, gross);
            if (amount <= 0)
            {
                continue;
            }

            deductions.Add(ToLineItem(component, amount));
        }

        foreach (SalaryVersionComponentEntity component in activeComponents.Where(c =>
                     c.ComponentType == SalaryComponentType.Deduction && c.CalculationType == SalaryCalculationType.PercentOfGross))
        {
            decimal amount = ResolveAmount(component, basicBase, gross);
            if (amount <= 0)
            {
                continue;
            }

            deductions.Add(ToLineItem(component, amount));
        }

        decimal totalDeductions = Round(deductions.Sum(d => d.Amount));
        decimal net = Round(gross - totalDeductions);
        decimal reportedBasic = earnings
            .FirstOrDefault(e => IsBasicLineName(e.Name))?.Amount ?? basicBase;

        return new SalaryBreakdown
        {
            BasicSalary = reportedBasic,
            GrossSalary = gross,
            TotalDeductions = totalDeductions,
            NetSalary = net,
            Earnings = earnings,
            Deductions = deductions
        };
    }

    public static decimal EstimateGross(IList<SalaryVersionComponentEntity> components) =>
        Calculate(components).GrossSalary;

    private static decimal ResolveBasicBase(IList<SalaryVersionComponentEntity> active)
    {
        SalaryVersionComponentEntity? basicComponent = active.FirstOrDefault(c =>
            c.ComponentType == SalaryComponentType.Earning
            && IsBasicAnchor(c)
            && c.CalculationType == SalaryCalculationType.Fixed);

        return basicComponent is null ? 0 : Round(basicComponent.Value);
    }

    private static bool IsBasicAnchor(SalaryVersionComponentEntity component)
    {
        if (!string.IsNullOrWhiteSpace(component.ShortCode))
        {
            string code = component.ShortCode.Trim();
            if (code.Equals("BASIC", StringComparison.OrdinalIgnoreCase)
                || code.Equals("BASE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        string normalized = NormalizeComponentName(component.Name);
        return normalized is "basic" or "base" or "basepay" or "basesalary"
            || normalized.Contains("basic", StringComparison.Ordinal)
            || normalized.Contains("basepay", StringComparison.Ordinal);
    }

    private static bool IsBasicLineName(string name) =>
        IsBasicAnchor(new SalaryVersionComponentEntity { Name = name });

    private static string NormalizeComponentName(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static decimal ResolveAmount(SalaryVersionComponentEntity component, decimal basic, decimal gross) =>
        component.CalculationType switch
        {
            SalaryCalculationType.PercentOfBasic => Round(basic * component.Value / 100m),
            SalaryCalculationType.PercentOfGross => Round(gross * component.Value / 100m),
            SalaryCalculationType.Fixed => Round(component.Value),
            _ => 0
        };

    private static SalaryLineItemDto ToLineItem(SalaryVersionComponentEntity component, decimal amount) => new(
        component.Id,
        component.Name,
        component.ComponentType,
        SalaryLabelHelper.ComponentTypeLabel(component.ComponentType),
        amount,
        component.ComponentType == SalaryComponentType.Earning);

    private static decimal SumEarnings(IEnumerable<SalaryLineItemDto> earnings) =>
        Round(earnings.Sum(e => e.Amount));

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
