using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Notice;

namespace SmartOps.Application.Modules.Notice.Interfaces;

public interface INoticeService
{
    Task<Result<IList<NoticeListItemDto>>> GetListAsync(CancellationToken ct = default);
    Task<Result<NoticeDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<NoticeDetailDto>> CreateAsync(CreateNoticeRequestDto request, CancellationToken ct = default);
    Task<Result<NoticeDetailDto>> UpdateAsync(Guid id, UpdateNoticeRequestDto request, CancellationToken ct = default);
    Task<Result<NoticeDetailDto>> PublishAsync(Guid id, CancellationToken ct = default);
    Task<Result<IList<NoticeResponseItemDto>>> GetResponsesAsync(Guid id, CancellationToken ct = default);
    Task<Result> RespondAsync(Guid id, RespondToNoticeRequestDto request, CancellationToken ct = default);
    Task<Result<NoticeAudiencePreviewDto>> GetAudiencePreviewAsync(
        NoticeTargetType targetType,
        Guid? targetRefId,
        IList<Guid>? targetRefIds = null,
        CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
