using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicYear.Entities;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.AcademicYear;

public sealed class AcademicYearRepository : BaseRepository, IAcademicYearRepository
{
    public AcademicYearRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<Guid> CreateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (academicYear.Id == Guid.Empty)
        {
            academicYear.Id = Guid.NewGuid();
        }

        academicYear.IsActive = true;
        EnsureInsertAudit(academicYear, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        academicYear.IsCurrent = false;

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var id = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, academicYear, tx)
                .ConfigureAwait(false);
            return id;
        }).ConfigureAwait(false);
    }

    public async Task<AcademicYearEntity?> GetAcademicYearByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";

        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id{activeFilter};";

        return await connection.QuerySingleOrDefaultAsync<AcademicYearEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task<PagedResult<AcademicYearListModel>> GetAllAcademicYearsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        AcademicYearFilter filter = AcademicYearFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableAcademicYears;

        var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{table} ay
            {whereClause};";

        var querySql = $@"
            SELECT
                ay.id AS Id,
                ay.title AS Title,
                ay.startdate AS StartDate,
                ay.enddate AS EndDate,
                CASE
                    WHEN NOT ay.isactive THEN 'Deleted'
                    WHEN ay.iscurrent THEN 'Current'
                    ELSE 'Archived'
                END AS Status,
                ay.isactive AS IsActive,
                ay.iscurrent AS IsCurrent
            FROM {schema}.{table} ay
            {whereClause}
            ORDER BY {orderBy}";

        var result = await GetPagedResultAsync<AcademicYearListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);

        return result;
    }

    public async Task<IReadOnlyList<AcademicYearDropdownItem>> GetAcademicYearDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT
                ay.id AS Id,
                ay.title AS Name,
                ay.iscurrent AS IsCurrent
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears} ay
            WHERE ay.isactive = true
            ORDER BY ay.iscurrent DESC, ay.startdate DESC, ay.title ASC;";

        var items = await connection.QueryAsync<AcademicYearDropdownItem>(sql).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<AcademicYearEntity?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
            WHERE iscurrent = true AND isactive = true
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<AcademicYearEntity>(sql).ConfigureAwait(false);
    }

    public async Task<Guid?> GetCurrentAcademicYearIdAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await GetCurrentAcademicYearIdInternalAsync(connection, Context.OperationalSchema, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetCurrentAcademicYearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var exists = await conn.QuerySingleOrDefaultAsync<bool>(
                $"""
                SELECT EXISTS(
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableAcademicYears}
                    WHERE id = @Id AND isactive = true);
                """,
                new { Id = id },
                tx).ConfigureAwait(false);

            if (!exists)
            {
                throw new InvalidOperationException("Academic year not found or has been deleted.");
            }

            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableAcademicYears}
                SET iscurrent = false,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE isactive = true AND iscurrent = true;
                """,
                new { UpdatedBy = actorId, UpdatedOn = utcNow },
                tx).ConfigureAwait(false);

            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableAcademicYears}
                SET iscurrent = true,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE id = @Id AND isactive = true;
                """,
                new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow },
                tx).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<bool> AcademicYearExistsAsync(Guid id, bool requireNotDeleted = true, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var deletedFilter = requireNotDeleted ? " AND isactive = true" : string.Empty;
        var sql = $@"
            SELECT EXISTS(
                SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                WHERE id = @Id{deletedFilter});";

        return await connection.QuerySingleAsync<bool>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task UpdateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(academicYear, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, academicYear, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var isCurrent = await connection.QuerySingleOrDefaultAsync<bool>(
            $"""
            SELECT iscurrent FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """,
            new { Id = id }).ConfigureAwait(false);

        if (isCurrent)
        {
            throw new InvalidOperationException("Cannot delete the current academic year. Set another year as current first.");
        }

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IList<AcademicYearSemesterEntity>> GetSemestersAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
            SELECT id AS Id,
                   academicyearid AS AcademicYearId,
                   semesterindex AS SemesterIndex,
                   name AS Name,
                   startdate AS StartDate,
                   enddate AS EndDate
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYearSemesters}
            WHERE academicyearid = @AcademicYearId AND isactive = true
            ORDER BY semesterindex;";

        var rows = await connection
            .QueryAsync<AcademicYearSemesterEntity>(sql, new { AcademicYearId = academicYearId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SaveSemestersAsync(
        Guid academicYearId,
        IList<AcademicYearSemesterInput> semesters,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveInsertActor();
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableAcademicYearSemesters;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{table}
                SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                WHERE academicyearid = @AcademicYearId AND isactive = true;
                """,
                new { AcademicYearId = academicYearId, UpdatedBy = actorId, UpdatedOn = utcNow },
                tx).ConfigureAwait(false);

            foreach (var semester in semesters.OrderBy(s => s.SemesterIndex))
            {
                var entity = new AcademicYearSemesterEntity
                {
                    Id = Guid.NewGuid(),
                    AcademicYearId = academicYearId,
                    SemesterIndex = semester.SemesterIndex,
                    Name = semester.Name.Trim(),
                    StartDate = semester.StartDate,
                    EndDate = semester.EndDate
                };
                EnsureInsertAudit(entity, utcNow, actorId);
                await InsertAsync(conn, schema, table, entity, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    private static async Task<Guid?> GetCurrentAcademicYearIdInternalAsync(
        System.Data.IDbConnection connection,
        string schema,
        System.Data.IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
            WHERE iscurrent = true AND isactive = true
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(AcademicYearFilter filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        switch (filter)
        {
            case AcademicYearFilter.Active:
                where += " AND ay.isactive = true";
                break;
            case AcademicYearFilter.Inactive:
                where += " AND ay.isactive = false";
                break;
            case AcademicYearFilter.Current:
                where += " AND ay.isactive = true AND ay.iscurrent = true";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (ay.title ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "ay.iscurrent DESC, ay.startdate DESC, ay.id ASC";
        }

        if (IsSortKey(sortColumn, "title"))
        {
            return $"ay.title {direction}, ay.id ASC";
        }

        if (IsSortKey(sortColumn, "startDate"))
        {
            return $"ay.startdate {direction}, ay.id ASC";
        }

        if (IsSortKey(sortColumn, "endDate"))
        {
            return $"ay.enddate {direction}, ay.id ASC";
        }

        return "ay.iscurrent DESC, ay.startdate DESC, ay.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }
}
