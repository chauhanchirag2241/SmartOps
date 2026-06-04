using SmartOps.Domain.Common;

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
}
