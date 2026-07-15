using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Audit;
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
        academicYear.Status = AcademicYearStatus.Draft;
        academicYear.IsCurrent = false;
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
                    WHEN ay.status = {(int)AcademicYearStatus.Draft} THEN 'Draft'
                    WHEN ay.status = {(int)AcademicYearStatus.Current} OR ay.iscurrent THEN 'Current'
                    WHEN ay.status = {(int)AcademicYearStatus.Archived} THEN 'Archived'
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

    public async Task<IReadOnlyList<AcademicYearDropdownItem>> GetAcademicYearDropdownAsync(
        bool currentAndFutureOnly = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableAcademicYears;

        string scopeFilter = currentAndFutureOnly
            ? $"""
              AND ay.status IN ({(int)AcademicYearStatus.Current}, {(int)AcademicYearStatus.Archived})
              AND (
                  ay.iscurrent = true
                  OR ay.startdate >= (
                      SELECT cur.startdate
                      FROM {schema}.{table} cur
                      WHERE cur.iscurrent = true AND cur.isactive = true
                      LIMIT 1
                  )
              )
              """
            : $" AND ay.status <> {(int)AcademicYearStatus.Draft}";

        var sql = $@"
            SELECT
                ay.id AS Id,
                ay.title AS Name,
                ay.iscurrent AS IsCurrent,
                ay.startdate AS StartDate
            FROM {schema}.{table} ay
            WHERE ay.isactive = true
            {scopeFilter}
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
        if (Context.OperationalSchema == DatabaseConfig.Schema_Global)
        {
            return null;
        }

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
        int draft = (int)AcademicYearStatus.Draft;
        int current = (int)AcademicYearStatus.Current;
        int archived = (int)AcademicYearStatus.Archived;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var statusValue = await conn.QuerySingleOrDefaultAsync<short?>(
                $"""
                SELECT status FROM {schema}.{DatabaseConfig.TableAcademicYears}
                WHERE id = @Id AND isactive = true;
                """,
                new { Id = id },
                tx).ConfigureAwait(false);

            if (statusValue is null)
            {
                throw new InvalidOperationException("Academic year not found or has been deleted.");
            }

            if (statusValue.Value != draft)
            {
                throw new InvalidOperationException("Only draft academic years can be set as current.");
            }

            var previousCurrentIds = (await conn.QueryAsync<Guid>(
                $"""
                SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
                WHERE isactive = true AND (iscurrent = true OR status = @CurrentStatus);
                """,
                new { CurrentStatus = current },
                tx).ConfigureAwait(false)).ToList();

            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableAcademicYears}
                SET iscurrent = false,
                    status = @ArchivedStatus,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE isactive = true AND (iscurrent = true OR status = @CurrentStatus);
                """,
                new
                {
                    ArchivedStatus = archived,
                    CurrentStatus = current,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow,
                },
                tx).ConfigureAwait(false);

            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableAcademicYears}
                SET iscurrent = true,
                    status = @CurrentStatus,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE id = @Id AND isactive = true;
                """,
                new
                {
                    Id = id,
                    CurrentStatus = current,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow,
                },
                tx).ConfigureAwait(false);

            foreach (Guid previousId in previousCurrentIds.Where(x => x != id))
            {
                await WriteAuditLogInternalAsync(
                    conn,
                    schema,
                    DatabaseConfig.TableAcademicYears,
                    previousId,
                    "Updated",
                    actorId,
                    utcNow,
                    [
                        new FieldChangeDto { Field = "Status", OldValue = "Current", NewValue = "Archived" },
                        new FieldChangeDto { Field = "IsCurrent", OldValue = "True", NewValue = "False" },
                    ],
                    tx).ConfigureAwait(false);
            }

            await WriteAuditLogInternalAsync(
                conn,
                schema,
                DatabaseConfig.TableAcademicYears,
                id,
                "Updated",
                actorId,
                utcNow,
                [
                    new FieldChangeDto { Field = "Status", OldValue = "Draft", NewValue = "Current" },
                    new FieldChangeDto { Field = "IsCurrent", OldValue = "False", NewValue = "True" },
                ],
                tx).ConfigureAwait(false);
        }).ConfigureAwait(false);
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
            // Keep Status / IsCurrent from existing
            ApplyUpdateAudit(existing, actorId, utcNow);

            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableAcademicYears, existing, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var isCurrent = await connection.QuerySingleOrDefaultAsync<bool>(
            $"""
            SELECT iscurrent FROM {schema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """,
            new { Id = id }).ConfigureAwait(false);

        if (isCurrent)
        {
            throw new InvalidOperationException("Cannot delete the current academic year. Set another year as current first.");
        }

        // Check for classes mapped to this academic year
        var classCount = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableClasses} WHERE academicyearid = @Id AND isactive = true;",
            new { Id = id }).ConfigureAwait(false);

        if (classCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete this academic year because it has {classCount} class(es) associated with it.");
        }

        // Check for student academic enrollments
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
                where += $" AND ay.isactive = true AND (ay.iscurrent = true OR ay.status = {(int)AcademicYearStatus.Current})";
                break;
            case AcademicYearFilter.Draft:
                where += $" AND ay.isactive = true AND ay.status = {(int)AcademicYearStatus.Draft}";
                break;
            case AcademicYearFilter.Archived:
                where += $" AND ay.isactive = true AND ay.status = {(int)AcademicYearStatus.Archived}";
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
