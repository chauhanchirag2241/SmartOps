using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
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
    private readonly IFeeStructureRepository _feeStructureRepo;
    private readonly IStudentFeeInstallmentRepository _studentFeeInstallmentRepo;

    private static readonly string[] RelatedTablesForSoftDelete =
    {
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentFeeConfigs,
        DatabaseConfig.TableStudentFeeHeadAssignments,
        DatabaseConfig.TableStudentCustomFields,
    };

    public StudentRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IFeeStructureRepository feeStructureRepo,
        IStudentFeeInstallmentRepository studentFeeInstallmentRepo)
        : base(context, currentUser)
    {
        _feeStructureRepo = feeStructureRepo;
        _studentFeeInstallmentRepo = studentFeeInstallmentRepo;
        _scope = scope;
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
    public async Task<StudentEntity?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = BuildStudentDetailSql();

        using var multi = await connection.QueryMultipleAsync(sql, new { Id = id }).ConfigureAwait(false);
        var student = await multi.ReadSingleOrDefaultAsync<StudentEntity>().ConfigureAwait(false);

        if (student is null)
        {
            return null;
        }

        student.Parents = (await multi.ReadAsync<StudentParentEntity>().ConfigureAwait(false)).ToList();
        student.Academics = (await multi.ReadAsync<StudentAcademicEntity>().ConfigureAwait(false)).ToList();
        student.FeeConfigs = (await multi.ReadAsync<StudentFeeConfigEntity>().ConfigureAwait(false)).ToList();
        student.FeeHeadAssignments = (await multi.ReadAsync<StudentFeeHeadAssignmentEntity>().ConfigureAwait(false)).ToList();
        student.PreviousSchools = (await multi.ReadAsync<StudentPreviousSchoolEntity>().ConfigureAwait(false)).ToList();
        student.CustomFields = (await multi.ReadAsync<StudentCustomFieldEntity>().ConfigureAwait(false)).ToList();

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
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

            Guid? effectiveClassId = ScopeSqlBuilder.ResolveClassIdFilter(_scope, classId);
            if (effectiveClassId == Guid.Empty)
            {
                return new PagedResult<StudentListModel>
                {
                    Items = [],
                    TotalCount = 0,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }

            var whereClause = BuildListWhereClause(filter, effectiveClassId, ref searchTerm);
            whereClause = ScopeSqlBuilder.AppendStudentScopeFilter(
                _scope, "s", Context.OperationalSchema, ref whereClause);
            var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

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
                s.isactive AS IsActive
            FROM {schema}.{students} s
            LEFT JOIN (
                SELECT studentid, classid, rollnumber, feestructureversionid, academicyearid,
                       ROW_NUMBER() OVER(PARTITION BY studentid ORDER BY createdon DESC) AS rn
                FROM {schema}.{academics}
                WHERE isactive = true
                  AND (@ScopeAcademicYearId IS NULL OR academicyearid = @ScopeAcademicYearId)
            ) a ON s.id = a.studentid AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c ON a.classid = c.id
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
                        ClassId = effectiveClassId,
                        ScopeStudentIds = _scope.AllowedStudentIds.ToArray(),
                        ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                        ScopeAcademicYearId = _scope.ActiveAcademicYearId
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

    #region List query helpers

    private string BuildListWhereClause(StudentFilter filter, Guid? classId, ref string? searchTerm)
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

        if (classId.HasValue)
        {
            where += $@"
                AND EXISTS (
                    SELECT 1
                    FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentAcademics} sa
                    WHERE sa.studentid = s.id
                      AND sa.classid = @ClassId
                      AND sa.isactive = true
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
            return $"a.class {direction}, a.section {direction}, s.id ASC";
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

    private string BuildStudentDetailSql()
    {
        var g = Context.OperationalSchema;
        return $@"
            SELECT * FROM {g}.{DatabaseConfig.TableStudents} WHERE id = @Id AND isactive = true;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentParents} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentAcademics} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentFeeConfigs} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentFeeHeadAssignments} WHERE studentid = @Id AND isactive = true;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentPreviousSchools} WHERE studentid = @Id;
            SELECT * FROM {g}.{DatabaseConfig.TableStudentCustomFields} WHERE studentid = @Id AND isactive = true ORDER BY createdon, fieldlabel;
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

        if (student.FeeConfigs is { Count: > 0 })
        {
            foreach (var feeConfig in student.FeeConfigs)
            {
                feeConfig.Id = Guid.NewGuid();
                feeConfig.StudentId = studentId;
                EnsureInsertAudit(feeConfig, utcNow);
                await InsertWithoutReturnAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentFeeConfigs,
                        feeConfig,
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
                ApplyUpdateAudit(academic, actorId, utcNow);
                await UpdateAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentAcademics,
                        academic,
                        transaction,
                        "StudentId")
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
                        "StudentId")
                    .ConfigureAwait(false);
            }
        }

        if (student.FeeConfigs is not null)
        {
            foreach (var fee in student.FeeConfigs)
            {
                fee.StudentId = student.Id;
                ApplyUpdateAudit(fee, actorId, utcNow);
                await UpdateAsync(
                        connection,
                        Context.OperationalSchema,
                        DatabaseConfig.TableStudentFeeConfigs,
                        fee,
                        transaction,
                        "StudentId")
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

    #endregion
}

