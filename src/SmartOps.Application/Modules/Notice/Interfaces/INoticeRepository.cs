using SmartOps.Domain.Modules.Notice.Entities;

namespace SmartOps.Application.Modules.Notice.Interfaces;

public interface INoticeRepository
{
    Task<Guid> CreateAsync(NoticeEntity entity, CancellationToken ct = default);
    Task UpdateAsync(NoticeEntity entity, CancellationToken ct = default);
    Task<NoticeEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<NoticeListRow>> GetListAsync(CancellationToken ct = default);
    Task UpsertResponseAsync(Guid noticeId, Guid respondentUserId, string responseBody, CancellationToken ct = default);
    Task<IList<NoticeResponseRow>> GetResponsesAsync(Guid noticeId, CancellationToken ct = default);
    Task<int> CountResponsesAsync(Guid noticeId, CancellationToken ct = default);
}

public sealed class NoticeListRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public short Status { get; set; }
    public short TargetType { get; set; }
    public bool RequiresResponse { get; set; }
    public DateOnly? ResponseDeadline { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public int ResponseCount { get; set; }
}

public sealed class NoticeResponseRow
{
    public Guid Id { get; set; }
    public Guid RespondentUserId { get; set; }
    public string? RespondentEmail { get; set; }
    public string ResponseBody { get; set; } = null!;
    public DateTimeOffset RespondedOn { get; set; }
}
