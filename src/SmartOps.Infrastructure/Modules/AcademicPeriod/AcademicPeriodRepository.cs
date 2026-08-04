using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.AcademicPeriod;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.AcademicPeriod;

public sealed class AcademicPeriodRepository : BaseRepository, IAcademicPeriodRepository
{
    private readonly IBranchContext _branchContext;

    /// <summary>Temporary periodindex values used while swapping active indexes (must be &gt; 0 for CHECK).</summary>
    private const int TempIndexOffset = 100_000;

    public AcademicPeriodRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
    }

    public async Task<IReadOnlyList<ClassAcademicPeriodEntity>> GetByClassAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id,
                   classgroupid AS ClassGroupId,
                   periodindex AS PeriodIndex,
                   name AS Name,
                   versionno AS VersionNo,
                   createdby AS CreatedBy,
                   createdon AS CreatedOn,
                   isactive AS IsActive
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE classgroupid = @ClassGroupId
              AND isactive = true
            ORDER BY periodindex;
            """;
        IEnumerable<ClassAcademicPeriodEntity> rows = await connection.QueryAsync<ClassAcademicPeriodEntity>(
            new CommandDefinition(
                sql,
                new { ClassGroupId = classId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ClassAcademicPeriodEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id,
                   classgroupid AS ClassGroupId,
                   periodindex AS PeriodIndex,
                   name AS Name,
                   isactive AS IsActive,
                   createdby AS CreatedBy,
                   createdon AS CreatedOn,
                   updatedby AS UpdatedBy,
                   updatedon AS UpdatedOn,
                   versionno AS VersionNo
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<ClassAcademicPeriodEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(
        Guid classId,
        IReadOnlyList<ClassAcademicPeriodEntity> periods,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        string schema = Context.OperationalSchema;
        string table = DatabaseConfig.TableClassAcademicPeriods;

        IReadOnlyList<ClassAcademicPeriodEntity> existing =
            await GetByClassAsync(classId, cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            HashSet<Guid> claimedIds = [];
            List<(ClassAcademicPeriodEntity Incoming, ClassAcademicPeriodEntity Current)> updates = [];
            List<ClassAcademicPeriodEntity> inserts = [];

            foreach (ClassAcademicPeriodEntity incoming in periods.OrderBy(p => p.PeriodIndex))
            {
                incoming.ClassGroupId = classId;
                incoming.Name = incoming.Name.Trim();

                ClassAcademicPeriodEntity? current =
                    MatchExisting(incoming, existing, claimedIds);

                if (current is not null)
                {
                    claimedIds.Add(current.Id);
                    updates.Add((incoming, current));
                }
                else
                {
                    inserts.Add(incoming);
                }
            }

            // Remove periods dropped from the list first (frees unique index/name slots).
            foreach (ClassAcademicPeriodEntity old in existing)
            {
                if (!claimedIds.Contains(old.Id))
                {
                    await SoftDeleteAsync(conn, schema, table, old.Id, tx).ConfigureAwait(false);
                }
            }

            bool needsIndexPark = updates.Any(u => u.Incoming.PeriodIndex != u.Current.PeriodIndex);
            if (needsIndexPark)
            {
                for (int i = 0; i < updates.Count; i++)
                {
                    (_, ClassAcademicPeriodEntity current) = updates[i];
                    ClassAcademicPeriodEntity parked = CloneForUpdate(
                        current,
                        classId,
                        TempIndexOffset + i + 1,
                        $"__park_{current.Id:N}");
                    ApplyUpdateAudit(parked, actorId, now);
                    await UpdateAsync(conn, schema, table, parked, tx, "Id").ConfigureAwait(false);
                    current.VersionNo = parked.VersionNo;
                }
            }

            foreach ((ClassAcademicPeriodEntity incoming, ClassAcademicPeriodEntity current) in updates)
            {
                if (!needsIndexPark
                    && string.Equals(current.Name, incoming.Name, StringComparison.Ordinal)
                    && current.PeriodIndex == incoming.PeriodIndex)
                {
                    continue;
                }

                ClassAcademicPeriodEntity final = CloneForUpdate(
                    current,
                    classId,
                    incoming.PeriodIndex,
                    incoming.Name);
                ApplyUpdateAudit(final, actorId, now);
                await UpdateAsync(conn, schema, table, final, tx, "Id").ConfigureAwait(false);
            }

            foreach (ClassAcademicPeriodEntity incoming in inserts)
            {
                incoming.Id = Guid.NewGuid();
                EnsureInsertAudit(incoming, now, actorId);
                await InsertAsync(conn, schema, table, incoming, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    private static ClassAcademicPeriodEntity? MatchExisting(
        ClassAcademicPeriodEntity incoming,
        IReadOnlyList<ClassAcademicPeriodEntity> existing,
        HashSet<Guid> claimedIds)
    {
        if (incoming.Id != Guid.Empty)
        {
            ClassAcademicPeriodEntity? byId = existing.FirstOrDefault(e => e.Id == incoming.Id);
            if (byId is not null && !claimedIds.Contains(byId.Id))
            {
                return byId;
            }
        }

        // Fallback: same slot (period index) — keeps edit-in-place when client omits Id.
        ClassAcademicPeriodEntity? byIndex = existing.FirstOrDefault(
            e => e.PeriodIndex == incoming.PeriodIndex && !claimedIds.Contains(e.Id));
        return byIndex;
    }

    private static ClassAcademicPeriodEntity CloneForUpdate(
        ClassAcademicPeriodEntity current,
        Guid classId,
        int periodIndex,
        string name) =>
        new()
        {
            Id = current.Id,
            ClassGroupId = classId,
            PeriodIndex = periodIndex,
            Name = name,
            VersionNo = current.VersionNo,
            CreatedBy = current.CreatedBy,
            CreatedOn = current.CreatedOn,
            IsActive = true,
        };
}
