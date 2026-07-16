using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Application.Modules.Student.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Modules.Fees;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Student;

/// <summary>
/// Student aggregate persistence. Pattern: connection → <see cref="BaseRepository"/> transaction helpers for writes;
/// list query split into filter / order / SQL builders as a template for other modules.
/// </summary>
public sealed class StudentRepository : BaseRepository, IStudentRepository
{
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IFeeStructureRepository _feeStructureRepo;
    private readonly IClassFeeAmountRepository _classFeeAmountRepo;
    private readonly IStudentFeeInstallmentRepository _studentFeeInstallmentRepo;
    private readonly IFeeCollectionRepository _feeCollectionRepo;
    private readonly IBranchScopedWriteHelper _branchWrite;

    private static readonly string[] RelatedTablesForSoftDelete =
    {
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentFeeHeadAssignments,
        DatabaseConfig.TableStudentCustomFields,
    };

    public StudentRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IFeeStructureRepository feeStructureRepo,
        IClassFeeAmountRepository classFeeAmountRepo,
        IStudentFeeInstallmentRepository studentFeeInstallmentRepo,
        IFeeCollectionRepository feeCollectionRepo,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _feeStructureRepo = feeStructureRepo;
        _classFeeAmountRepo = classFeeAmountRepo;
        _studentFeeInstallmentRepo = studentFeeInstallmentRepo;
        _feeCollectionRepo = feeCollectionRepo;
        _branchWrite = branchWrite;
        _scope = scope;
        _branchContext = branchContext;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default)
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            if (student.Id == Guid.Empty)
            {
                student.Id = Guid.NewGuid();
            }

            EnsureInsertAudit(student, utcNow);

            student.BranchId = await _branchWrite
                .ResolveWriteBranchIdAsync(student.BranchId, cancellationToken)
                .ConfigureAwait(false);

            var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

            Guid studentId = await WithTransactionAsync(connection, async (conn, tx) =>
            {
                var id = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, student, tx)
                    .ConfigureAwait(false);
                student.Id = id;

                await InsertChildCollectionsAsync(conn, tx, id, student, utcNow).ConfigureAwait(false);

                return id;
            }).ConfigureAwait(false);

            await GenerateStudentFeeInstallmentsAfterCreateAsync(studentId, student, cancellationToken)
                .ConfigureAwait(false);

            return studentId;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StudentEntity?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = BuildStudentDetailSql(includeInactive);

        using var multi = await connection.QueryMultipleAsync(sql, new { Id = id }).ConfigureAwait(false);
        var student = await multi.ReadSingleOrDefaultAsync<StudentEntity>().ConfigureAwait(false);

        if (student is null)
        {
            return null;
        }

        student.Parents = (await multi.ReadAsync<StudentParentEntity>().ConfigureAwait(false)).ToList();
        student.Academics = (await multi.ReadAsync<StudentAcademicEntity>().ConfigureAwait(false)).ToList();
        student.FeeHeadAssignments = (await multi.ReadAsync<StudentFeeHeadAssignmentEntity>().ConfigureAwait(false)).ToList();
        student.PreviousSchools = (await multi.ReadAsync<StudentPreviousSchoolEntity>().ConfigureAwait(false)).ToList();
        student.CustomFields = (await multi.ReadAsync<StudentCustomFieldEntity>().ConfigureAwait(false)).ToList();
        student.Documents = (await multi.ReadAsync<StudentDocumentEntity>().ConfigureAwait(false)).ToList();

        return student;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StudentListModel>> GetAllStudentsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StudentFilter filter = StudentFilter.Active,
        Guid? classId = null,
        IReadOnlyList<Guid>? classIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

            var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

            IReadOnlyList<Guid>? requestedClassIds = classIds;
            if ((requestedClassIds == null || requestedClassIds.Count == 0) && classId.HasValue)
            {
                requestedClassIds = [classId.Value];
            }

            var effectiveClassIds = ScopeSqlBuilder.ResolveClassIdsFilter(_scope, requestedClassIds);
            if (effectiveClassIds != null && effectiveClassIds.Count == 0)
            {
                return new PagedResult<StudentListModel>
                {
                    Items = [],
                    TotalCount = 0,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }

            var whereClause = BuildListWhereClause(filter, effectiveClassIds, ref searchTerm);
            whereClause = AcademicYearScopeSql.AppendStudentHasEnrollmentInScopeYear(
                _scope, "s", Context.OperationalSchema, ref whereClause);
            whereClause = ScopeSqlBuilder.AppendStudentScopeFilter(
                _scope, "s", Context.OperationalSchema, ref whereClause);
            whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "s", ref whereClause);
            var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
            string enrollmentJoin = _scope.ActiveAcademicYearId.HasValue ? "INNER JOIN" : "LEFT JOIN";

            var schema = Context.OperationalSchema;
            var students = DatabaseConfig.TableStudents;
            var academics = DatabaseConfig.TableStudentAcademics;
            var attendance = DatabaseConfig.TableAttendance;
            var classFeeAmounts = DatabaseConfig.TableClassFeeAmounts;
            var feeTypes = DatabaseConfig.TableFeeTypes;
            var feePayments = DatabaseConfig.TableFeePayments;

            var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{students} s
            {whereClause};";

            var querySql = $@"
            SELECT
                s.id AS Id,
                a.classid AS ClassId,
                TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS Name,
                COALESCE(s.email, 'N/A') AS Email,
                s.admissionno AS AdmNo,
                a.rollnumber AS RollNumber,
                CASE
                    WHEN c.classname IS NOT NULL THEN 
                        c.classname || ' — ' || 
                        CASE c.section 
                            WHEN 1 THEN 'A' WHEN 2 THEN 'B' WHEN 3 THEN 'C' WHEN 4 THEN 'D' ELSE 'N/A' 
                        END
                    ELSE 'N/A'
                END AS Class,
                COALESCE(att_stats.attendance_pct, 0) AS Attendance,
                CASE
                    WHEN COALESCE(fee_totals.total_fees, 0) <= 0 THEN 'Pending'
                    WHEN COALESCE(paid_totals.paid, 0) >= COALESCE(fee_totals.total_fees, 0) THEN 'Paid'
                    WHEN COALESCE(paid_totals.paid, 0) > 0 THEN 'Partial'
                    ELSE 'Overdue'
                END AS Fees,
                s.isactive AS IsActive,
                COALESCE(a.isactive, false) AS EnrollmentIsActive
            FROM {schema}.{students} s
            {enrollmentJoin} (
                SELECT sa.studentid,
                       sa.classid,
                       sa.rollnumber,
                       sa.feestructureversionid,
                       sa.academicyearid,
                       sa.isactive,
                       ROW_NUMBER() OVER(
                           PARTITION BY sa.studentid
                           ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                FROM {schema}.{academics} sa
                WHERE {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
            ) a ON s.id = a.studentid AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c
                ON c.id = a.classid AND c.academicyearid = a.academicyearid
            LEFT JOIN LATERAL (
                SELECT CAST(ROUND(
                    100.0 * COUNT(*) FILTER (WHERE att.status IN (1, 4))
                    / NULLIF(COUNT(*), 0)
                ) AS INT) AS attendance_pct
                FROM {schema}.{attendance} att
                WHERE att.studentid = s.id
                  AND att.classid = a.classid
                  AND att.isactive = true
            ) att_stats ON a.classid IS NOT NULL
            LEFT JOIN LATERAL (
                SELECT SUM(cfa.amount) AS total_fees
                FROM {schema}.{classFeeAmounts} cfa
                INNER JOIN {schema}.{feeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                WHERE cfa.classid = a.classid
                  AND cfa.feestructureversionid = a.feestructureversionid
                  AND cfa.isactive = true
                  AND {StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(schema, "cfa.feetypeid", "s.id", "a.feestructureversionid")}
            ) fee_totals ON a.classid IS NOT NULL AND a.feestructureversionid IS NOT NULL
            LEFT JOIN LATERAL (
                SELECT SUM(fp.amount) AS paid
                FROM {schema}.{feePayments} fp
                WHERE fp.studentid = s.id
                  AND fp.feestructureversionid = a.feestructureversionid
                  AND fp.isactive = true
            ) paid_totals ON a.feestructureversionid IS NOT NULL
            {whereClause}
            ORDER BY {orderBy}";

            var result = await GetPagedResultAsync<StudentListModel>(
                    connection,
                    querySql,
                    countSql,
                    new
                    {
                        SearchTerm = searchTerm,
                        ClassIds = effectiveClassIds?.ToArray(),
                        ScopeStudentIds = _scope.AllowedStudentIds.ToArray(),
                        ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                        ScopeAcademicYearId = _scope.ActiveAcademicYearId,
                        ActiveBranchId = _branchContext.ActiveBranchId
                    },
                    pageIndex,
                    pageSize)
                .ConfigureAwait(false);

            var items = result.Items.ToList();
            NormalizeListItems(items);
            result.Items = items;
            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(student, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, student, tx, "Id")
                .ConfigureAwait(false);

            await UpdateChildCollectionsAsync(conn, tx, student, actorId, utcNow).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<bool> AdmissionNoExistsAsync(
        string admissionNo,
        Guid branchId,
        Guid? excludingStudentId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
SELECT EXISTS (
    SELECT 1
    FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudents}
    WHERE lower(admissionno) = lower(@AdmissionNo)
      AND branchid = @BranchId
      AND isactive = true
      AND (@ExcludingStudentId IS NULL OR id <> @ExcludingStudentId)
);
""";

        return await connection.QuerySingleAsync<bool>(
                sql,
                new { AdmissionNo = admissionNo.Trim(), BranchId = branchId, ExcludingStudentId = excludingStudentId })
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetStudentUserIdAsync(Guid studentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableStudents}
SET userid = @UserId, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
WHERE id = @StudentId AND isactive = true
""";
        await connection.ExecuteAsync(sql, new
        {
            StudentId = studentId,
            UserId = userId,
            Now = DateTime.UtcNow,
            Actor = ResolveUpdateActor()
        }).ConfigureAwait(false);
    }

    public async Task SetStudentParentUserIdAsync(Guid parentId, Guid userId, CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableStudentParents}
SET userid = @UserId, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
WHERE id = @ParentId AND isactive = true
""";
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    ParentId = parentId,
                    UserId = userId,
                    Now = DateTime.UtcNow,
                    Actor = ResolveUpdateActor()
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteStudentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, id, tx)
                .ConfigureAwait(false);

            foreach (var table in RelatedTablesForSoftDelete)
            {
                await SoftDeleteRelatedAsync(conn, Context.OperationalSchema, table, "StudentId", id, tx)
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task RecoverStudentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sqlCheckClass = $"""
            SELECT c.isactive 
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentAcademics} sa
            INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c ON sa.classid = c.id
            WHERE sa.studentid = @Id
            ORDER BY sa.createdon DESC
            LIMIT 1;
        """;
        var isClassActive = await connection.ExecuteScalarAsync<bool?>(sqlCheckClass, new { Id = id }).ConfigureAwait(false);
        
        if (isClassActive.HasValue && !isClassActive.Value)
        {
            throw new InvalidOperationException("Cannot recover student because the assigned class is inactive. Please recover the class first.");
        }

        var now = DateTime.UtcNow;
        var actor = ResolveUpdateActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var updateStudentSql = $"""
                UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableStudents}
                SET isactive = true, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
                WHERE id = @Id AND isactive = false;
            """;
            await conn.ExecuteAsync(updateStudentSql, new { Id = id, Now = now, Actor = actor }, tx).ConfigureAwait(false);

            foreach (var table in RelatedTablesForSoftDelete)
            {
                var updateRelatedSql = $"""
                    UPDATE {Context.OperationalSchema}.{table}
                    SET isactive = true, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
                    WHERE studentid = @Id AND isactive = false;
                """;
                await conn.ExecuteAsync(updateRelatedSql, new { Id = id, Now = now, Actor = actor }, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    #region List query helpers

    private string BuildListWhereClause(StudentFilter filter, IReadOnlyList<Guid>? classIds, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        switch (filter)
        {
            case StudentFilter.Active:
                where += " AND s.isactive = true";
                break;
            case StudentFilter.Inactive:
                where += " AND s.isactive = false";
                break;
            case StudentFilter.FeeOverdue:
                var g = Context.OperationalSchema;
                where += $@"
                AND EXISTS (
                    SELECT 1
                    FROM {g}.{DatabaseConfig.TableStudentAcademics} sa
                    LEFT JOIN LATERAL (
                        SELECT SUM(cfa.amount) AS total_fees
                        FROM {g}.{DatabaseConfig.TableClassFeeAmounts} cfa
                        INNER JOIN {g}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                        WHERE cfa.classid = sa.classid
                          AND cfa.feestructureversionid = sa.feestructureversionid
                          AND cfa.isactive = true
                    ) ft ON true
                    LEFT JOIN LATERAL (
                        SELECT SUM(fp.amount) AS paid
                        FROM {g}.{DatabaseConfig.TableFeePayments} fp
                        WHERE fp.studentid = sa.studentid
                          AND fp.feestructureversionid = sa.feestructureversionid
                          AND fp.isactive = true
                    ) pt ON true
                    WHERE sa.studentid = s.id
                      AND sa.isactive = true
                      AND COALESCE(ft.total_fees, 0) > 0
                      AND COALESCE(pt.paid, 0) < COALESCE(ft.total_fees, 0)
                )";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (s.firstname ILIKE @SearchTerm OR s.lastname ILIKE @SearchTerm OR s.admissionno ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        if (classIds != null && classIds.Count > 0)
        {
            where += $@"
                AND EXISTS (
                    SELECT 1
                    FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentAcademics} sa
                    WHERE sa.studentid = s.id
                      AND sa.classid = ANY(@ClassIds)
                      AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
                )";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "s.createdon DESC, s.id ASC";
        }

        if (IsSortKey(sortColumn, "student", "name"))
        {
            return $"s.firstname {direction}, s.lastname {direction}, s.id ASC";
        }

        if (IsSortKey(sortColumn, "admNo"))
        {
            return $"s.admissionno {direction}, s.id ASC";
        }

        if (IsSortKey(sortColumn, "class"))
        {
            return $"c.classname {direction}, c.section {direction}, s.id ASC";
        }

        return "s.createdon DESC, s.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }

    private static void NormalizeListItems(IList<StudentListModel> items)
    {
        foreach (var student in items)
        {
            if (string.IsNullOrEmpty(student.AdmNo))
            {
                student.AdmNo = "N/A";
            }

            if (string.IsNullOrEmpty(student.Fees))
            {
                student.Fees = "Pending";
            }

            student.Status = student.IsActive ? "Active" : "Inactive";
        }
    }

    #endregion

    #region Detail SQL

    private string BuildStudentDetailSql(bool includeInactive)
    {
        var g = Context.OperationalSchema;
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        return $@"
            SELECT * FROM {g}.{DatabaseConfig.TableStudents} WHERE id = @Id{activeFilter};
            SELECT * FROM {g}.{DatabaseConfig.TableStudentParents} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentAcademics}
            WHERE studentid = @Id
            ORDER BY isactive DESC, createdon DESC;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentFeeHeadAssignments} WHERE studentid = @Id AND isactive = true;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentPreviousSchools} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentCustomFields} WHERE studentid = @Id AND isactive = true ORDER BY createdon, fieldlabel;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentDocuments} WHERE studentid = @Id AND isactive = true;
        ";
    }

    #endregion

    #region Child rows (create / update)

    private async Task InsertChildCollectionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid studentId,
        StudentEntity student,
        DateTime utcNow)
    {
        if (student.Parents is { Count: > 0 })
        {
            foreach (var parent in student.Parents)
            {
                parent.Id = Guid.NewGuid();
                parent.StudentId = studentId;
                EnsureInsertAudit(parent, utcNow);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentParents,
                        parent,
                        transaction)
                    .ConfigureAwait(false);
            }
        }

        if (student.Academics is { Count: > 0 })
        {
            foreach (var academic in student.Academics)
            {
                if (!academic.FeeStructureVersionId.HasValue || academic.FeeStructureVersionId == Guid.Empty)
                {
                    var admissionVersion = await _feeStructureRepo
                        .GetAdmissionVersionForYearAsync(academic.AcademicYearId, CancellationToken.None)
                        .ConfigureAwait(false);
                    if (admissionVersion is not null)
                    {
                        academic.FeeStructureVersionId = admissionVersion.Id;
                    }
                }

                academic.Id = Guid.NewGuid();
                academic.StudentId = studentId;
                EnsureInsertAudit(academic, utcNow);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentAcademics,
                        academic,
                        transaction)
                    .ConfigureAwait(false);
            }
        }

        if (student.PreviousSchools is { Count: > 0 })
        {
            foreach (var prevSchool in student.PreviousSchools)
            {
                prevSchool.Id = Guid.NewGuid();
                prevSchool.StudentId = studentId;
                EnsureInsertAudit(prevSchool, utcNow);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentPreviousSchools,
                        prevSchool,
                        transaction)
                    .ConfigureAwait(false);
            }
        }

        StudentAcademicEntity? admissionAcademic = student.Academics.FirstOrDefault();
        Guid? feeVersionId = admissionAcademic?.FeeStructureVersionId;
        if (!feeVersionId.HasValue || feeVersionId.Value == Guid.Empty)
        {
            feeVersionId = student.Academics
                .Select(a => a.FeeStructureVersionId)
                .FirstOrDefault(v => v.HasValue && v.Value != Guid.Empty);
        }

        if (student.FeeHeadAssignments is { Count: > 0 } && feeVersionId.HasValue)
        {
            foreach (var assignment in student.FeeHeadAssignments)
            {
                assignment.Id = Guid.NewGuid();
                assignment.StudentId = studentId;
                assignment.FeeStructureVersionId = feeVersionId.Value;
                EnsureInsertAudit(assignment, utcNow);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentFeeHeadAssignments,
                        assignment,
                        transaction)
                    .ConfigureAwait(false);
            }
        }

        await InsertCustomFieldsAsync(connection, transaction, studentId, student.CustomFields, utcNow)
            .ConfigureAwait(false);
    }

    private async Task GenerateStudentFeeInstallmentsAfterCreateAsync(
        Guid studentId,
        StudentEntity student,
        CancellationToken cancellationToken)
    {
        StudentAcademicEntity? academic = student.Academics.FirstOrDefault();
        if (academic is null || academic.ClassId == Guid.Empty || academic.AcademicYearId == Guid.Empty)
        {
            return;
        }

        Guid versionId = academic.FeeStructureVersionId ?? Guid.Empty;
        if (versionId == Guid.Empty)
        {
            var admissionVersion = await _feeStructureRepo
                .GetAdmissionVersionForYearAsync(academic.AcademicYearId, cancellationToken)
                .ConfigureAwait(false);
            versionId = admissionVersion?.Id ?? Guid.Empty;
        }

        if (versionId == Guid.Empty)
        {
            return;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = student.FeeHeadAssignments;
        await _studentFeeInstallmentRepo
            .GenerateForStudentAdmissionAsync(
                studentId,
                academic.ClassId,
                versionId,
                academic.AcademicYearId,
                assignments,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task UpdateChildCollectionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        StudentEntity student,
        Guid actorId,
        DateTime utcNow)
    {
        if (student.Parents is not null)
        {
            foreach (var parent in student.Parents)
            {
                parent.StudentId = student.Id;
                ApplyUpdateAudit(parent, actorId, utcNow);
                await UpdateAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentParents,
                        parent,
                        transaction,
                        "StudentId",
                        "RelationType")
                    .ConfigureAwait(false);
            }
        }

        if (student.Academics is not null)
        {
            foreach (var academic in student.Academics)
            {
                academic.StudentId = student.Id;

                StudentAcademicEntity? existingAcademic = await GetAcademicRecordAsync(
                        connection,
                        Context.OperationalSchema,
                        student.Id,
                        academic.AcademicYearId,
                        transaction)
                    .ConfigureAwait(false);
                if (existingAcademic is not null)
                {
                    if (academic.Id == Guid.Empty)
                    {
                        academic.Id = existingAcademic.Id;
                    }

                    if (!academic.FeeStructureVersionId.HasValue || academic.FeeStructureVersionId.Value == Guid.Empty)
                    {
                        academic.FeeStructureVersionId = existingAcademic.FeeStructureVersionId;
                    }
                }

                if (academic.ClassId != Guid.Empty && academic.AcademicYearId != Guid.Empty)
                {
                    bool classValid = await connection.QuerySingleAsync<bool>(
                        $"""
                        SELECT EXISTS(
                            SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses}
                            WHERE id = @ClassId AND academicyearid = @AcademicYearId AND isactive = true);
                        """,
                        new { academic.ClassId, academic.AcademicYearId },
                        transaction).ConfigureAwait(false);
                    if (!classValid)
                    {
                        throw new InvalidOperationException(
                            "Selected class does not belong to the chosen academic year.");
                    }
                }

                ApplyUpdateAudit(academic, actorId, utcNow);
                await UpdateAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentAcademics,
                        academic,
                        transaction,
                        "StudentId",
                        "AcademicYearId")
                    .ConfigureAwait(false);
            }
        }

        if (student.PreviousSchools is not null)
        {
            foreach (var prev in student.PreviousSchools)
            {
                prev.StudentId = student.Id;
                ApplyUpdateAudit(prev, actorId, utcNow);
                await UpdateAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentPreviousSchools,
                        prev,
                        transaction,
                        "Id")
                    .ConfigureAwait(false);
            }
        }

        if (student.CustomFields is not null)
        {
            await ReplaceCustomFieldsAsync(connection, transaction, student.Id, student.CustomFields, actorId, utcNow)
                .ConfigureAwait(false);
        }
    }

    private async Task InsertCustomFieldsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid studentId,
        List<StudentCustomFieldEntity>? customFields,
        DateTime utcNow)
    {
        if (customFields is not { Count: > 0 })
        {
            return;
        }

        foreach (var field in customFields)
        {
            if (string.IsNullOrWhiteSpace(field.FieldLabel) && string.IsNullOrWhiteSpace(field.FieldValue))
            {
                continue;
            }

            field.Id = Guid.NewGuid();
            field.StudentId = studentId;
            EnsureInsertAudit(field, utcNow);
            await InsertWithoutReturnAsync(
                    connection,
                    Context.OperationalSchema,
                    DatabaseConfig.TableStudentCustomFields,
                    field,
                    transaction)
                .ConfigureAwait(false);
        }
    }

    private async Task ReplaceCustomFieldsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid studentId,
        List<StudentCustomFieldEntity>? customFields,
        Guid actorId,
        DateTime utcNow)
    {
        await SoftDeleteRelatedAsync(
                connection,
                Context.OperationalSchema,
                DatabaseConfig.TableStudentCustomFields,
                "StudentId",
                studentId,
                transaction)
            .ConfigureAwait(false);

        await InsertCustomFieldsAsync(connection, transaction, studentId, customFields, utcNow)
            .ConfigureAwait(false);
    }

    private static async Task<StudentAcademicEntity?> GetAcademicRecordAsync(
        IDbConnection connection,
        string schema,
        Guid studentId,
        Guid academicYearId,
        IDbTransaction? transaction,
        bool activeOnly = true)
    {
        string activeFilter = activeOnly ? " AND isactive = true" : string.Empty;
        string sql = $"""
            SELECT id AS Id, studentid AS StudentId, admissiondate AS AdmissionDate,
                   academicyearid AS AcademicYearId, classid AS ClassId,
                   feestructureversionid AS FeeStructureVersionId, rollnumber AS RollNumber,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {schema}.{DatabaseConfig.TableStudentAcademics}
            WHERE studentid = @StudentId
              AND academicyearid = @AcademicYearId{activeFilter}
            ORDER BY isactive DESC, createdon DESC
            LIMIT 1;
            """;

        return await connection
            .QueryFirstOrDefaultAsync<StudentAcademicEntity>(
                sql,
                new { StudentId = studentId, AcademicYearId = academicYearId },
                transaction)
            .ConfigureAwait(false);
    }

    public async Task<int> GetMaxRollNumberAsync(Guid academicYearId, Guid classId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableStudentAcademics;

        var sql = $@"
            SELECT COALESCE(MAX(CAST(NULLIF(rollnumber, '') AS INTEGER)), 0)
            FROM {schema}.{table}
            WHERE academicyearid = @AcademicYearId 
              AND classid = @ClassId 
              AND isactive = true";

        return await connection.QuerySingleAsync<int>(sql, new { AcademicYearId = academicYearId, ClassId = classId })
            .ConfigureAwait(false);
    }

    public async Task<string?> GetPromoteTargetValidationErrorAsync(
        Guid targetAcademicYearId,
        Guid targetClassId,
        CancellationToken cancellationToken = default)
    {
        if (targetAcademicYearId == Guid.Empty || targetClassId == Guid.Empty)
        {
            return "Select target academic year and class.";
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        bool classValid = await connection.QuerySingleAsync<bool>(
            $"""
            SELECT EXISTS(
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                WHERE id = @ClassId AND academicyearid = @TargetYearId AND isactive = true);
            """,
            new { ClassId = targetClassId, TargetYearId = targetAcademicYearId }).ConfigureAwait(false);

        if (!classValid)
        {
            return "No classes in target year. Create classes first.";
        }

        var admissionFeeStructure = await _feeStructureRepo
            .GetAdmissionVersionForYearAsync(targetAcademicYearId, cancellationToken)
            .ConfigureAwait(false);
        if (admissionFeeStructure is null)
        {
            return "No published fee structure for the target academic year. Publish the fee structure first.";
        }

        bool classHasConfiguredAmounts = await _classFeeAmountRepo
            .ClassHasConfiguredAmountsAsync(targetClassId, admissionFeeStructure.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!classHasConfiguredAmounts)
        {
            return "Set class-wise fee amounts for the selected class in the target academic year before promoting students.";
        }

        return null;
    }

    public async Task<IReadOnlyList<PromotePendingFeeRow>> GetPromotePendingFeesAsync(
        Guid sourceAcademicYearId,
        IReadOnlyList<Guid> studentIds,
        CancellationToken cancellationToken = default)
    {
        if (sourceAcademicYearId == Guid.Empty || studentIds.Count == 0)
        {
            return Array.Empty<PromotePendingFeeRow>();
        }

        var rows = new List<PromotePendingFeeRow>();
        foreach (Guid studentId in studentIds.Distinct())
        {
            FeeCollectionStudentRow? row = await _feeCollectionRepo
                .GetStudentRowAsync(studentId, sourceAcademicYearId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                continue;
            }

            decimal pending = Math.Max(0, row.TotalFees - row.PaidAmount);
            if (pending <= 0)
            {
                continue;
            }

            rows.Add(new PromotePendingFeeRow(
                studentId,
                row.StudentName,
                row.TotalFees,
                row.PaidAmount,
                pending));
        }

        return rows;
    }

    public async Task<PromoteStudentsResult> PromoteStudentsAsync(
        Guid sourceAcademicYearId,
        Guid targetAcademicYearId,
        IReadOnlyList<PromoteStudentEntry> students,
        CancellationToken cancellationToken = default)
    {
        if (sourceAcademicYearId == targetAcademicYearId)
        {
            return new PromoteStudentsResult(0, ["Source and target academic year must be different."]);
        }

        if (students.Count == 0)
        {
            return new PromoteStudentsResult(0, ["At least one student is required."]);
        }

        var errors = new List<string>();
        foreach (Guid targetClassId in students.Select(s => s.TargetClassId).Distinct())
        {
            string? feeError = await GetPromoteTargetValidationErrorAsync(
                targetAcademicYearId, targetClassId, cancellationToken).ConfigureAwait(false);
            if (feeError is not null)
            {
                errors.Add(feeError);
            }
        }

        if (errors.Count > 0)
        {
            return new PromoteStudentsResult(0, errors);
        }

        var targetFeeVersion = await _feeStructureRepo
            .GetAdmissionVersionForYearAsync(targetAcademicYearId, cancellationToken)
            .ConfigureAwait(false);
        if (targetFeeVersion is null)
        {
            return new PromoteStudentsResult(0, [
                "No published fee structure for the target academic year. Publish the fee structure first."
            ]);
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveInsertActor();
        int promoted = 0;
        int feesTransferred = 0;
        decimal totalPendingTransferred = 0m;
        var pendingFeeTransfers = new List<(
            Guid StudentId,
            Guid SourceClassId,
            Guid SourceFeeStructureVersionId,
            Guid TargetClassId)>();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (var entry in students)
            {
                bool classValid = await conn.QuerySingleAsync<bool>(
                    $"""
                    SELECT EXISTS(
                        SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                        WHERE id = @ClassId AND academicyearid = @TargetYearId AND isactive = true);
                    """,
                    new { ClassId = entry.TargetClassId, TargetYearId = targetAcademicYearId },
                    tx).ConfigureAwait(false);

                if (!classValid)
                {
                    errors.Add($"Student {entry.StudentId}: target class is invalid for the target academic year.");
                    continue;
                }

                StudentAcademicEntity? sourceRecord = await GetAcademicRecordAsync(
                    conn, schema, entry.StudentId, sourceAcademicYearId, tx).ConfigureAwait(false);

                if (sourceRecord is null)
                {
                    StudentAcademicEntity? inactiveSource = await GetAcademicRecordAsync(
                        conn, schema, entry.StudentId, sourceAcademicYearId, tx, activeOnly: false)
                        .ConfigureAwait(false);
                    if (inactiveSource is not null)
                    {
                        StudentAcademicEntity? activeTarget = await GetAcademicRecordAsync(
                            conn, schema, entry.StudentId, targetAcademicYearId, tx, activeOnly: true)
                            .ConfigureAwait(false);
                        errors.Add(activeTarget is not null
                            ? $"Student {entry.StudentId}: already promoted to the target academic year."
                            : $"Student {entry.StudentId}: enrollment in the source year is closed (already promoted or inactive).");
                    }
                    else
                    {
                        errors.Add($"Student {entry.StudentId}: no enrollment found in the source academic year.");
                    }

                    continue;
                }

                StudentAcademicEntity? existingTarget = await GetAcademicRecordAsync(
                    conn, schema, entry.StudentId, targetAcademicYearId, tx, activeOnly: false)
                    .ConfigureAwait(false);

                if (existingTarget is { IsActive: true })
                {
                    errors.Add($"Student {entry.StudentId}: already enrolled in target academic year.");
                    continue;
                }

                await conn.ExecuteAsync(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableStudentAcademics}
                    SET isactive = false,
                        updatedby = @UpdatedBy,
                        updatedon = @UpdatedOn,
                        versionno = versionno + 1
                    WHERE id = @Id;
                    """,
                    new { sourceRecord.Id, UpdatedBy = actorId, UpdatedOn = utcNow },
                    tx).ConfigureAwait(false);

                string rollNumber = entry.RollNumber?.Trim() ?? sourceRecord.RollNumber ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rollNumber))
                {
                    int nextRoll = await conn.QuerySingleAsync<int>(
                        $"""
                        SELECT COALESCE(MAX(CAST(NULLIF(rollnumber, '') AS INTEGER)), 0) + 1
                        FROM {schema}.{DatabaseConfig.TableStudentAcademics}
                        WHERE academicyearid = @TargetYearId AND classid = @ClassId AND isactive = true;
                        """,
                        new { TargetYearId = targetAcademicYearId, ClassId = entry.TargetClassId },
                        tx).ConfigureAwait(false);
                    rollNumber = nextRoll.ToString();
                }

                if (existingTarget is not null)
                {
                    await conn.ExecuteAsync(
                        $"""
                        UPDATE {schema}.{DatabaseConfig.TableStudentAcademics}
                        SET classid = @ClassId,
                            rollnumber = @RollNumber,
                            admissiondate = @AdmissionDate,
                            feestructureversionid = @FeeStructureVersionId,
                            isactive = true,
                            updatedby = @UpdatedBy,
                            updatedon = @UpdatedOn,
                            versionno = versionno + 1
                        WHERE id = @Id;
                        """,
                        new
                        {
                            existingTarget.Id,
                            ClassId = entry.TargetClassId,
                            RollNumber = rollNumber,
                            AdmissionDate = entry.AdmissionDate ?? sourceRecord.AdmissionDate ?? DateOnly.FromDateTime(utcNow),
                            FeeStructureVersionId = targetFeeVersion.Id,
                            UpdatedBy = actorId,
                            UpdatedOn = utcNow
                        },
                        tx).ConfigureAwait(false);
                }
                else
                {
                    var newRecord = new StudentAcademicEntity
                    {
                        Id = Guid.NewGuid(),
                        StudentId = entry.StudentId,
                        AcademicYearId = targetAcademicYearId,
                        ClassId = entry.TargetClassId,
                        AdmissionDate = entry.AdmissionDate ?? sourceRecord.AdmissionDate ?? DateOnly.FromDateTime(utcNow),
                        RollNumber = rollNumber,
                        FeeStructureVersionId = targetFeeVersion.Id
                    };
                    EnsureInsertAudit(newRecord, utcNow, actorId);
                    await InsertAsync(conn, schema, DatabaseConfig.TableStudentAcademics, newRecord, tx)
                        .ConfigureAwait(false);
                }

                promoted++;
                pendingFeeTransfers.Add((
                    entry.StudentId,
                    sourceRecord.ClassId,
                    sourceRecord.FeeStructureVersionId ?? Guid.Empty,
                    entry.TargetClassId));
            }
        }).ConfigureAwait(false);

        foreach ((Guid studentId, Guid sourceClassId, Guid sourceFeeVersionId, Guid targetClassId) in pendingFeeTransfers)
        {
            try
            {
                decimal transferred = await TransferPendingFeesToTargetYearAsync(
                    studentId,
                    sourceAcademicYearId,
                    sourceClassId,
                    sourceFeeVersionId,
                    targetClassId,
                    targetFeeVersion.Id,
                    targetAcademicYearId,
                    cancellationToken).ConfigureAwait(false);
                if (transferred > 0)
                {
                    feesTransferred++;
                    totalPendingTransferred += transferred;
                }
            }
            catch (Exception)
            {
                errors.Add(
                    $"Student {studentId}: promoted successfully, but pending fees could not be transferred to the target year.");
            }
        }

        return new PromoteStudentsResult(promoted, errors, feesTransferred, totalPendingTransferred);
    }

    private async Task<decimal> TransferPendingFeesToTargetYearAsync(
        Guid studentId,
        Guid sourceAcademicYearId,
        Guid sourceClassId,
        Guid sourceFeeStructureVersionId,
        Guid targetClassId,
        Guid targetFeeStructureVersionId,
        Guid targetAcademicYearId,
        CancellationToken cancellationToken)
    {
        if (sourceFeeStructureVersionId == Guid.Empty || targetFeeStructureVersionId == Guid.Empty)
        {
            return 0m;
        }

        decimal paid = await _feeCollectionRepo
            .GetStudentPaidTotalAsync(studentId, sourceFeeStructureVersionId, cancellationToken)
            .ConfigureAwait(false);
        FeeCollectionStudentRow? sourceRow = await _feeCollectionRepo
            .GetStudentRowAsync(studentId, sourceAcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        decimal total = sourceRow?.TotalFees ?? 0m;
        if (total <= 0 && sourceClassId != Guid.Empty)
        {
            total = await _feeCollectionRepo
                .GetStudentTotalFeesAsync(sourceClassId, sourceFeeStructureVersionId, cancellationToken)
                .ConfigureAwait(false);
        }

        decimal pending = Math.Max(0, total - paid);
        if (pending <= 0)
        {
            return 0m;
        }

        await _studentFeeInstallmentRepo
            .CopyFeeHeadAssignmentsFromVersionAsync(
                studentId,
                sourceFeeStructureVersionId,
                targetFeeStructureVersionId,
                cancellationToken)
            .ConfigureAwait(false);

        await _studentFeeInstallmentRepo
            .EnsureCurrentYearInstallmentsAsync(
                studentId,
                targetClassId,
                targetFeeStructureVersionId,
                targetAcademicYearId,
                cancellationToken)
            .ConfigureAwait(false);

        await _studentFeeInstallmentRepo
            .AddCarriedForwardBalanceAsync(
                studentId,
                targetClassId,
                targetFeeStructureVersionId,
                targetAcademicYearId,
                pending,
                cancellationToken)
            .ConfigureAwait(false);

        return pending;
    }

    #endregion

    #region Documents and Photo

    public async Task AddDocumentAsync(StudentDocumentEntity document, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (document.Id == Guid.Empty) document.Id = Guid.NewGuid();
        EnsureInsertAudit(document, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await InsertWithoutReturnAsync(
            connection,
            Context.OperationalSchema,
            DatabaseConfig.TableStudentDocuments,
            document,
            null).ConfigureAwait(false);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Context.OperationalSchema, DatabaseConfig.TableStudentDocuments, documentId, null).ConfigureAwait(false);
    }

    public async Task<StudentDocumentEntity?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentDocuments} WHERE id = @Id AND isactive = true";
        return await connection.QuerySingleOrDefaultAsync<StudentDocumentEntity>(sql, new { Id = documentId }).ConfigureAwait(false);
    }

    public async Task UpdatePhotoUrlAsync(Guid studentId, string photoUrl, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableStudents} SET photourl = @PhotoUrl, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1 WHERE id = @StudentId AND isactive = true";
        await connection.ExecuteAsync(sql, new { StudentId = studentId, PhotoUrl = photoUrl, Now = DateTime.UtcNow, Actor = ResolveUpdateActor() }).ConfigureAwait(false);
    }

    #endregion
}

