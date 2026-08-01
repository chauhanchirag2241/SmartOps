using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Domain.Modules.FeeMaster;

public interface IFeePaymentRepository
{
    Task<bool> HasPaymentAsync(Guid studentId, Guid feeMasterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal>> GetPaidByHeadAsync(
        Guid studentId,
        Guid feeMasterId,
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Period-wise due amounts per fee head for the student's class group.</summary>
    Task<IReadOnlyList<FeeCollectionPeriodHeadDue>> GetPeriodHeadDuesAsync(
        Guid feeMasterId,
        Guid studentId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreatePaymentAsync(
        FeePaymentEntity payment,
        IReadOnlyList<FeePaymentLineEntity> lines,
        CancellationToken cancellationToken = default);

    Task<FeeCollectionDetailModel?> GetStudentCollectionDetailAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeCollectionStudentSummaryModel>> GetStudentCollectionSummariesAsync(
        IReadOnlyList<Guid> studentIds,
        CancellationToken cancellationToken = default);
}
