using SmartOps.Domain.Modules.Notice;

namespace SmartOps.Application.Modules.Notice;

public record CreateNoticeRequestDto(
    string Title,
    string Body,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    NoticeTargetType TargetType,
    Guid? TargetRefId,
    NoticeContentType ContentType,
    NoticeContentPayloadDto? Content);

public record UpdateNoticeRequestDto(
    string Title,
    string Body,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    NoticeTargetType TargetType,
    Guid? TargetRefId,
    NoticeContentType ContentType,
    NoticeContentPayloadDto? Content);

public record NoticeListItemDto(
    Guid Id,
    string Title,
    NoticeStatus Status,
    string StatusLabel,
    NoticeTargetType TargetType,
    string TargetTypeLabel,
    NoticeContentType ContentType,
    string ContentTypeLabel,
    bool RequiresResponse,
    DateOnly? ResponseDeadline,
    DateTimeOffset? PublishedOn,
    int ResponseCount,
    bool IsActive = true);

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
    string StatusLabel,
    NoticeTargetType TargetType,
    string TargetTypeLabel,
    Guid? TargetRefId,
    NoticeContentType ContentType,
    string ContentTypeLabel,
    NoticeContentPayloadDto? Content);
