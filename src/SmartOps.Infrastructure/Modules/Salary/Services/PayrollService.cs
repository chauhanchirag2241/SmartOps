using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.Salary;

using SmartOps.Application.Abstractions;

namespace SmartOps.Infrastructure.Modules.Salary.Services;

public sealed class PayrollService : IPayrollService
{
    public const string AttendanceCutComponentName = "Attendance cut";

    private readonly IPayrollRepository _payrollRepo;
    private readonly IEmployeeSalaryRepository _employeeRepo;
    private readonly ISalaryStructureRepository _structureRepo;
    private readonly IStaffAttendanceService _attendanceService;
    private readonly IAcademicCalendarService _calendarService;
    private readonly IBranchContext _branchContext;

    public PayrollService(
        IPayrollRepository payrollRepo,
        IEmployeeSalaryRepository employeeRepo,
        ISalaryStructureRepository structureRepo,
        IStaffAttendanceService attendanceService,
        IAcademicCalendarService calendarService,
        IBranchContext branchContext)
    {
        _payrollRepo = payrollRepo;
        _employeeRepo = employeeRepo;
        _structureRepo = structureRepo;
        _attendanceService = attendanceService;
        _calendarService = calendarService;
        _branchContext = branchContext;
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

    public async Task<Result<PayrollRunDto>> PreviewPayrollAsync(PreviewPayrollRequestDto request, CancellationToken ct = default)
    {
        if (!IsValidPeriod(request.PayYear, request.PayMonth))
        {
            return Result<PayrollRunDto>.Failure("Invalid pay year or month.");
        }

        Result<IList<BuiltPayrollEntry>> built = await BuildEntriesAsync(
            request.PayYear,
            request.PayMonth,
            request.UseAttendanceWiseSalary,
            request.FullSalaryEmployeeIds,
            ct).ConfigureAwait(false);
        if (!built.IsSuccess)
        {
            return Result<PayrollRunDto>.Failure(built.Error ?? "Failed to preview payroll.");
        }

        return Result<PayrollRunDto>.Success(MapPreviewRun(
            request.PayYear,
            request.PayMonth,
            request.UseAttendanceWiseSalary,
            built.Value!));
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

        Result<IList<BuiltPayrollEntry>> built = await BuildEntriesAsync(
            request.PayYear,
            request.PayMonth,
            request.UseAttendanceWiseSalary,
            request.FullSalaryEmployeeIds,
            ct).ConfigureAwait(false);
        if (!built.IsSuccess)
        {
            return Result<PayrollRunDto>.Failure(built.Error ?? "Failed to process payroll.");
        }

        IList<BuiltPayrollEntry> entries = built.Value!;

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

        decimal totalGross = 0;
        decimal totalDeductions = 0;
        decimal totalNet = 0;

        foreach (BuiltPayrollEntry builtEntry in entries)
        {
            var entry = new PayrollEntryEntity
            {
                PayrollRunId = run.Id,
                EmployeeId = builtEntry.EmployeeId,
                BasicSalary = builtEntry.BasicSalary,
                GrossSalary = builtEntry.GrossSalary,
                TotalDeductions = builtEntry.TotalDeductions,
                NetSalary = builtEntry.NetSalary,
                Status = PayrollEntryStatus.Processed,
                WorkingDays = builtEntry.WorkingDays,
                PresentDays = builtEntry.PresentDays
            };
            Guid entryId = await _payrollRepo.CreateEntryAsync(entry, ct).ConfigureAwait(false);
            await _payrollRepo.CreateEntryLinesAsync(
                builtEntry.Lines.Select(line => new PayrollEntryLineEntity
                {
                    PayrollEntryId = entryId,
                    SalaryVersionComponentId = line.ComponentId,
                    ComponentName = line.Name,
                    ComponentType = line.ComponentType,
                    Amount = line.Amount,
                    IsEarning = line.IsEarning
                }).ToList(),
                ct).ConfigureAwait(false);

            totalGross += builtEntry.GrossSalary;
            totalDeductions += builtEntry.TotalDeductions;
            totalNet += builtEntry.NetSalary;
        }

        run.Status = PayrollRunStatus.Processed;
        run.TotalGross = totalGross;
        run.TotalDeductions = totalDeductions;
        run.TotalNet = totalNet;
        run.EmployeeCount = entries.Count;
        run.ProcessedOn = SchoolLocalTime.NowDateTime();
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
        decimal attendanceCut = deductions
            .Where(d => string.Equals(d.Name, AttendanceCutComponentName, StringComparison.OrdinalIgnoreCase))
            .Sum(d => d.Amount);
        int daysCut = Math.Max(0, context.WorkingDays - context.PresentDays);

        return Result<PayslipDto>.Success(new PayslipDto(
            context.EntryId,
            context.PayYear,
            context.PayMonth,
            context.EmployeeName,
            context.EmployeeCode,
            string.IsNullOrWhiteSpace(context.Department) ? null : context.Department,
            context.Designation,
            context.UseAttendanceWiseSalary,
            context.WorkingDays,
            context.PresentDays,
            daysCut,
            attendanceCut,
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

    private async Task<Result<IList<BuiltPayrollEntry>>> BuildEntriesAsync(
        int payYear,
        int payMonth,
        bool useAttendanceWiseSalary,
        IList<Guid>? fullSalaryEmployeeIds,
        CancellationToken ct)
    {
        IList<EmployeeSalaryEntity> assignments = await _employeeRepo.GetActiveAssignmentsAsync(ct).ConfigureAwait(false);
        if (assignments.Count == 0)
        {
            return Result<IList<BuiltPayrollEntry>>.Failure("No active employee salary assignments found.");
        }

        HashSet<Guid> fullSalaryIds = (fullSalaryEmployeeIds ?? [])
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        int calendarDays = DateTime.DaysInMonth(payYear, payMonth);
        int attendanceWorkingDays = calendarDays;
        Dictionary<Guid, StaffAttendanceReportEmployeeDto> attendanceByEmployee = new();

        if (useAttendanceWiseSalary)
        {
            Result<StaffAttendanceReportDto> attendanceResult =
                await _attendanceService.GetReportAsync(payMonth, payYear, null, ct).ConfigureAwait(false);
            if (attendanceResult.IsSuccess && attendanceResult.Value is not null)
            {
                attendanceWorkingDays = Math.Max(1, attendanceResult.Value.TotalWorkingDays);
                attendanceByEmployee = attendanceResult.Value.Employees
                    .GroupBy(e => e.EmployeeId)
                    .ToDictionary(g => g.Key, g => g.First());
            }
            else
            {
                await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
                attendanceWorkingDays = Math.Max(
                    1,
                    await _calendarService
                        .CountWorkingDaysAsync(
                            _branchContext.ActiveBranchId,
                            payYear,
                            payMonth,
                            CalendarAudience.Staff,
                            ct)
                        .ConfigureAwait(false));
            }
        }

        var builtEntries = new List<BuiltPayrollEntry>();

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

            int entryWorkingDays = useAttendanceWiseSalary ? attendanceWorkingDays : calendarDays;
            int presentDays = entryWorkingDays;
            decimal attendanceCut = 0m;
            bool useFullSalaryOverride = useAttendanceWiseSalary && fullSalaryIds.Contains(assignment.EmployeeId);
            var lines = breakdown.Earnings.Concat(breakdown.Deductions).ToList();
            decimal totalDeductions = breakdown.TotalDeductions;
            decimal netSalary = breakdown.NetSalary;
            decimal perDayCutAmount = entryWorkingDays > 0
                ? RoundMoney(breakdown.GrossSalary / entryWorkingDays)
                : 0m;

            if (useAttendanceWiseSalary && !useFullSalaryOverride
                && attendanceByEmployee.TryGetValue(assignment.EmployeeId, out StaffAttendanceReportEmployeeDto? att))
            {
                decimal payableDays = att.PresentDays + att.LateDays + (att.HalfDayDays * 0.5m);
                payableDays = Math.Clamp(payableDays, 0m, entryWorkingDays);
                presentDays = (int)Math.Round(payableDays, MidpointRounding.AwayFromZero);
                presentDays = Math.Clamp(presentDays, 0, entryWorkingDays);

                decimal ratio = entryWorkingDays > 0 ? payableDays / entryWorkingDays : 1m;
                ratio = Math.Clamp(ratio, 0m, 1m);
                attendanceCut = RoundMoney(breakdown.GrossSalary * (1m - ratio));
            }

            if (attendanceCut > 0)
            {
                lines.Add(new SalaryLineItemDto(
                    null,
                    AttendanceCutComponentName,
                    SalaryComponentType.Deduction,
                    SalaryLabelHelper.ComponentTypeLabel(SalaryComponentType.Deduction),
                    attendanceCut,
                    false));
                totalDeductions = RoundMoney(totalDeductions + attendanceCut);
                netSalary = RoundMoney(breakdown.GrossSalary - totalDeductions);
            }

            EmployeeSalaryContextRow? empInfo =
                await _employeeRepo.GetEmployeeSalaryContextAsync(assignment.EmployeeId, ct).ConfigureAwait(false);

            builtEntries.Add(new BuiltPayrollEntry(
                assignment.EmployeeId,
                empInfo?.EmployeeName ?? string.Empty,
                empInfo?.Department,
                breakdown.BasicSalary,
                breakdown.GrossSalary,
                totalDeductions,
                netSalary,
                entryWorkingDays,
                presentDays,
                attendanceCut,
                perDayCutAmount,
                useFullSalaryOverride,
                lines));
        }

        if (builtEntries.Count == 0)
        {
            return Result<IList<BuiltPayrollEntry>>.Failure("No employees with salary component values found.");
        }

        return Result<IList<BuiltPayrollEntry>>.Success(builtEntries);
    }

    private static PayrollRunDto MapPreviewRun(
        int payYear,
        int payMonth,
        bool useAttendanceWiseSalary,
        IList<BuiltPayrollEntry> entries) =>
        new(
            Guid.Empty,
            payYear,
            payMonth,
            PayrollRunStatus.Draft,
            SalaryLabelHelper.PayrollRunStatusLabel(PayrollRunStatus.Draft),
            useAttendanceWiseSalary,
            entries.Sum(e => e.GrossSalary),
            entries.Sum(e => e.TotalDeductions),
            entries.Sum(e => e.NetSalary),
            entries.Count,
            null,
            entries
                .OrderBy(e => e.EmployeeName)
                .Select(e =>
                {
                    int daysCut = Math.Max(0, e.WorkingDays - e.PresentDays);
                    IList<SalaryLineItemDto> earnings = e.Lines.Where(l => l.IsEarning).ToList();
                    IList<SalaryLineItemDto> deductions = e.Lines.Where(l => !l.IsEarning).ToList();
                    return new PayrollEntryListItemDto(
                        Guid.Empty,
                        e.EmployeeId,
                        e.EmployeeName,
                        string.IsNullOrWhiteSpace(e.Department) ? null : e.Department,
                        e.BasicSalary,
                        0,
                        RoundMoney(Math.Max(0, e.GrossSalary - e.BasicSalary)),
                        e.GrossSalary,
                        e.TotalDeductions,
                        e.NetSalary,
                        e.WorkingDays,
                        e.PresentDays,
                        daysCut,
                        e.AttendanceCutAmount,
                        e.PerDayCutAmount,
                        e.UseFullSalaryOverride,
                        PayrollEntryStatus.Draft,
                        SalaryLabelHelper.PayrollEntryStatusLabel(PayrollEntryStatus.Draft),
                        earnings,
                        deductions);
                }).ToList());

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

    private static PayrollEntryListItemDto MapEntry(PayrollEntryListRow row)
    {
        int daysCut = Math.Max(0, row.WorkingDays - row.PresentDays);
        decimal perDayCut = daysCut > 0 && row.AttendanceCutAmount > 0
            ? RoundMoney(row.AttendanceCutAmount / daysCut)
            : (row.WorkingDays > 0 ? RoundMoney(row.GrossSalary / row.WorkingDays) : 0m);

        return new(
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
            row.WorkingDays,
            row.PresentDays,
            daysCut,
            row.AttendanceCutAmount,
            perDayCut,
            false,
            row.Status,
            SalaryLabelHelper.PayrollEntryStatusLabel(row.Status),
            [],
            []);
    }

    private static SalaryLineItemDto MapLine(PayrollEntryLineEntity line) => new(
        line.SalaryVersionComponentId,
        line.ComponentName,
        line.ComponentType,
        SalaryLabelHelper.ComponentTypeLabel(line.ComponentType),
        line.Amount,
        line.IsEarning);

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool IsValidPeriod(int payYear, int payMonth) =>
        payYear is >= 2000 and <= 2100 && payMonth is >= 1 and <= 12;

    private sealed record BuiltPayrollEntry(
        Guid EmployeeId,
        string EmployeeName,
        string? Department,
        decimal BasicSalary,
        decimal GrossSalary,
        decimal TotalDeductions,
        decimal NetSalary,
        int WorkingDays,
        int PresentDays,
        decimal AttendanceCutAmount,
        decimal PerDayCutAmount,
        bool UseFullSalaryOverride,
        IList<SalaryLineItemDto> Lines);
}
