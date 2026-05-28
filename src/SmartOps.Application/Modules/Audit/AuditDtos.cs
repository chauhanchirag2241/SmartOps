namespace SmartOps.Application.Modules.Audit;

public sealed class AuditLogListItemDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;       // "Created" | "Updated" | "Deleted"
    public Guid ChangedBy { get; init; }
    public string ChangedByName { get; init; } = string.Empty;
    public DateTime ChangedOn { get; init; }
    public IReadOnlyList<FieldChangeDto> Changes { get; init; } = [];
}

public sealed class FieldChangeDto
{
    public string Field { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public sealed class AuditLogPagedResponseDto
{
    public IReadOnlyList<AuditLogListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
}
