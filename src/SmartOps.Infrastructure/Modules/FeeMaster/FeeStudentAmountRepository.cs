using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FeeMaster;

public sealed class FeeStudentAmountRepository : BaseRepository, IFeeStudentAmountRepository
{
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public FeeStudentAmountRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _scope = scope;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    public async Task<PagedResult<FeeStudentListModel>> GetStudentsAsync(
        Guid feeMasterId,
        string applicableTo,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        IReadOnlyList<Guid>? classIds,
        string? sortColumn,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var schema = Context.OperationalSchema;
        var identity = Context.IdentitySchema;
        var isStudentWise = string.Equals(applicableTo, "StudentWise", StringComparison.OrdinalIgnoreCase);

        var where = "WHERE s.isactive = true";
        where = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "s", ref where);
        where = AcademicYearScopeSql.AppendStudentHasEnrollmentInScopeYear(
            _scope, "s", schema, ref where);

        if (isStudentWise)
        {
            where += $"""
                 AND EXISTS (
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableFeeStudentAmount} fsa0
                    WHERE fsa0.feemasterid = @FeeMasterId
                      AND fsa0.studentid = s.id
                      AND fsa0.isactive = true)
                """;
        }

        var classIdList = (classIds ?? []).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (classIdList.Length > 0)
        {
            where += " AND a.classid = ANY(@ClassIds)";
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += """
                 AND (
                    u.firstname ILIKE @SearchTerm
                    OR u.lastname ILIKE @SearchTerm
                    OR s.admissionno ILIKE @SearchTerm
                    OR a.rollnumber ILIKE @SearchTerm
                    OR TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) ILIKE @SearchTerm
                )
                """;
            searchTerm = $"%{searchTerm.Trim()}%";
        }

        var orderBy = ResolveOrderBy(sortColumn, sortDirection);
        var enrollmentJoin = _scope.ActiveAcademicYearId.HasValue ? "INNER JOIN" : "LEFT JOIN";

        var fromSql = $"""
            FROM {schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {identity}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            {enrollmentJoin} (
                SELECT sa.studentid,
                       sa.classid,
                       sa.rollnumber,
                       sa.isactive,
                       ROW_NUMBER() OVER(
                           PARTITION BY sa.studentid
                           ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
                WHERE {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
            ) a ON s.id = a.studentid AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = a.classid
            LEFT JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            """;

        var countSql = $"SELECT COUNT(*) {fromSql} {where};";
        var querySql = $"""
            SELECT
                s.id AS StudentId,
                TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) AS StudentName,
                a.rollnumber AS RollNumber,
                s.admissionno AS AdmissionNo,
                cg.classname AS ClassName,
                c.section AS Section,
                a.classid AS ClassId,
                (
                    SELECT COALESCE(SUM(
                        CASE
                            WHEN fsa.isexcluded = true THEN 0
                            WHEN fsa.amount IS NOT NULL THEN fsa.amount
                            ELSE COALESCE(
                                h.amount,
                                (
                                    SELECT SUM(pa.amount)
                                    FROM {schema}.{DatabaseConfig.TableFeeHeadPeriodAmount} pa
                                    WHERE pa.feeheadid = h.id
                                      AND pa.isactive = true
                                      AND pa.classgroupid = c.classgroupid
                                ),
                                0
                            )
                        END
                    ), 0)
                    FROM {schema}.{DatabaseConfig.TableFeeHead} h
                    LEFT JOIN {schema}.{DatabaseConfig.TableFeeStudentAmount} fsa
                        ON fsa.feeheadid = h.id
                       AND fsa.studentid = s.id
                       AND fsa.isactive = true
                    WHERE h.feemasterid = @FeeMasterId AND h.isactive = true
                ) AS AmountSummary,
                EXISTS (
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableFeeHead} h2
                    WHERE h2.feemasterid = @FeeMasterId AND h2.isactive = true AND h2.iseditable = true
                ) AS CanEdit,
                {(isStudentWise
                    ? "true"
                    : $@"EXISTS (
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableFeeHead} h3
                    WHERE h3.feemasterid = @FeeMasterId AND h3.isactive = true AND h3.ismandatory = false
                )")} AS CanRemove,
                EXISTS (
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableFeeStudentAmount} fsa2
                    WHERE fsa2.feemasterid = @FeeMasterId
                      AND fsa2.studentid = s.id
                      AND fsa2.isactive = true
                ) AS HasOverrides
            {fromSql}
            {where}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var param = new
        {
            FeeMasterId = feeMasterId,
            SearchTerm = searchTerm,
            ClassIds = classIdList,
            ScopeAcademicYearId = _scope.ActiveAcademicYearId,
            ActiveBranchId = _branchContext.ActiveBranchId,
            Offset = Math.Max(0, (pageIndex - 1) * pageSize),
            PageSize = pageSize,
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, param, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var items = (await connection.QueryAsync<FeeStudentListModel>(
            new CommandDefinition(querySql, param, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return new PagedResult<FeeStudentListModel>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task<FeeStudentDetailModel?> GetStudentDetailAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var identity = Context.IdentitySchema;

        var nameSql = $"""
            SELECT TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, ''))
            FROM {schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {identity}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            WHERE s.id = @StudentId;
            """;
        var name = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(nameSql, new { StudentId = studentId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (name is null)
        {
            return null;
        }

        var headsSql = $"""
            SELECT
                h.id AS FeeHeadId,
                h.feeheadname AS FeeHeadName,
                h.ismandatory AS IsMandatory,
                h.iseditable AS IsEditable,
                COALESCE(
                    h.amount,
                    (
                        SELECT SUM(pa.amount)
                        FROM {schema}.{DatabaseConfig.TableFeeHeadPeriodAmount} pa
                        WHERE pa.feeheadid = h.id
                          AND pa.isactive = true
                          AND pa.classgroupid = (
                              SELECT c.classgroupid
                              FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
                              INNER JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid
                              WHERE sa.studentid = @StudentId
                                AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause("sa")}
                              ORDER BY sa.isactive DESC, sa.createdon DESC
                              LIMIT 1
                          )
                    )
                ) AS DefaultAmount,
                fsa.amount AS Amount,
                COALESCE(fsa.isexcluded, false) AS IsExcluded,
                (fsa.id IS NOT NULL) AS HasOverride
            FROM {schema}.{DatabaseConfig.TableFeeHead} h
            LEFT JOIN {schema}.{DatabaseConfig.TableFeeStudentAmount} fsa
                ON fsa.feeheadid = h.id
               AND fsa.studentid = @StudentId
               AND fsa.isactive = true
            WHERE h.feemasterid = @FeeMasterId AND h.isactive = true
            ORDER BY h.feeheadname ASC;
            """;

        var heads = (await connection.QueryAsync<FeeStudentHeadAmountModel>(
            new CommandDefinition(
                headsSql,
                new
                {
                    FeeMasterId = feeMasterId,
                    StudentId = studentId,
                    ScopeAcademicYearId = _scope.ActiveAcademicYearId,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        foreach (var head in heads)
        {
            if (!head.HasOverride || head.IsExcluded)
            {
                // Effective display amount for UI: excluded → null; else override or default
                if (head.IsExcluded)
                {
                    head.Amount = null;
                }
                else
                {
                    head.Amount ??= head.DefaultAmount;
                }
            }
        }

        return new FeeStudentDetailModel
        {
            StudentId = studentId,
            StudentName = name,
            Heads = heads,
        };
    }

    public async Task UpsertOverridesAsync(
        Guid feeMasterId,
        Guid studentId,
        Guid branchId,
        IReadOnlyList<FeeStudentAmountEntity> rows,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var resolvedBranch = await _branchWrite
            .ResolveWriteBranchIdAsync(branchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (var row in rows)
            {
                row.FeeMasterId = feeMasterId;
                row.StudentId = studentId;
                row.BranchId = resolvedBranch;

                var existingId = await conn.ExecuteScalarAsync<Guid?>(
                    new CommandDefinition(
                        $"""
                        SELECT id FROM {schema}.{DatabaseConfig.TableFeeStudentAmount}
                        WHERE feeheadid = @FeeHeadId AND studentid = @StudentId AND isactive = true
                        LIMIT 1;
                        """,
                        new { row.FeeHeadId, StudentId = studentId },
                        transaction: tx,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);

                if (existingId.HasValue)
                {
                    row.Id = existingId.Value;
                    var existing = await conn.QuerySingleAsync<FeeStudentAmountEntity>(
                        new CommandDefinition(
                            $"SELECT * FROM {schema}.{DatabaseConfig.TableFeeStudentAmount} WHERE id = @Id",
                            new { Id = existingId.Value },
                            transaction: tx,
                            cancellationToken: cancellationToken))
                        .ConfigureAwait(false);

                    row.VersionNo = existing.VersionNo;
                    row.CreatedBy = existing.CreatedBy;
                    row.CreatedOn = existing.CreatedOn;
                    ApplyUpdateAudit(row, ResolveUpdateActor(), utcNow);
                    await UpdateAsync(conn, schema, DatabaseConfig.TableFeeStudentAmount, row, tx, "Id")
                        .ConfigureAwait(false);
                }
                else
                {
                    if (row.Id == Guid.Empty)
                    {
                        row.Id = Guid.NewGuid();
                    }

                    EnsureInsertAudit(row, utcNow);
                    await InsertAsync(conn, schema, DatabaseConfig.TableFeeStudentAmount, row, tx)
                        .ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task SoftDeleteByStudentAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableFeeStudentAmount}
                    SET isactive = false,
                        updatedby = @ActorId,
                        updatedon = @UtcNow,
                        versionno = versionno + 1
                    WHERE feemasterid = @FeeMasterId
                      AND studentid = @StudentId
                      AND isactive = true;
                    """,
                    new { FeeMasterId = feeMasterId, StudentId = studentId, ActorId = actorId, UtcNow = utcNow },
                    transaction: tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<bool> StudentExistsOnMasterAsync(
        Guid feeMasterId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableFeeStudentAmount}
                WHERE feemasterid = @FeeMasterId AND studentid = @StudentId AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { FeeMasterId = feeMasterId, StudentId = studentId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static string ResolveOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "cg.classname ASC NULLS LAST, c.section ASC NULLS LAST, a.rollnumber ASC NULLS LAST, s.id ASC";
        }

        return sortColumn.ToLowerInvariant() switch
        {
            "studentname" or "name" => $"u.firstname {direction}, u.lastname {direction}, s.id ASC",
            "rollnumber" => $"a.rollnumber {direction} NULLS LAST, s.id ASC",
            "classname" or "class" => $"cg.classname {direction} NULLS LAST, c.section {direction}, s.id ASC",
            "section" => $"c.section {direction} NULLS LAST, s.id ASC",
            "admissionno" => $"s.admissionno {direction}, s.id ASC",
            "amountsummary" => $"AmountSummary {direction}, s.id ASC",
            _ => "cg.classname ASC NULLS LAST, c.section ASC NULLS LAST, a.rollnumber ASC NULLS LAST, s.id ASC"
        };
    }
}
