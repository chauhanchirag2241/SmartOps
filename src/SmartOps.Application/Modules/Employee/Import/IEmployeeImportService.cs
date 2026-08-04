using SmartOps.Application.Modules.BulkImport;

namespace SmartOps.Application.Modules.Employee.Import;

public interface IEmployeeImportService
{
    Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default);

    Task<EmployeeImportValidateResultDto> ValidateAsync(
        Stream fileStream,
        CancellationToken cancellationToken = default);

    Task<EmployeeImportCommitResultDto> CommitAsync(
        Stream fileStream,
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
