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
    Task<IList<NoticeAudienceRow>> GetTeacherAudienceAsync(CancellationToken ct = default);
    Task<IList<NoticeAudienceRow>> GetParentAudienceAsync(Guid? classId, CancellationToken ct = default);
    Task<IList<NoticeAudienceRow>> GetSchoolUserAudienceAsync(Guid schoolId, CancellationToken ct = default);
    Task<IList<NoticeFeeParentRow>> GetPendingFeeParentTargetsAsync(Guid academicYearId, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
    Task<IList<NoticeListRow>> GetInactiveListAsync(CancellationToken ct = default);
}

public sealed class NoticeAudienceRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Subtitle { get; set; }
}

public sealed class NoticeFeeParentRow
{
    public Guid ParentUserId { get; set; }

    public string ParentName { get; set; } = string.Empty;

    public decimal PendingAmount { get; set; }

    public string StudentSummary { get; set; } = string.Empty;
}

public sealed class NoticeListRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public short Status { get; set; }
    public short TargetType { get; set; }
    public short ContentType { get; set; }
    public bool RequiresResponse { get; set; }
    public DateOnly? ResponseDeadline { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public int ResponseCount { get; set; }
    public bool IsActive { get; set; }
}

public sealed class NoticeResponseRow
{
    public Guid Id { get; set; }
    public Guid RespondentUserId { get; set; }
    public string? RespondentEmail { get; set; }
    public string ResponseBody { get; set; } = null!;
    public DateTimeOffset RespondedOn { get; set; }
}
