using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Application.Modules.Student.Interfaces;

public interface IStudentFeeInstallmentRepository
{
    Task<bool> IsSchemaReadyAsync(CancellationToken ct = default);

    Task<IList<ClassFeeInstallmentRow>> GetByStudentVersionAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> StudentHasInstallmentsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> InstallmentBelongsToStudentAsync(
        Guid installmentId,
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task GenerateForStudentAdmissionAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        IList<StudentFeeHeadAssignmentEntity> assignments,
        CancellationToken ct = default);

    Task EnsureForStudentAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<bool> StudentHasInstallmentPaymentsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> InstallmentsAlignWithAssignmentsAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);
}
