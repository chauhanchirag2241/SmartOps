namespace SmartOps.Application.Modules.Student.Interfaces;

public interface IStudentFeeHeadAssignmentRepository
{
    /// <summary>
    /// Included fee type ids for this student and version, or null when no rows exist (legacy: all heads apply).
    /// </summary>
    Task<IReadOnlySet<Guid>?> GetIncludedFeeTypeIdsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);
}
