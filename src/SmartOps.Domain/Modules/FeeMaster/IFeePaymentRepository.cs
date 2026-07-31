using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Domain.Modules.FeeMaster;

public interface IFeePaymentRepository
{
    Task<bool> HasPaymentAsync(Guid studentId, Guid feeMasterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, decimal>> GetPaidByHeadAsync(
        Guid studentId,
        Guid feeMasterId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreatePaymentAsync(
        FeePaymentEntity payment,
        IReadOnlyList<FeePaymentLineEntity> lines,
        CancellationToken cancellationToken = default);

    Task<FeeCollectionDetailModel?> GetStudentCollectionDetailAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
