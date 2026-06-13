using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Infrastructure.Modules.Salary.Services;

public sealed class EmployeeSalaryService : IEmployeeSalaryService
{
    private readonly IEmployeeSalaryRepository _employeeRepo;
    private readonly ISalaryStructureRepository _structureRepo;

    public EmployeeSalaryService(IEmployeeSalaryRepository employeeRepo, ISalaryStructureRepository structureRepo)
    {
        _employeeRepo = employeeRepo;
        _structureRepo = structureRepo;
    }

    public async Task<Result<IList<EmployeeSalaryListItemDto>>> GetEmployeesAsync(
        string? search,
        Guid? departmentId,
        string? designation,
        CancellationToken ct = default)
    {
        IList<EmployeeSalaryListRow> rows = await _employeeRepo
            .GetEmployeeSalariesAsync(search, departmentId, designation, ct)
            .ConfigureAwait(false);

        IList<EmployeeSalaryListItemDto> dtos = [];
        foreach (EmployeeSalaryListRow row in rows)
        {
            decimal? net = null;
            if (row.EmployeeSalaryId.HasValue && row.SalaryStructureVersionId.HasValue)
            {
                IList<SalaryVersionComponentListRow> versionComponents =
                    await _structureRepo.GetComponentsAsync(row.SalaryStructureVersionId.Value, ct).ConfigureAwait(false);
                IList<EmployeeSalaryComponentEntity> values =
                    await _employeeRepo.GetComponentValuesForAssignmentAsync(row.EmployeeSalaryId.Value, ct).ConfigureAwait(false);
                IList<SalaryVersionComponentEntity> merged = SalaryCalculationHelper.MergeEmployeeValues(
                    MapVersionComponents(versionComponents),
                    values);
                net = SalaryCalculationHelper.Calculate(merged).NetSalary;
            }

            dtos.Add(new EmployeeSalaryListItemDto(
                row.EmployeeRecordId,
                row.EmployeeName,
                row.EmployeeId,
                string.IsNullOrWhiteSpace(row.Department) ? null : row.Department,
                row.Designation,
                net,
                row.EmployeeSalaryId.HasValue));
        }

        return Result<IList<EmployeeSalaryListItemDto>>.Success(dtos);
    }

    public async Task<Result<EmployeeSalaryDetailDto>> GetEmployeeDetailAsync(Guid employeeId, CancellationToken ct = default)
    {
        EmployeeSalaryContextRow? context = await _employeeRepo.GetEmployeeSalaryContextAsync(employeeId, ct).ConfigureAwait(false);
        if (context is null)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Employee not found.");
        }

        IList<SalaryVersionComponentListRow> versionComponents = context.SalaryStructureVersionId.HasValue
            ? await _structureRepo.GetComponentsAsync(context.SalaryStructureVersionId.Value, ct).ConfigureAwait(false)
            : [];

        IList<EmployeeSalaryComponentEntity> values = context.EmployeeSalaryId.HasValue
            ? await _employeeRepo.GetComponentValuesForAssignmentAsync(context.EmployeeSalaryId.Value, ct).ConfigureAwait(false)
            : [];

        return Result<EmployeeSalaryDetailDto>.Success(MapDetail(context, versionComponents, values));
    }

    public async Task<Result<EmployeeSalaryDetailDto>> AssignOrUpdateAsync(
        Guid employeeId,
        AssignEmployeeSalaryRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Request body is required.");
        }

        if (request.SalaryStructureVersionId == Guid.Empty)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Salary structure version is required.");
        }

        if (request.Components is null || request.Components.Count == 0)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("At least one salary component value is required.");
        }

        if (request.Components.Any(c => c.Value <= 0))
        {
            return Result<EmployeeSalaryDetailDto>.Failure("All component values must be greater than zero.");
        }

        SalaryStructureVersionEntity? version = await _structureRepo.GetVersionByIdAsync(request.SalaryStructureVersionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Salary structure version not found.");
        }

        if (version.Status is not (SalaryStructureVersionStatus.Published or SalaryStructureVersionStatus.Active))
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Assign salary only from a published or active structure version.");
        }

        EmployeeSalaryContextRow? employee = await _employeeRepo.GetEmployeeSalaryContextAsync(employeeId, ct).ConfigureAwait(false);
        if (employee is null)
        {
            return Result<EmployeeSalaryDetailDto>.Failure("Employee not found.");
        }

        IList<SalaryVersionComponentListRow> versionComponents =
            await _structureRepo.GetComponentsAsync(request.SalaryStructureVersionId, ct).ConfigureAwait(false);
        HashSet<Guid> validIds = versionComponents.Select(m => m.Id).ToHashSet();
        if (request.Components.Any(c => !validIds.Contains(c.SalaryVersionComponentId)))
        {
            return Result<EmployeeSalaryDetailDto>.Failure("One or more salary components are invalid for this structure version.");
        }

        EmployeeSalaryEntity? activeAssignment =
            await _employeeRepo.GetActiveAssignmentByEmployeeIdAsync(employeeId, ct).ConfigureAwait(false);

        Guid assignmentId;
        if (activeAssignment is null)
        {
            var assignment = new EmployeeSalaryEntity
            {
                EmployeeId = employeeId,
                SalaryStructureVersionId = request.SalaryStructureVersionId,
                EffectiveDate = request.EffectiveDate
            };
            assignmentId = await _employeeRepo.CreateAssignmentAsync(assignment, ct).ConfigureAwait(false);
        }
        else
        {
            activeAssignment.SalaryStructureVersionId = request.SalaryStructureVersionId;
            activeAssignment.EffectiveDate = request.EffectiveDate;
            await _employeeRepo.UpdateAssignmentAsync(activeAssignment, ct).ConfigureAwait(false);
            assignmentId = activeAssignment.Id;
        }

        IList<EmployeeSalaryComponentEntity> valueRows = request.Components
            .Select(c => new EmployeeSalaryComponentEntity
            {
                EmployeeSalaryId = assignmentId,
                SalaryVersionComponentId = c.SalaryVersionComponentId,
                Value = c.Value
            })
            .ToList();

        await _employeeRepo.ReplaceComponentValuesAsync(assignmentId, valueRows, ct).ConfigureAwait(false);
        return await GetEmployeeDetailAsync(employeeId, ct).ConfigureAwait(false);
    }

    private static EmployeeSalaryDetailDto MapDetail(
        EmployeeSalaryContextRow context,
        IList<SalaryVersionComponentListRow> versionComponents,
        IList<EmployeeSalaryComponentEntity> employeeValues)
    {
        IList<EmployeeSalaryComponentItemDto> componentItems = versionComponents.Select(master =>
        {
            EmployeeSalaryComponentEntity? row = employeeValues
                .FirstOrDefault(v => v.SalaryVersionComponentId == master.Id);
            decimal value = row?.Value ?? master.Value;
            return new EmployeeSalaryComponentItemDto(
                master.Id,
                master.Name,
                master.ShortCode,
                master.ComponentType,
                SalaryLabelHelper.ComponentTypeLabel(master.ComponentType),
                master.CalculationType,
                SalaryLabelHelper.CalculationTypeLabel(master.CalculationType),
                value,
                master.Value,
                master.IsTaxable);
        }).ToList();

        if (!context.EmployeeSalaryId.HasValue)
        {
            return new EmployeeSalaryDetailDto(
                context.EmployeeRecordId,
                context.EmployeeName,
                context.EmployeeId,
                string.IsNullOrWhiteSpace(context.Department) ? null : context.Department,
                context.Designation,
                null,
                null,
                0,
                0,
                0,
                0,
                context.EffectiveDate,
                componentItems,
                [],
                []);
        }

        IList<SalaryVersionComponentEntity> merged = SalaryCalculationHelper.MergeEmployeeValues(
            MapVersionComponents(versionComponents),
            employeeValues);
        SalaryBreakdown breakdown = SalaryCalculationHelper.Calculate(merged);
        return new EmployeeSalaryDetailDto(
            context.EmployeeRecordId,
            context.EmployeeName,
            context.EmployeeId,
            string.IsNullOrWhiteSpace(context.Department) ? null : context.Department,
            context.Designation,
            context.EmployeeSalaryId,
            context.SalaryStructureVersionId,
            breakdown.BasicSalary,
            breakdown.GrossSalary,
            breakdown.TotalDeductions,
            breakdown.NetSalary,
            context.EffectiveDate,
            componentItems,
            breakdown.Earnings,
            breakdown.Deductions);
    }

    private static IList<SalaryVersionComponentEntity> MapVersionComponents(IList<SalaryVersionComponentListRow> rows) =>
        rows.Select(r => new SalaryVersionComponentEntity
        {
            Id = r.Id,
            SalaryStructureVersionId = r.SalaryStructureVersionId,
            Name = r.Name,
            ShortCode = r.ShortCode,
            ComponentType = r.ComponentType,
            CalculationType = r.CalculationType,
            Value = r.Value,
            IsTaxable = r.IsTaxable,
            IsActive = r.IsActive
        }).ToList();
}
