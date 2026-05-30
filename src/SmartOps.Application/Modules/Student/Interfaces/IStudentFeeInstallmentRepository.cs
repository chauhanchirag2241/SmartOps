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

    /// <summary>True when the student has fee lines for the current year (not only carried-forward pending).</summary>
    Task<bool> HasCurrentYearFeeInstallmentsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    /// <summary>Generates class fee installments for the year; preserves an existing carried-forward line when possible.</summary>
    Task EnsureCurrentYearInstallmentsAsync(
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

    Task CopyFeeHeadAssignmentsFromVersionAsync(
        Guid studentId,
        Guid sourceFeeStructureVersionId,
        Guid targetFeeStructureVersionId,
        CancellationToken ct = default);

    /// <summary>Adds a single installment line for unpaid balance carried from a prior academic year.</summary>
    Task AddCarriedForwardBalanceAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        decimal pendingAmount,
        CancellationToken ct = default);
}
