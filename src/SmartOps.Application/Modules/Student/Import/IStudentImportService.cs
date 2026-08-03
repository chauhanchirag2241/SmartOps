using SmartOps.Application.Modules.BulkImport;

namespace SmartOps.Application.Modules.Student.Import;

public interface IStudentImportService
{
    Task<byte[]> BuildTemplateAsync(CancellationToken cancellationToken = default);

    Task<StudentImportValidateResultDto> ValidateAsync(
        Stream fileStream,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<StudentImportCommitResultDto> CommitAsync(
        Stream fileStream,
        Guid academicYearId,
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
