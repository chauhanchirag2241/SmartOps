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

        EnsureInsertAudit(academicYear, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var id = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, academicYear, tx)
                .ConfigureAwait(false);
            return id;
        }).ConfigureAwait(false);
    }

    public async Task<AcademicYearEntity?> GetAcademicYearByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;";

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
                CASE WHEN ay.isactive THEN 'Active' ELSE 'Inactive' END AS Status,
                ay.isactive AS IsActive
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

    public async Task<IReadOnlyList<DropdownDto>> GetAcademicYearDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT
                ay.id AS Id,
                ay.title AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableAcademicYears} ay
            WHERE ay.isactive = true
            ORDER BY ay.title ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql).ConfigureAwait(false);
        return items.ToList();
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

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
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
            return "ay.createdon DESC, ay.id ASC";
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

        return "ay.createdon DESC, ay.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }
}
