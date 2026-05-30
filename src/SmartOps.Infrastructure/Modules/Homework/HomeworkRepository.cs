using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Homework.Interfaces;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Homework;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Homework;

public sealed class HomeworkRepository : BaseRepository, IHomeworkRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IUserScopeContext _scope;

    public HomeworkRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema,
        IUserScopeContext scope)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
        _scope = scope;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    /// <summary>Display label for classes (matches class dropdown).</summary>
    private const string ClassDisplayNameSql =
        "c.classname || CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    public async Task<Guid> CreateAsync(HomeworkEntity homework, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = GetActorId();

        homework.Id = homework.Id == Guid.Empty ? Guid.NewGuid() : homework.Id;
        EnsureInsertAudit(homework, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableHomework}
                (id, classid, subjectid, teacherid, title, description,
                 assigndate, duedate, priority, marks, submissiontype,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ClassId, @SubjectId, @TeacherId, @Title, @Description,
                 @AssignDate, @DueDate, @Priority, @Marks, @SubmissionType,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, homework, cancellationToken: ct))
            .ConfigureAwait(false);

        return homework.Id;
    }

    public async Task UpdateAsync(HomeworkEntity homework, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(homework, GetActorId(), DateTime.UtcNow);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableHomework}
            SET classid = @ClassId,
                subjectid = @SubjectId,
                title = @Title,
                description = @Description,
                assigndate = @AssignDate,
                duedate = @DueDate,
                priority = @Priority,
                marks = @Marks,
                submissiontype = @SubmissionType,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, homework, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = GetActorId();

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableHomework}
            SET isactive = false, updatedby = @ActorId, updatedon = @UtcNow, versionno = versionno + 1
            WHERE id = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableHomeworkDetails}
            SET isactive = false, updatedby = @ActorId, updatedon = @UtcNow, versionno = versionno + 1
            WHERE homeworkid = @Id;
            """;

        await connection.ExecuteAsync(
                new CommandDefinition(sql, new { Id = id, ActorId = actorId, UtcNow = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<HomeworkEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT h.id, h.classid, h.subjectid, h.teacherid, h.title, h.description,
                   h.assigndate, h.duedate, h.priority, h.marks, h.submissiontype,
                   h.isactive, h.versionno, h.createdby, h.createdon, h.updatedby, h.updatedon
            FROM {Schema}.{DatabaseConfig.TableHomework} h
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid
            WHERE h.id = @Id AND h.isactive = true
              {HomeworkAcademicYearSql.FilterOnClass("c")};
            """;

        return await connection.QuerySingleOrDefaultAsync<HomeworkEntity>(
                new CommandDefinition(
                    sql,
                    new { Id = id, ScopeAcademicYearId = _scope.ActiveAcademicYearId },
                    cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<HomeworkListRow>> GetListAsync(
        Guid? classId,
        Guid? subjectId,
        string? statusFilter,
        string? searchTerm,
        CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        var where = new StringBuilder($"h.isactive = true{HomeworkAcademicYearSql.FilterOnClass("c")}");
        var parameters = new DynamicParameters();
        parameters.Add("ScopeAcademicYearId", _scope.ActiveAcademicYearId);

        if (classId.HasValue && classId.Value != Guid.Empty)
        {
            where.Append(" AND h.classid = @ClassId");
            parameters.Add("ClassId", classId.Value);
        }

        if (subjectId.HasValue && subjectId.Value != Guid.Empty)
        {
            where.Append(" AND h.subjectid = @SubjectId");
            parameters.Add("SubjectId", subjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where.Append(" AND (h.title ILIKE @SearchTerm OR COALESCE(h.description, '') ILIKE @SearchTerm)");
            parameters.Add("SearchTerm", $"%{searchTerm.Trim()}%");
        }

        string sql = $"""
            SELECT h.id AS Id,
                   h.title AS Title,
                   h.description AS Description,
                   h.classid AS ClassId,
                   COALESCE({ClassDisplayNameSql}, '') AS ClassName,
                   h.subjectid AS SubjectId,
                   COALESCE(s.subjectname, '') AS SubjectName,
                   h.assigndate AS AssignDate,
                   h.duedate AS DueDate,
                   h.priority::int AS Priority,
                   h.submissiontype::int AS SubmissionType,
                   h.marks AS Marks,
                   COALESCE(SUM(CASE WHEN d.status = 1 THEN 1 ELSE 0 END), 0)::int AS Submitted,
                   COALESCE(SUM(CASE WHEN d.status = 0 THEN 1 ELSE 0 END), 0)::int AS Pending,
                   COALESCE(SUM(CASE WHEN d.status = 2 THEN 1 ELSE 0 END), 0)::int AS Late,
                   COALESCE(COUNT(d.id), 0)::int AS Total
            FROM {Schema}.{DatabaseConfig.TableHomework} h
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid AND c.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = h.subjectid AND s.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableHomeworkDetails} d
                ON d.homeworkid = h.id AND d.isactive = true
            WHERE {where}
            GROUP BY h.id, h.title, h.description, h.classid, c.classname, c.section, h.subjectid, s.subjectname,
                     h.assigndate, h.duedate, h.priority, h.marks, h.submissiontype, h.createdon
            ORDER BY h.duedate DESC, h.createdon DESC;
            """;

        IEnumerable<HomeworkListRow> rows = await connection.QueryAsync<HomeworkListRow>(
                new CommandDefinition(sql, parameters, cancellationToken: ct))
            .ConfigureAwait(false);

        IList<HomeworkListRow> list = rows.ToList();

        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "all")
        {
            return list;
        }

        return list
            .Where(r => MatchesStatusFilter(r, statusFilter, today))
            .ToList();
    }

    public async Task<HomeworkStatsRow> GetStatsAsync(CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        string yearFilter = HomeworkAcademicYearSql.FilterOnClass("c");

        string sql = $"""
            SELECT
                (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableHomework} h
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid
                    WHERE h.isactive = true{yearFilter}) AS TotalAssigned,
                (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableHomework} h
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid
                    WHERE h.isactive = true AND h.duedate = @Today{yearFilter}) AS DueToday,
                (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableHomeworkDetails} d
                    INNER JOIN {Schema}.{DatabaseConfig.TableHomework} h ON h.id = d.homeworkid AND h.isactive = true
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid
                    WHERE d.isactive = true AND d.status IN (1, 2){yearFilter}) AS TotalSubmissions,
                (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableHomework} h
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid
                    WHERE h.isactive = true AND h.duedate < @Today{yearFilter}
                      AND EXISTS (
                          SELECT 1 FROM {Schema}.{DatabaseConfig.TableHomeworkDetails} d
                          WHERE d.homeworkid = h.id AND d.isactive = true AND d.status = 0
                      )) AS Overdue;
            """;

        HomeworkStatsRow? row = await connection.QuerySingleOrDefaultAsync<HomeworkStatsRow>(
                new CommandDefinition(
                    sql,
                    new { Today = today, ScopeAcademicYearId = _scope.ActiveAcademicYearId },
                    cancellationToken: ct))
            .ConfigureAwait(false);

        return row ?? new HomeworkStatsRow();
    }

    public async Task<IList<HomeworkDetailEntity>> GetDetailsByHomeworkIdAsync(Guid homeworkId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, homeworkid, classid, subjectid, studentid, status,
                   submittedon, marks, remark,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableHomeworkDetails}
            WHERE homeworkid = @HomeworkId AND isactive = true
            ORDER BY studentid;
            """;

        IEnumerable<HomeworkDetailEntity> rows = await connection.QueryAsync<HomeworkDetailEntity>(
                new CommandDefinition(sql, new { HomeworkId = homeworkId }, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<bool> HasSubmissionsAsync(Guid homeworkId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableHomeworkDetails}
                WHERE homeworkid = @HomeworkId AND isactive = true
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { HomeworkId = homeworkId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task BulkInsertDetailsAsync(IList<HomeworkDetailEntity> details, CancellationToken ct = default)
    {
        if (details.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = GetActorId();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (HomeworkDetailEntity detail in details)
            {
                detail.Id = detail.Id == Guid.Empty ? Guid.NewGuid() : detail.Id;
                EnsureInsertAudit(detail, utcNow, actorId);

                string sql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableHomeworkDetails}
                        (id, homeworkid, classid, subjectid, studentid, status,
                         submittedon, marks, remark,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @HomeworkId, @ClassId, @SubjectId, @StudentId, @Status,
                         @SubmittedOn, @Marks, @Remark,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """;

                await conn.ExecuteAsync(
                        new CommandDefinition(sql, detail, tx, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task BulkUpsertDetailsAsync(IList<HomeworkDetailEntity> details, CancellationToken ct = default)
    {
        if (details.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = GetActorId();
        Guid homeworkId = details[0].HomeworkId;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            Dictionary<Guid, Guid> existingByStudent = await GetExistingDetailIdsAsync(
                conn, tx, homeworkId, ct).ConfigureAwait(false);

            foreach (HomeworkDetailEntity detail in details)
            {
                if (existingByStudent.TryGetValue(detail.StudentId, out Guid existingId))
                {
                    detail.Id = existingId;
                    ApplyUpdateAudit(detail, actorId, utcNow);

                    string updateSql = $"""
                        UPDATE {Schema}.{DatabaseConfig.TableHomeworkDetails}
                        SET status = @Status,
                            submittedon = @SubmittedOn,
                            marks = @Marks,
                            remark = @Remark,
                            updatedby = @UpdatedBy,
                            updatedon = @UpdatedOn,
                            versionno = versionno + 1
                        WHERE id = @Id;
                        """;

                    await conn.ExecuteAsync(
                            new CommandDefinition(updateSql, detail, tx, cancellationToken: ct))
                        .ConfigureAwait(false);
                }
                else
                {
                    detail.Id = Guid.NewGuid();
                    EnsureInsertAudit(detail, utcNow, actorId);

                    string insertSql = $"""
                        INSERT INTO {Schema}.{DatabaseConfig.TableHomeworkDetails}
                            (id, homeworkid, classid, subjectid, studentid, status,
                             submittedon, marks, remark,
                             isactive, versionno, createdby, createdon, updatedby, updatedon)
                        VALUES
                            (@Id, @HomeworkId, @ClassId, @SubjectId, @StudentId, @Status,
                             @SubmittedOn, @Marks, @Remark,
                             @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                        """;

                    await conn.ExecuteAsync(
                            new CommandDefinition(insertSql, detail, tx, cancellationToken: ct))
                        .ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task<HomeworkMetaRow?> GetMetaByHomeworkIdAsync(Guid homeworkId, CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT
                COALESCE({ClassDisplayNameSql}, '') AS ClassName,
                COALESCE(s.subjectname, '') AS SubjectName
            FROM {Schema}.{DatabaseConfig.TableHomework} h
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid AND c.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = h.subjectid AND s.isactive = true
            WHERE h.id = @HomeworkId AND h.isactive = true
              {HomeworkAcademicYearSql.FilterOnClass("c")};
            """;

        return await connection.QuerySingleOrDefaultAsync<HomeworkMetaRow>(
                new CommandDefinition(
                    sql,
                    new { HomeworkId = homeworkId, ScopeAcademicYearId = _scope.ActiveAcademicYearId },
                    cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<HomeworkStudentRow>> GetClassStudentsForHomeworkAsync(Guid classId, CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT st.id AS StudentId,
                   TRIM(COALESCE(st.firstname, '') || ' ' || COALESCE(st.lastname, '')) AS StudentName,
                   COALESCE(sa.rollnumber, '') AS RollNo
            FROM {Schema}.{DatabaseConfig.TableStudents} st
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = @ClassId AND cl.isactive = true
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                ON sa.studentid = st.id
               AND sa.classid = @ClassId
               AND sa.academicyearid = cl.academicyearid
               AND sa.isactive = true
            WHERE st.isactive = true
            ORDER BY sa.rollnumber NULLS LAST, st.firstname, st.lastname;
            """;

        IEnumerable<HomeworkStudentRow> rows = await connection.QueryAsync<HomeworkStudentRow>(
                new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    private async Task<Dictionary<Guid, Guid>> GetExistingDetailIdsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid homeworkId,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT studentid, id
            FROM {Schema}.{DatabaseConfig.TableHomeworkDetails}
            WHERE homeworkid = @HomeworkId AND isactive = true;
            """;

        IEnumerable<(Guid StudentId, Guid Id)> rows = await connection.QueryAsync<(Guid StudentId, Guid Id)>(
                new CommandDefinition(sql, new { HomeworkId = homeworkId }, transaction, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.StudentId, r => r.Id);
    }

    private static bool MatchesStatusFilter(HomeworkListRow row, string statusFilter, DateOnly today)
    {
        string status = ComputeHomeworkStatus(row.DueDate, row.Submitted, row.Pending, row.Late, row.Total);
        return statusFilter.ToLowerInvariant() switch
        {
            "active" => status == "active",
            "today" => status == "today",
            "overdue" => status == "overdue",
            "done" => status == "done",
            _ => true
        };
    }

    internal static string ComputeHomeworkStatus(
        DateOnly dueDate,
        int submitted,
        int pending,
        int late,
        int total)
    {
        if (total > 0 && pending == 0)
        {
            return "done";
        }

        if (dueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return "overdue";
        }

        if (dueDate == DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return "today";
        }

        return "active";
    }

    private Guid GetActorId() =>
        CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty
            ? CurrentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);
}
