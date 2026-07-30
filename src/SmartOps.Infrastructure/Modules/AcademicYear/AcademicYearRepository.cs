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

        academicYear.Title = academicYear.Title.Trim();
        academicYear.IsActive = true;
        // Status column kept for schema compatibility; current year is date-derived.
        academicYear.Status = AcademicYearStatus.Draft;
        EnsureInsertAudit(academicYear, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

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
                    WHEN CURRENT_DATE BETWEEN ay.startdate AND ay.enddate THEN 'Current'
                    WHEN ay.startdate > CURRENT_DATE THEN 'Upcoming'
                    ELSE 'Past'
                END AS Status,
                ay.isactive AS IsActive,
                (ay.isactive AND CURRENT_DATE BETWEEN ay.startdate AND ay.enddate) AS IsCurrent
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

    public async Task<IReadOnlyList<AcademicYearDropdownItem>> GetAcademicYearDropdownAsync(
        bool currentAndFutureOnly = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableAcademicYears;

        // all: every active year for settings year switcher.
        // switcher: current (by date) + upcoming only (excludes past).
        string scopeFilter = currentAndFutureOnly
            ? "AND ay.enddate >= CURRENT_DATE"
            : string.Empty;

        var sql = $@"
            SELECT
                ay.id AS Id,
                ay.title AS Name,
                (CURRENT_DATE BETWEEN ay.startdate AND ay.enddate) AS IsCurrent,
                ay.startdate AS StartDate
            FROM {schema}.{table} ay
            WHERE ay.isactive = true
            {scopeFilter}
            ORDER BY (CURRENT_DATE BETWEEN ay.startdate AND ay.enddate) DESC, ay.startdate DESC, ay.title ASC;";

        var items = await connection.QueryAsync<AcademicYearDropdownItem>(sql).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<AcademicYearEntity?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
            WHERE isactive = true
              AND CURRENT_DATE BETWEEN startdate AND enddate
            ORDER BY startdate DESC
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<AcademicYearEntity>(sql).ConfigureAwait(false);
    }

    public async Task<Guid?> GetCurrentAcademicYearIdAsync(CancellationToken cancellationToken = default)
    {
        if (Context.OperationalSchema == DatabaseConfig.Schema_Global)
        {
            return null;
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await GetCurrentAcademicYearIdInternalAsync(connection, Context.OperationalSchema, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> TitleExistsAsync(
        string title,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                WHERE isactive = true
                  AND LOWER(title) = LOWER(@Title)
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId));
            """;

        return await connection.QuerySingleAsync<bool>(
            sql,
            new { Title = title.Trim(), ExcludeId = excludeId }).ConfigureAwait(false);
    }

    public async Task<bool> HasOverlappingDatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                WHERE isactive = true
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
                  AND startdate <= @EndDate
                  AND enddate >= @StartDate);
            """;

        return await connection.QuerySingleAsync<bool>(
            sql,
            new { StartDate = startDate, EndDate = endDate, ExcludeId = excludeId }).ConfigureAwait(false);
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

    public async Task<bool> IsAcademicYearBeforeAsync(
        Guid academicYearId,
        Guid referenceAcademicYearId,
        CancellationToken cancellationToken = default)
    {
        if (academicYearId == referenceAcademicYearId)
        {
            return false;
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
            SELECT
                (SELECT startdate FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                 WHERE id = @AcademicYearId AND isactive = true)
                <
                (SELECT startdate FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                 WHERE id = @ReferenceAcademicYearId AND isactive = true);";

        return await connection.QuerySingleOrDefaultAsync<bool?>(sql, new
        {
            AcademicYearId = academicYearId,
            ReferenceAcademicYearId = referenceAcademicYearId,
        }).ConfigureAwait(false) ?? false;
    }

    public async Task UpdateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var existing = await conn.QuerySingleOrDefaultAsync<AcademicYearEntity>(
                $"""
                SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
                WHERE id = @Id;
                """,
                new { academicYear.Id },
                tx).ConfigureAwait(false);

            if (existing is null || !existing.IsActive)
            {
                throw new InvalidOperationException("Academic year not found or has been deleted.");
            }

            existing.Title = academicYear.Title.Trim();
            existing.StartDate = academicYear.StartDate;
            existing.EndDate = academicYear.EndDate;
            ApplyUpdateAudit(existing, actorId, utcNow);

            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, existing, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var current = await connection.QuerySingleOrDefaultAsync<(DateOnly StartDate, DateOnly EndDate)?>(
            $"""
            SELECT startdate AS StartDate, enddate AS EndDate
            FROM {schema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """,
            new { Id = id }).ConfigureAwait(false);

        if (current is null)
        {
            throw new InvalidOperationException("Academic year not found or has been deleted.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today >= current.Value.StartDate && today <= current.Value.EndDate)
        {
            throw new InvalidOperationException("Cannot delete the current academic year (today falls within its date range).");
        }

        var studentCount = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableStudentAcademics} WHERE academicyearid = @Id AND isactive = true;",
            new { Id = id }).ConfigureAwait(false);

        if (studentCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete this academic year because it has {studentCount} student enrollment(s) associated with it.");
        }

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, schema, DatabaseConfig.TableAcademicYears, id, tx)
                .ConfigureAwait(false);
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
            WHERE isactive = true
              AND CURRENT_DATE BETWEEN startdate AND enddate
            ORDER BY startdate DESC
            LIMIT 1;";

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
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
                where += " AND ay.isactive = true AND CURRENT_DATE BETWEEN ay.startdate AND ay.enddate";
                break;
            case AcademicYearFilter.Draft:
                // Upcoming (legacy enum value Draft)
                where += " AND ay.isactive = true AND ay.startdate > CURRENT_DATE";
                break;
            case AcademicYearFilter.Archived:
                // Past (legacy enum value Archived)
                where += " AND ay.isactive = true AND ay.enddate < CURRENT_DATE";
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
            return "(CURRENT_DATE BETWEEN ay.startdate AND ay.enddate) DESC, ay.startdate DESC, ay.id ASC";
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

        return "(CURRENT_DATE BETWEEN ay.startdate AND ay.enddate) DESC, ay.startdate DESC, ay.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }
}
