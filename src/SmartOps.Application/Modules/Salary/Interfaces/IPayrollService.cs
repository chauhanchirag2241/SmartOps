using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface IPayrollService
{
    Task<Result<PayrollRunDto>> GetPayrollAsync(int payYear, int payMonth, CancellationToken ct = default);

    Task<Result<PayrollRunDto>> ProcessPayrollAsync(ProcessPayrollRequestDto request, CancellationToken ct = default);

    Task<Result<bool>> MarkPaidAsync(Guid runId, MarkPayrollPaidRequestDto request, CancellationToken ct = default);

    Task<Result<PayslipDto>> GetPayslipAsync(Guid entryId, CancellationToken ct = default);
}
