using Dapper;

using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Domain.Modules.Student.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;

using System.Data;

namespace SmartOps.Infrastructure.Persistence.Repositories;

public class StudentRepository : BaseRepository, IStudentRepository
{
    public StudentRepository(DapperContext context, ICurrentUserService currentUser) : base(context, currentUser)
    {
    }

    public async Task<Guid> CreateStudentAsync(StudentEntity student)
    {
        var utcNow = DateTime.UtcNow;
        if (student.Id == Guid.Empty) student.Id = Guid.NewGuid();
        EnsureInsertAudit(student, utcNow);

        var connection = await Context.GetGlobalConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var studentId = await InsertAsync<StudentEntity>(
                connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, student, transaction);
            student.Id = studentId;

            if (student.Parents != null && student.Parents.Any())
            {
                foreach (var parent in student.Parents)
                {
                    parent.Id = Guid.NewGuid();
                    parent.StudentId = studentId;
                    EnsureInsertAudit(parent, utcNow);
                    await InsertWithoutReturnAsync<StudentParentEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentParents, parent, transaction);
                }
            }

            if (student.Academics != null && student.Academics.Any())
            {
                foreach (var academic in student.Academics)
                {
                    academic.Id = Guid.NewGuid();
                    academic.StudentId = studentId;
                    EnsureInsertAudit(academic, utcNow);
                    await InsertWithoutReturnAsync<StudentAcademicEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentAcademics, academic, transaction);
                }
            }

            if (student.PreviousSchools != null && student.PreviousSchools.Any())
            {
                foreach (var prevSchool in student.PreviousSchools)
                {
                    prevSchool.Id = Guid.NewGuid();
                    prevSchool.StudentId = studentId;
                    EnsureInsertAudit(prevSchool, utcNow);
                    await InsertWithoutReturnAsync<StudentPreviousSchoolEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentPreviousSchools, prevSchool, transaction);
                }
            }

            if (student.FeeConfigs != null && student.FeeConfigs.Any())
            {
                foreach (var feeConfig in student.FeeConfigs)
                {
                    feeConfig.Id = Guid.NewGuid();
                    feeConfig.StudentId = studentId;
                    EnsureInsertAudit(feeConfig, utcNow);
                    await InsertWithoutReturnAsync<StudentFeeConfigEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentFeeConfigs, feeConfig, transaction);
                }
            }

            transaction.Commit();
            return studentId;
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<StudentEntity?> GetStudentByIdAsync(Guid id)
    {
        try
        {
            var connection = await Context.GetGlobalConnectionAsync();

            string sql = $@"
            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudents} WHERE id = @Id AND isactive = true;
            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentParents} WHERE studentid = @Id;
            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentAcademics} WHERE studentid = @Id;
            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentFeeConfigs} WHERE studentid = @Id;
            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentPreviousSchools} WHERE studentid = @Id;
        ";

            using var multi = await connection.QueryMultipleAsync(sql, new { Id = id });
            var student = await multi.ReadSingleOrDefaultAsync<StudentEntity>();

            if (student != null)
            {
                student.Parents = (await multi.ReadAsync<StudentParentEntity>()).ToList();
                student.Academics = (await multi.ReadAsync<StudentAcademicEntity>()).ToList();
                student.FeeConfigs = (await multi.ReadAsync<StudentFeeConfigEntity>()).ToList();
                student.PreviousSchools = (await multi.ReadAsync<StudentPreviousSchoolEntity>()).ToList();
            }
            return student;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<PagedResult<StudentListModel>> GetAllStudentsAsync(int pageIndex, int pageSize, string? searchTerm = null, string? sortColumn = null, string? sortDirection = null)
    {
        try
        {
            var connection = await Context.GetGlobalConnectionAsync();

            string whereClause = "WHERE s.isactive = true";
            if (!string.IsNullOrEmpty(searchTerm))
            {
                whereClause += " AND (s.firstname ILIKE @SearchTerm OR s.lastname ILIKE @SearchTerm OR s.admissionno ILIKE @SearchTerm)";
                searchTerm = $"%{searchTerm}%";
            }

            string orderBy = "s.createdon DESC, s.id ASC";
            if (!string.IsNullOrEmpty(sortColumn))
            {
                string direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (string.Equals(sortColumn, "student", StringComparison.OrdinalIgnoreCase) || string.Equals(sortColumn, "name", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"s.firstname {direction}, s.lastname {direction}, s.id ASC";
                }
                else if (string.Equals(sortColumn, "admNo", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"s.admissionno {direction}, s.id ASC";
                }
                else if (string.Equals(sortColumn, "class", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"a.class {direction}, a.section {direction}, s.id ASC";
                }
                else if (string.Equals(sortColumn, "status", StringComparison.OrdinalIgnoreCase))
                {
                    orderBy = $"s.status {direction}, s.id ASC";
                }
            }

            string countSql = $@"
            SELECT COUNT(*) 
            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudents} s
            {whereClause};";

            string querySql = $@"
            SELECT 
                s.id AS Id,
                TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS Name,
                COALESCE(s.email, 'N/A') AS Email,
                s.admissionno AS AdmNo,
                CASE 
                    WHEN a.class IS NOT NULL AND a.section IS NOT NULL THEN a.class || ' — ' || a.section
                    WHEN a.class IS NOT NULL THEN a.class
                    ELSE 'N/A' 
                END AS Class,
                s.status AS Status
            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudents} s
            LEFT JOIN (
                SELECT studentid, class, section, 
                       ROW_NUMBER() OVER(PARTITION BY studentid ORDER BY createdon DESC) as rn
                FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableStudentAcademics}
                WHERE isactive = true
            ) a ON s.id = a.studentid AND a.rn = 1
            {whereClause}
            ORDER BY {orderBy}";

            var result = await GetPagedResultAsync<StudentListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm },
                pageIndex,
                pageSize);

            var random = new Random();

            var items = result.Items.ToList();
            foreach (var student in items)
            {
                student.Attendance = random.Next(80, 100);
                student.Fees = "Paid";

                if (string.IsNullOrEmpty(student.Status)) student.Status = "Active";
                if (string.IsNullOrEmpty(student.AdmNo)) student.AdmNo = "N/A";
            }

            result.Items = items;
            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateStudentAsync(StudentEntity student)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(student, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            await UpdateAsync<StudentEntity>(
                connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, student, transaction, "Id");

            if (student.Parents != null)
            {
                foreach (var parent in student.Parents)
                {
                    parent.StudentId = student.Id;
                    ApplyUpdateAudit(parent, actorId, utcNow);
                    await UpdateAsync<StudentParentEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentParents, parent, transaction, "StudentId", "RelationType");
                }
            }

            if (student.Academics != null)
            {
                foreach (var academic in student.Academics)
                {
                    academic.StudentId = student.Id;
                    ApplyUpdateAudit(academic, actorId, utcNow);
                    await UpdateAsync<StudentAcademicEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentAcademics, academic, transaction, "StudentId");
                }
            }

            if (student.PreviousSchools != null)
            {
                foreach (var prev in student.PreviousSchools)
                {
                    prev.StudentId = student.Id;
                    ApplyUpdateAudit(prev, actorId, utcNow);
                    await UpdateAsync<StudentPreviousSchoolEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentPreviousSchools, prev, transaction, "StudentId");
                }
            }

            if (student.FeeConfigs != null)
            {
                foreach (var fee in student.FeeConfigs)
                {
                    fee.StudentId = student.Id;
                    ApplyUpdateAudit(fee, actorId, utcNow);
                    await UpdateAsync<StudentFeeConfigEntity>(
                        connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudentFeeConfigs, fee, transaction, "StudentId");
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteStudentAsync(Guid id)
    {
        var connection = await Context.GetGlobalConnectionAsync();
        await SoftDeleteAsync(connection, DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, id);
    }
}
