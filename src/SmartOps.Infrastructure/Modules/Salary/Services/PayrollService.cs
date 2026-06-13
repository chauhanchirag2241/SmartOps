using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Infrastructure.Modules.Salary.Services;

public sealed class PayrollService : IPayrollService
{
    private readonly IPayrollRepository _payrollRepo;
    private readonly IEmployeeSalaryRepository _employeeRepo;
    private readonly ISalaryStructureRepository _structureRepo;

    public PayrollService(
        IPayrollRepository payrollRepo,
        IEmployeeSalaryRepository employeeRepo,
        ISalaryStructureRepository structureRepo)
    {
        _payrollRepo = payrollRepo;
        _employeeRepo = employeeRepo;
        _structureRepo = structureRepo;
    }

    public async Task<Result<PayrollRunDto>> GetPayrollAsync(int payYear, int payMonth, CancellationToken ct = default)
    {
        if (!IsValidPeriod(payYear, payMonth))
        {
            return Result<PayrollRunDto>.Failure("Invalid pay year or month.");
        }

        PayrollRunEntity? run = await _payrollRepo.GetRunByYearMonthAsync(payYear, payMonth, ct).ConfigureAwait(false);
        if (run is null)
        {
            return Result<PayrollRunDto>.Success(new PayrollRunDto(
                Guid.Empty,
                payYear,
                payMonth,
                PayrollRunStatus.Draft,
                SalaryLabelHelper.PayrollRunStatusLabel(PayrollRunStatus.Draft),
                false,
                0,
                0,
                0,
                0,
                null,
                []));
        }

        IList<PayrollEntryListRow> entries = await _payrollRepo.GetEntriesForRunAsync(run.Id, ct).ConfigureAwait(false);
        return Result<PayrollRunDto>.Success(MapRun(run, entries));
    }

    public async Task<Result<PayrollRunDto>> ProcessPayrollAsync(ProcessPayrollRequestDto request, CancellationToken ct = default)
    {
        if (!IsValidPeriod(request.PayYear, request.PayMonth))
        {
            return Result<PayrollRunDto>.Failure("Invalid pay year or month.");
        }

        PayrollRunEntity? existingRun = await _payrollRepo.GetRunByYearMonthAsync(request.PayYear, request.PayMonth, ct).ConfigureAwait(false);
        if (existingRun is not null && existingRun.Status == PayrollRunStatus.Processed)
        {
            return Result<PayrollRunDto>.Failure("Payroll for this period has already been processed.");
        }

        IList<EmployeeSalaryEntity> assignments = await _employeeRepo.GetActiveAssignmentsAsync(ct).ConfigureAwait(false);
        if (assignments.Count == 0)
        {
            return Result<PayrollRunDto>.Failure("No active employee salary assignments found.");
        }

        PayrollRunEntity run = existingRun ?? new PayrollRunEntity
        {
            PayYear = request.PayYear,
            PayMonth = request.PayMonth,
            Status = PayrollRunStatus.Draft
        };

        run.UseAttendanceWiseSalary = request.UseAttendanceWiseSalary;

        if (existingRun is null)
        {
            run.Id = await _payrollRepo.CreateRunAsync(run, ct).ConfigureAwait(false);
        }
        else
        {
            await _payrollRepo.DeleteEntriesForRunAsync(run.Id, ct).ConfigureAwait(false);
        }

        int daysInMonth = DateTime.DaysInMonth(request.PayYear, request.PayMonth);
        decimal totalGross = 0;
        decimal totalDeductions = 0;
        decimal totalNet = 0;
        int employeeCount = 0;

        foreach (EmployeeSalaryEntity assignment in assignments)
        {
            IList<EmployeeSalaryComponentEntity> values =
                await _employeeRepo.GetComponentValuesForAssignmentAsync(assignment.Id, ct).ConfigureAwait(false);
            if (values.Count == 0)
            {
                continue;
            }

            IList<SalaryVersionComponentListRow> versionComponentRows =
                await _structureRepo.GetComponentsAsync(assignment.SalaryStructureVersionId, ct).ConfigureAwait(false);
            IList<SalaryVersionComponentEntity> versionComponents = versionComponentRows.Select(r => new SalaryVersionComponentEntity
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

            IList<SalaryVersionComponentEntity> merged = SalaryCalculationHelper.MergeEmployeeValues(versionComponents, values);
            SalaryBreakdown breakdown = SalaryCalculationHelper.Calculate(merged);

            var entry = new PayrollEntryEntity
            {
                PayrollRunId = run.Id,
                EmployeeId = assignment.EmployeeId,
                BasicSalary = breakdown.BasicSalary,
                GrossSalary = breakdown.GrossSalary,
                TotalDeductions = breakdown.TotalDeductions,
                NetSalary = breakdown.NetSalary,
                Status = PayrollEntryStatus.Processed,
                WorkingDays = daysInMonth,
                PresentDays = daysInMonth
            };
            Guid entryId = await _payrollRepo.CreateEntryAsync(entry, ct).ConfigureAwait(false);

            IList<PayrollEntryLineEntity> lines = breakdown.Earnings
                .Concat(breakdown.Deductions)
                .Select(line => new PayrollEntryLineEntity
                {
                    PayrollEntryId = entryId,
                    SalaryVersionComponentId = line.ComponentId,
                    ComponentName = line.Name,
                    ComponentType = line.ComponentType,
                    Amount = line.Amount,
                    IsEarning = line.IsEarning
                })
                .ToList();
            await _payrollRepo.CreateEntryLinesAsync(lines, ct).ConfigureAwait(false);

            totalGross += breakdown.GrossSalary;
            totalDeductions += breakdown.TotalDeductions;
            totalNet += breakdown.NetSalary;
            employeeCount++;
        }

        if (employeeCount == 0)
        {
            return Result<PayrollRunDto>.Failure("No employees with salary component values found.");
        }

        run.Status = PayrollRunStatus.Processed;
        run.TotalGross = totalGross;
        run.TotalDeductions = totalDeductions;
        run.TotalNet = totalNet;
        run.EmployeeCount = employeeCount;
        run.ProcessedOn = DateTime.UtcNow;
        await _payrollRepo.UpdateRunAsync(run, ct).ConfigureAwait(false);

        IList<PayrollEntryListRow> entryRows = await _payrollRepo.GetEntriesForRunAsync(run.Id, ct).ConfigureAwait(false);
        return Result<PayrollRunDto>.Success(MapRun(run, entryRows));
    }

    public async Task<Result<bool>> MarkPaidAsync(Guid runId, MarkPayrollPaidRequestDto request, CancellationToken ct = default)
    {
        PayrollRunEntity? run = await _payrollRepo.GetRunByIdAsync(runId, ct).ConfigureAwait(false);
        if (run is null)
        {
            return Result<bool>.Failure("Payroll run not found.");
        }

        if (run.Status != PayrollRunStatus.Processed)
        {
            return Result<bool>.Failure("Only processed payroll can be marked as paid.");
        }

        await _payrollRepo.MarkEntriesPaidAsync(runId, request.EntryIds, ct).ConfigureAwait(false);
        return Result<bool>.Success(true);
    }

    public async Task<Result<PayslipDto>> GetPayslipAsync(Guid entryId, CancellationToken ct = default)
    {
        PayslipContextRow? context = await _payrollRepo.GetPayslipContextAsync(entryId, ct).ConfigureAwait(false);
        if (context is null)
        {
            return Result<PayslipDto>.Failure("Payroll entry not found.");
        }

        IList<PayrollEntryLineEntity> lines = await _payrollRepo.GetLinesForEntryAsync(entryId, ct).ConfigureAwait(false);
        IList<SalaryLineItemDto> earnings = lines.Where(l => l.IsEarning).Select(MapLine).ToList();
        IList<SalaryLineItemDto> deductions = lines.Where(l => !l.IsEarning).Select(MapLine).ToList();

        return Result<PayslipDto>.Success(new PayslipDto(
            context.EntryId,
            context.PayYear,
            context.PayMonth,
            context.EmployeeName,
            context.EmployeeId,
            string.IsNullOrWhiteSpace(context.Department) ? null : context.Department,
            context.Designation,
            context.WorkingDays,
            context.PresentDays,
            context.BasicSalary,
            context.GrossSalary,
            context.TotalDeductions,
            context.NetSalary,
            context.BankName,
            context.BankAccountNumber,
            context.BankIfscCode,
            earnings,
            deductions));
    }

    private static PayrollRunDto MapRun(PayrollRunEntity run, IList<PayrollEntryListRow> entries) => new(
        run.Id,
        run.PayYear,
        run.PayMonth,
        run.Status,
        SalaryLabelHelper.PayrollRunStatusLabel(run.Status),
        run.UseAttendanceWiseSalary,
        run.TotalGross,
        run.TotalDeductions,
        run.TotalNet,
        run.EmployeeCount,
        run.ProcessedOn,
        entries.Select(MapEntry).ToList());

    private static PayrollEntryListItemDto MapEntry(PayrollEntryListRow row) => new(
        row.Id,
        row.EmployeeRecordId,
        row.EmployeeName,
        string.IsNullOrWhiteSpace(row.Department) ? null : row.Department,
        row.BasicSalary,
        row.HraAmount,
        row.Allowances,
        row.GrossSalary,
        row.TotalDeductions,
        row.NetSalary,
        row.Status,
        SalaryLabelHelper.PayrollEntryStatusLabel(row.Status));

    private static SalaryLineItemDto MapLine(PayrollEntryLineEntity line) => new(
        line.SalaryVersionComponentId,
        line.ComponentName,
        line.ComponentType,
        SalaryLabelHelper.ComponentTypeLabel(line.ComponentType),
        line.Amount,
        line.IsEarning);

    private static bool IsValidPeriod(int payYear, int payMonth) =>
        payYear is >= 2000 and <= 2100 && payMonth is >= 1 and <= 12;
}
