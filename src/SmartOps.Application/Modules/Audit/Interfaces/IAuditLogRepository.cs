using SmartOps.Domain.Common.Models;

namespace SmartOps.Application.Modules.Audit.Interfaces;

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogListItemDto>> GetEntityHistoryAsync(
        string entityName,
        Guid entityId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task WriteAuditLogAsync(
        string entityName,
        Guid entityId,
        string action,
        Guid changedBy,
        DateTime changedOn,
        IReadOnlyList<FieldChangeDto> changes,
        CancellationToken cancellationToken = default);
}
