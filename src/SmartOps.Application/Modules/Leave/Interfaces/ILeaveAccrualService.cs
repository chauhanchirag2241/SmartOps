using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveAccrualService
{
    Task<Result> RunAllSchoolsAsync(int year, int month, CancellationToken ct = default);
    Task<Result> RunForSchoolAsync(Guid schoolId, int year, int month, CancellationToken ct = default);
}
