using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface IPayrollRepository
{
    Task<PayrollRunEntity?> GetRunByYearMonthAsync(int payYear, int payMonth, CancellationToken ct = default);

    Task<PayrollRunEntity?> GetRunByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CreateRunAsync(PayrollRunEntity entity, CancellationToken ct = default);

    Task UpdateRunAsync(PayrollRunEntity entity, CancellationToken ct = default);

    Task DeleteEntriesForRunAsync(Guid runId, CancellationToken ct = default);

    Task<IList<PayrollEntryListRow>> GetEntriesForRunAsync(Guid runId, CancellationToken ct = default);

    Task<PayrollEntryEntity?> GetEntryByIdAsync(Guid entryId, CancellationToken ct = default);

    Task<Guid> CreateEntryAsync(PayrollEntryEntity entity, CancellationToken ct = default);

    Task CreateEntryLinesAsync(IEnumerable<PayrollEntryLineEntity> lines, CancellationToken ct = default);

    Task UpdateEntryStatusAsync(Guid entryId, PayrollEntryStatus status, CancellationToken ct = default);

    Task MarkEntriesPaidAsync(Guid runId, IEnumerable<Guid>? entryIds, CancellationToken ct = default);

    Task<IList<PayrollEntryLineEntity>> GetLinesForEntryAsync(Guid entryId, CancellationToken ct = default);

    Task<PayslipContextRow?> GetPayslipContextAsync(Guid entryId, CancellationToken ct = default);
}

public sealed class PayrollEntryListRow
{
    public Guid Id { get; init; }
    public Guid TeacherId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? Department { get; init; }
    public decimal BasicSalary { get; init; }
    public decimal HraAmount { get; init; }
    public decimal Allowances { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetSalary { get; init; }
    public PayrollEntryStatus Status { get; init; }
}

public sealed class PayslipContextRow
{
    public Guid EntryId { get; init; }
    public Guid RunId { get; init; }
    public int PayYear { get; init; }
    public int PayMonth { get; init; }
    public Guid TeacherId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? Department { get; init; }
    public string? Designation { get; init; }
    public decimal BasicSalary { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetSalary { get; init; }
    public int WorkingDays { get; init; }
    public int PresentDays { get; init; }
    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? BankIfscCode { get; init; }
}
