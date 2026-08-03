namespace SmartOps.Application.Modules.Jobs;

public record JobDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string CronExpression,
    string TimeZoneId,
    bool IsEnabled,
    int SortOrder);

public record UpdateJobDefinitionDto(
    string CronExpression,
    string TimeZoneId,
    bool IsEnabled);

public record HangfireStatusDto(bool IsEnabled);

public record JobMasterPageDto(
    bool HangfireEnabled,
    IList<JobDefinitionDto> Jobs);

