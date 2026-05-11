using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Persistence.Repositories;

public abstract class BaseRepository
{
    protected readonly DapperContext Context;
    protected readonly ICurrentUserService CurrentUser;

    protected BaseRepository(DapperContext context, ICurrentUserService currentUser)
    {
        Context = context;
        CurrentUser = currentUser;
    }

    protected void EnsureInsertAudit(AuditableEntity entity, DateTime utcNow, Guid? fallbackActorId = null)
    {
        Guid actor = ResolveInsertActor(fallbackActorId);

        if (entity.CreatedBy == Guid.Empty)
        {
            entity.CreatedBy = actor;
        }

        if (entity.UpdatedBy == Guid.Empty)
        {
            entity.UpdatedBy = entity.CreatedBy;
        }

        entity.CreatedOn = utcNow;
        entity.UpdatedOn = utcNow;
        entity.IsActive = true;
        entity.VersionNo = 1;
    }

    protected Guid ResolveInsertActor(Guid? fallbackActorId = null)
    {
        if (CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty)
        {
            return CurrentUser.UserId;
        }

        if (fallbackActorId.HasValue && fallbackActorId.Value != Guid.Empty)
        {
            return fallbackActorId.Value;
        }

        return Guid.Parse(DatabaseConfig.SystemUserId);
    }

    protected Guid ResolveUpdateActor(Guid? fallbackUserId = null)
    {
        if (CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty)
        {
            return CurrentUser.UserId;
        }

        if (fallbackUserId.HasValue && fallbackUserId.Value != Guid.Empty)
        {
            return fallbackUserId.Value;
        }

        throw new InvalidOperationException("An actor is required for updates.");
    }

    protected static void ApplyUpdateAudit(AuditableEntity entity, Guid actorId, DateTime utcNow)
    {
        entity.UpdatedBy = actorId;
        entity.UpdatedOn = utcNow;
    }
}
