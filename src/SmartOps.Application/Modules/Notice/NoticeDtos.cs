using SmartOps.Domain.Modules.Notice;

namespace SmartOps.Application.Modules.Notice;

public record CreateNoticeRequestDto(
    string Title,
    string Body,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    NoticeTargetType TargetType,
    Guid? TargetRefId);

public record UpdateNoticeRequestDto(
    string Title,
    string Body,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    NoticeTargetType TargetType,
    Guid? TargetRefId);

public record NoticeListItemDto(
    Guid Id,
    string Title,
    NoticeStatus Status,
    string StatusLabel,
    NoticeTargetType TargetType,
    string TargetTypeLabel,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    DateTimeOffset? PublishedOn,
    int ResponseCount);

public record NoticeResponseItemDto(
    Guid Id,
    Guid RespondentUserId,
    string? RespondentName,
    string ResponseBody,
    DateTimeOffset RespondedOn);

public record RespondToNoticeRequestDto(string ResponseBody);

public record NoticeDetailDto(
    Guid Id,
    string Title,
    string Body,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    string StatusLabel);
