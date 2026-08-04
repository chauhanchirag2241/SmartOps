using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
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
    private readonly IBranchScopedWriteHelper _branchWrite;
    private readonly IUserProvisioningService _userProvisioning;

    private static readonly string[] RelatedTablesForSoftDelete =
    {
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentCustomFields,
    };

    public StudentRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite,
        IUserProvisioningService userProvisioning)
        : base(context, currentUser)
    {
        _branchWrite = branchWrite;
        _scope = scope;
        _branchContext = branchContext;
        _userProvisioning = userProvisioning;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateStudentAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        if (student.Id == Guid.Empty)
        {
            student.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(student, now);

        student.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(student.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            Guid userId = await _userProvisioning
                .ProvisionStudentUserAsync(student, schoolId, tx, cancellationToken)
                .ConfigureAwait(false);
            student.UserId = userId;
            student.PortalAccess = true;

            var id = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, student, tx)
                .ConfigureAwait(false);
            student.Id = id;

            await InsertChildCollectionsAsync(conn, tx, id, student, now).ConfigureAwait(false);

            return id;
        }).ConfigureAwait(false);
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
            var global = IdentitySchema;
            var students = DatabaseConfig.TableStudents;
            var academics = DatabaseConfig.TableStudentAcademics;
            var attendance = DatabaseConfig.TableAttendance;

            var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{students} s
            INNER JOIN {global}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            {whereClause};";

            var querySql = $@"
            SELECT
                s.id AS Id,
                a.classgroupid AS ClassGroupId,
                a.classid AS ClassId,
                TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) AS Name,
                COALESCE(u.email, 'N/A') AS Email,
                s.admissionno AS AdmNo,
                a.rollnumber AS RollNumber,
                CASE
                    WHEN cg.classname IS NOT NULL AND c.section IS NOT NULL THEN
                        cg.classname || ' — ' || c.section
                    WHEN cg.classname IS NOT NULL THEN
                        cg.classname
                    ELSE 'N/A'
                END AS Class,
                COALESCE(att_stats.attendance_pct, 0) AS Attendance,
                CAST(NULL AS text) AS Fees,
                s.isactive AS IsActive,
                COALESCE(a.isactive, false) AS EnrollmentIsActive
            FROM {schema}.{students} s
            INNER JOIN {global}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            {enrollmentJoin} (
                SELECT sa.studentid,
                       sa.classgroupid,
                       sa.classid,
                       sa.rollnumber,
                       sa.academicyearid,
                       sa.isactive,
                       ROW_NUMBER() OVER(
                           PARTITION BY sa.studentid
                           ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                FROM {schema}.{academics} sa
                WHERE {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
            ) a ON s.id = a.studentid AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c
                ON c.id = a.classid
            LEFT JOIN {schema}.{DatabaseConfig.TableClassGroups} cg
                ON cg.id = COALESCE(a.classgroupid, c.classgroupid)
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
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(student, actorId, now);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            // Client update payloads often omit BranchId / UserId (defaults to Empty) — keep stored FKs.
            // UserId is never reassigned via this endpoint (see SetStudentUserIdAsync).
            var existing = await conn.QuerySingleOrDefaultAsync<(Guid BranchId, Guid UserId)>(
                new CommandDefinition(
                    $@"SELECT branchid AS BranchId, userid AS UserId
                       FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudents}
                       WHERE id = @Id AND isactive = true",
                    new { student.Id },
                    transaction: tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (existing.BranchId == Guid.Empty || existing.UserId == Guid.Empty)
            {
                throw new InvalidOperationException("Student not found.");
            }

            student.UserId = existing.UserId;

            if (student.BranchId == Guid.Empty)
            {
                student.BranchId = existing.BranchId;
            }
            else
            {
                student.BranchId = await _branchWrite
                    .ResolveWriteBranchIdAsync(student.BranchId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, student, tx, "Id")
                .ConfigureAwait(false);

            await UpdateChildCollectionsAsync(conn, tx, student, actorId, now).ConfigureAwait(false);
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
    public async Task<Guid?> GetStudentIdByAdmissionNoAsync(
        string admissionNo,
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
SELECT id
FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudents}
WHERE lower(admissionno) = lower(@AdmissionNo)
  AND branchid = @BranchId
  AND isactive = true
LIMIT 1;
""";

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
                sql,
                new { AdmissionNo = admissionNo.Trim(), BranchId = branchId })
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
            Now = SchoolLocalTime.NowDateTime(),
            Actor = ResolveUpdateActor()
        }).ConfigureAwait(false);
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
            SELECT COALESCE(c.isactive, true)
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentAcademics} sa
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c ON sa.classid = c.id
            WHERE sa.studentid = @Id
            ORDER BY sa.createdon DESC
            LIMIT 1;
        """;
        var isClassActive = await connection.ExecuteScalarAsync<bool?>(sqlCheckClass, new { Id = id }).ConfigureAwait(false);
        
        if (isClassActive.HasValue && !isClassActive.Value)
        {
            throw new InvalidOperationException("Cannot recover student because the assigned class is inactive. Please recover the class first.");
        }

        var now = SchoolLocalTime.NowDateTime();
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
                // Fees module removed — no-op filter returns no rows.
                where += " AND 1 = 0";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (u.firstname ILIKE @SearchTerm OR u.lastname ILIKE @SearchTerm OR s.admissionno ILIKE @SearchTerm)";
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
            return $"u.firstname {direction}, u.lastname {direction}, s.id ASC";
        }

        if (IsSortKey(sortColumn, "admNo"))
        {
            return $"s.admissionno {direction}, s.id ASC";
        }

        if (IsSortKey(sortColumn, "class"))
        {
            return $"cg.classname {direction}, c.section {direction}, s.id ASC";
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

            student.Status = student.IsActive ? "Active" : "Inactive";
        }
    }

    #endregion

    #region Detail SQL

    private string BuildStudentDetailSql(bool includeInactive)
    {
        var g = Context.OperationalSchema;
        var global = IdentitySchema;
        var activeFilter = includeInactive ? string.Empty : " AND s.isactive = true";
        return $@"
            SELECT s.*, u.firstname AS FirstName, u.lastname AS LastName, u.email AS Email, u.mobile AS Mobile
            FROM {g}.{DatabaseConfig.TableStudents} s
            INNER JOIN {global}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            WHERE s.id = @Id{activeFilter};
            SELECT * FROM {g}.{DatabaseConfig.TableStudentParents} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentAcademics}
            WHERE studentid = @Id
            ORDER BY isactive DESC, createdon DESC;
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
        DateTime now)
    {
        if (student.Parents is { Count: > 0 })
        {
            foreach (var parent in student.Parents)
            {
                parent.Id = Guid.NewGuid();
                parent.StudentId = studentId;
                EnsureInsertAudit(parent, now);
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
                academic.Id = Guid.NewGuid();
                academic.StudentId = studentId;

                if (academic.ClassGroupId == Guid.Empty)
                {
                    throw new InvalidOperationException("Class group is required.");
                }

                bool groupValid = await connection.QuerySingleAsync<bool>(
                    $"""
                    SELECT EXISTS(
                        SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassGroups} cg
                        WHERE cg.id = @ClassGroupId AND cg.isactive = true);
                    """,
                    new { academic.ClassGroupId },
                    transaction).ConfigureAwait(false);
                if (!groupValid)
                {
                    throw new InvalidOperationException("Selected class group is not active.");
                }

                if (academic.ClassId is { } classId && classId != Guid.Empty)
                {
                    bool classValid = await connection.QuerySingleAsync<bool>(
                        $"""
                        SELECT EXISTS(
                            SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c
                            WHERE c.id = @ClassId
                              AND c.isactive = true
                              AND c.classgroupid = @ClassGroupId);
                        """,
                        new { ClassId = classId, academic.ClassGroupId },
                        transaction).ConfigureAwait(false);
                    if (!classValid)
                    {
                        throw new InvalidOperationException(
                            "Selected section is not active for the class group.");
                    }
                }
                else
                {
                    academic.ClassId = null;
                    academic.RollNumber = null;
                }

                EnsureInsertAudit(academic, now);
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
                EnsureInsertAudit(prevSchool, now);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentPreviousSchools,
                        prevSchool,
                        transaction)
                    .ConfigureAwait(false);
            }
        }

        await InsertCustomFieldsAsync(connection, transaction, studentId, student.CustomFields, now)
            .ConfigureAwait(false);
    }

    private async Task UpdateChildCollectionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        StudentEntity student,
        Guid actorId,
        DateTime now)
    {
        if (student.Parents is not null)
        {
            foreach (var parent in student.Parents)
            {
                parent.StudentId = student.Id;
                ApplyUpdateAudit(parent, actorId, now);
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
                }

                if (academic.ClassGroupId == Guid.Empty)
                {
                    throw new InvalidOperationException("Class group is required.");
                }

                bool groupValid = await connection.QuerySingleAsync<bool>(
                    $"""
                    SELECT EXISTS(
                        SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassGroups} cg
                        WHERE cg.id = @ClassGroupId AND cg.isactive = true);
                    """,
                    new { academic.ClassGroupId },
                    transaction).ConfigureAwait(false);
                if (!groupValid)
                {
                    throw new InvalidOperationException(
                        "Selected class group is not active.");
                }

                if (academic.ClassId is { } classId && classId != Guid.Empty && academic.AcademicYearId != Guid.Empty)
                {
                    bool classValid = await connection.QuerySingleAsync<bool>(
                        $"""
                        SELECT EXISTS(
                            SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c
                            WHERE c.id = @ClassId
                              AND c.isactive = true
                              AND c.classgroupid = @ClassGroupId);
                        """,
                        new { ClassId = classId, academic.ClassGroupId },
                        transaction).ConfigureAwait(false);
                    if (!classValid)
                    {
                        throw new InvalidOperationException(
                            "Selected section is not active for the class group.");
                    }
                }
                else
                {
                    academic.ClassId = null;
                    academic.RollNumber = null;
                }

                ApplyUpdateAudit(academic, actorId, now);
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
                ApplyUpdateAudit(prev, actorId, now);
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
            await ReplaceCustomFieldsAsync(connection, transaction, student.Id, student.CustomFields, actorId, now)
                .ConfigureAwait(false);
        }
    }

    private async Task InsertCustomFieldsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid studentId,
        List<StudentCustomFieldEntity>? customFields,
        DateTime now)
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
            EnsureInsertAudit(field, now);
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
        DateTime now)
    {
        await SoftDeleteRelatedAsync(
                connection,
                Context.OperationalSchema,
                DatabaseConfig.TableStudentCustomFields,
                "StudentId",
                studentId,
                transaction)
            .ConfigureAwait(false);

        await InsertCustomFieldsAsync(connection, transaction, studentId, customFields, now)
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
                   academicyearid AS AcademicYearId, classgroupid AS ClassGroupId, classid AS ClassId,
                   rollnumber AS RollNumber,
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
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses} c
                WHERE c.id = @ClassId AND c.isactive = true);
            """,
            new { ClassId = targetClassId }).ConfigureAwait(false);

        if (!classValid)
        {
            return "Selected target class is not active.";
        }

        return null;
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
            string? validationError = await GetPromoteTargetValidationErrorAsync(
                targetAcademicYearId, targetClassId, cancellationToken).ConfigureAwait(false);
            if (validationError is not null)
            {
                errors.Add(validationError);
            }
        }

        if (errors.Count > 0)
        {
            return new PromoteStudentsResult(0, errors);
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveInsertActor();
        int promoted = 0;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (var entry in students)
            {
                bool classValid = await conn.QuerySingleAsync<bool>(
                    $"""
                    SELECT EXISTS(
                        SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses} c
                        WHERE c.id = @ClassId AND c.isactive = true);
                    """,
                    new { ClassId = entry.TargetClassId },
                    tx).ConfigureAwait(false);

                if (!classValid)
                {
                    errors.Add($"Student {entry.StudentId}: target class is not active.");
                    continue;
                }

                Guid targetClassGroupId = await conn.QuerySingleAsync<Guid>(
                    $"""
                    SELECT c.classgroupid
                    FROM {schema}.{DatabaseConfig.TableClasses} c
                    WHERE c.id = @ClassId;
                    """,
                    new { ClassId = entry.TargetClassId },
                    tx).ConfigureAwait(false);

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
                    new { sourceRecord.Id, UpdatedBy = actorId, UpdatedOn = now },
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
                        SET classgroupid = @ClassGroupId,
                            classid = @ClassId,
                            rollnumber = @RollNumber,
                            admissiondate = @AdmissionDate,
                            isactive = true,
                            updatedby = @UpdatedBy,
                            updatedon = @UpdatedOn,
                            versionno = versionno + 1
                        WHERE id = @Id;
                        """,
                        new
                        {
                            existingTarget.Id,
                            ClassGroupId = targetClassGroupId,
                            ClassId = entry.TargetClassId,
                            RollNumber = rollNumber,
                            AdmissionDate = entry.AdmissionDate ?? sourceRecord.AdmissionDate ?? DateOnly.FromDateTime(now),
                            UpdatedBy = actorId,
                            UpdatedOn = now
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
                        ClassGroupId = targetClassGroupId,
                        ClassId = entry.TargetClassId,
                        AdmissionDate = entry.AdmissionDate ?? sourceRecord.AdmissionDate ?? DateOnly.FromDateTime(now),
                        RollNumber = rollNumber
                    };
                    EnsureInsertAudit(newRecord, now, actorId);
                    await InsertAsync(conn, schema, DatabaseConfig.TableStudentAcademics, newRecord, tx)
                        .ConfigureAwait(false);
                }

                promoted++;
            }
        }).ConfigureAwait(false);

        return new PromoteStudentsResult(promoted, errors, StudentsWithFeesTransferred: 0, TotalPendingTransferred: 0);
    }

    /// <inheritdoc />
    public async Task<UpdateRollNumbersResult> UpdateRollNumbersAsync(
        Guid academicYearId,
        Guid classId,
        IReadOnlyList<UpdateRollNumberEntry> students,
        CancellationToken cancellationToken = default)
    {
        if (academicYearId == Guid.Empty || classId == Guid.Empty)
        {
            return new UpdateRollNumbersResult(0, ["Academic year and class are required."]);
        }

        if (students.Count == 0)
        {
            return new UpdateRollNumbersResult(0, ["At least one student is required."]);
        }

        var errors = new List<string>();
        var seenRolls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in students)
        {
            string? roll = entry.RollNumber?.Trim();
            if (string.IsNullOrWhiteSpace(roll))
            {
                continue;
            }

            if (!seenRolls.Add(roll))
            {
                errors.Add($"Duplicate roll number '{roll}' in the request.");
            }
        }

        if (errors.Count > 0)
        {
            return new UpdateRollNumbersResult(0, errors);
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        int updated = 0;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            bool classValid = await conn.QuerySingleAsync<bool>(
                $"""
                SELECT EXISTS(
                    SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses} c
                    WHERE c.id = @ClassId AND c.isactive = true);
                """,
                new { ClassId = classId },
                tx).ConfigureAwait(false);

            if (!classValid)
            {
                errors.Add("Target class was not found or is inactive.");
                return;
            }

            foreach (var entry in students)
            {
                int affected = await conn.ExecuteAsync(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableStudentAcademics}
                    SET rollnumber = @RollNumber,
                        updatedby = @UpdatedBy,
                        updatedon = @UpdatedOn,
                        versionno = versionno + 1
                    WHERE studentid = @StudentId
                      AND academicyearid = @AcademicYearId
                      AND classid = @ClassId
                      AND isactive = true;
                    """,
                    new
                    {
                        StudentId = entry.StudentId,
                        AcademicYearId = academicYearId,
                        ClassId = classId,
                        RollNumber = string.IsNullOrWhiteSpace(entry.RollNumber) ? null : entry.RollNumber.Trim(),
                        UpdatedBy = actorId,
                        UpdatedOn = now
                    },
                    tx).ConfigureAwait(false);

                if (affected == 0)
                {
                    errors.Add($"Student {entry.StudentId}: no active enrollment in this class/year.");
                }
                else
                {
                    updated++;
                }
            }
        }).ConfigureAwait(false);

        return new UpdateRollNumbersResult(updated, errors);
    }

    #endregion

    #region Documents and Photo

    public async Task AddDocumentAsync(StudentDocumentEntity document, CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        if (document.Id == Guid.Empty) document.Id = Guid.NewGuid();
        EnsureInsertAudit(document, now);

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
        await connection.ExecuteAsync(sql, new { StudentId = studentId, PhotoUrl = photoUrl, Now = SchoolLocalTime.NowDateTime(), Actor = ResolveUpdateActor() }).ConfigureAwait(false);
    }

    #endregion
}
