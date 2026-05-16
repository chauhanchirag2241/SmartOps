using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Domain.Modules.Student.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Student aggregate persistence. Pattern: connection → <see cref="BaseRepository"/> transaction helpers for writes;
/// list query split into filter / order / SQL builders as a template for other modules.
/// </summary>
public sealed class StudentRepository : BaseRepository, IStudentRepository
{
    private static readonly string[] RelatedTablesForSoftDelete =
    {
        DatabaseConfig.TableStudentParents,
        DatabaseConfig.TableStudentAcademics,
        DatabaseConfig.TableStudentPreviousSchools,
        DatabaseConfig.TableStudentFeeConfigs,
    };

    public StudentRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
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

            return await WithTransactionAsync(connection, async (conn, tx) =>
            {
                var studentId = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableStudents, student, tx)
                    .ConfigureAwait(false);
                student.Id = studentId;

                await InsertChildCollectionsAsync(conn, tx, studentId, student, utcNow).ConfigureAwait(false);

                return studentId;
            }).ConfigureAwait(false);
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
        student.PreviousSchools = (await multi.ReadAsync<StudentPreviousSchoolEntity>().ConfigureAwait(false)).ToList();

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
            var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

            var whereClause = BuildListWhereClause(filter, classId, ref searchTerm);
            var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

            var schema = Context.OperationalSchema;
            var students = DatabaseConfig.TableStudents;
            var academics = DatabaseConfig.TableStudentAcademics;

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
                CASE
                    WHEN c.classname IS NOT NULL THEN 
                        c.classname || ' — ' || 
                        CASE c.section 
                            WHEN 1 THEN 'A' WHEN 2 THEN 'B' WHEN 3 THEN 'C' WHEN 4 THEN 'D' ELSE 'N/A' 
                        END
                    ELSE 'N/A'
                END AS Class,
                s.isactive AS IsActive
            FROM {schema}.{students} s
            LEFT JOIN (
                SELECT studentid, classid,
                       ROW_NUMBER() OVER(PARTITION BY studentid ORDER BY createdon DESC) AS rn
                FROM {schema}.{academics}
                WHERE isactive = true
            ) a ON s.id = a.studentid AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c ON a.classid = c.id
            {whereClause}
            ORDER BY {orderBy}";

            var result = await GetPagedResultAsync<StudentListModel>(
                    connection,
                    querySql,
                    countSql,
                    new { SearchTerm = searchTerm, ClassId = classId },
                    pageIndex,
                    pageSize)
                .ConfigureAwait(false);

            var items = result.Items.ToList();
            ApplyListDemoProjection(items);
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
                var feeTable = $"{Context.OperationalSchema}.{DatabaseConfig.TableStudentFeeConfigs}";
                where += $" AND s.id IN (SELECT studentid FROM {feeTable} WHERE status = 'Overdue' AND isactive = true)";
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

    /// <summary>
    /// TEMP: attendance / fees / status until sourced from DB joins. Remove when real columns exist.
    /// </summary>
    private static void ApplyListDemoProjection(IList<StudentListModel> items)
    {
        var random = new Random();
        foreach (var student in items)
        {
            student.Attendance = random.Next(80, 100);
            student.Fees = "Paid";

            if (string.IsNullOrEmpty(student.Status))
            {
                student.Status = "Active";
            }

            if (string.IsNullOrEmpty(student.AdmNo))
            {
                student.AdmNo = "N/A";
            }
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
            SELECT * FROM {g}.{DatabaseConfig.TableStudentPreviousSchools} WHERE studentid = @Id;
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

