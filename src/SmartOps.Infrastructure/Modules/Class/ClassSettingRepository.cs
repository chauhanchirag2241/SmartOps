using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Class;
using SmartOps.Application.Modules.Class.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Class;

public sealed class ClassSettingRepository : BaseRepository, IClassSettingRepository
{
    public ClassSettingRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    private string Schema => Context.OperationalSchema;

    public async Task<ClassSettingEntity?> GetBySectionIdAsync(
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id AS Id, classgroupid AS ClassGroupId, sectionid AS SectionId, teacherid AS TeacherId,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassSettings}
WHERE sectionid = @SectionId AND isactive = true
LIMIT 1
""";
        return await connection.QuerySingleOrDefaultAsync<ClassSettingEntity>(
            new CommandDefinition(sql, new { SectionId = sectionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetClassTeacherEmployeeIdAsync(
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT teacherid
FROM {Schema}.{DatabaseConfig.TableClassSettings}
WHERE sectionid = @SectionId AND isactive = true AND teacherid IS NOT NULL
LIMIT 1
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { SectionId = sectionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetClassTeacherUserIdAsync(
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT e.userid
FROM {Schema}.{DatabaseConfig.TableClassSettings} s
INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = s.teacherid AND e.isactive = true
WHERE s.sectionid = @SectionId AND s.isactive = true AND s.teacherid IS NOT NULL
  AND e.userid IS NOT NULL
LIMIT 1
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { SectionId = sectionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetSectionIdsForTeacherAsync(
        Guid teacherEmployeeId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT sectionid
FROM {Schema}.{DatabaseConfig.TableClassSettings}
WHERE teacherid = @TeacherId AND isactive = true AND sectionid IS NOT NULL
""";
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { TeacherId = teacherEmployeeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ClassTeacherAssignmentDto>> GetAssignmentsForTeacherAsync(
        Guid teacherEmployeeId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT
    cs.id AS Id,
    cs.sectionid AS ClassId,
    {DashboardClassLabel.DisplayNameSql} AS ClassName,
    COALESCE(cs.classgroupid, c.classgroupid) AS ClassGroupId,
    cs.teacherid AS TeacherId
FROM {Schema}.{DatabaseConfig.TableClassSettings} cs
INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = cs.sectionid AND c.isactive = true
INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid AND cg.isactive = true
WHERE cs.teacherid = @TeacherId
  AND cs.isactive = true
  AND cs.sectionid IS NOT NULL
ORDER BY cg.classname ASC, c.section ASC
""";
        IEnumerable<ClassTeacherAssignmentDto> rows = await connection.QueryAsync<ClassTeacherAssignmentDto>(
            new CommandDefinition(sql, new { TeacherId = teacherEmployeeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task UpsertClassTeacherAsync(
        Guid sectionId,
        Guid? classGroupId,
        Guid? teacherId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actor = ResolveUpdateActor();

        ClassSettingEntity? existing = await GetBySectionIdAsync(sectionId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            if (teacherId is null)
            {
                return;
            }

            var entity = new ClassSettingEntity
            {
                Id = Guid.NewGuid(),
                ClassGroupId = classGroupId,
                SectionId = sectionId,
                TeacherId = teacherId
            };
            EnsureInsertAudit(entity, utcNow, actor);

            string insertSql = $"""
INSERT INTO {Schema}.{DatabaseConfig.TableClassSettings}
    (id, classgroupid, sectionid, teacherid,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @ClassGroupId, @SectionId, @TeacherId,
     true, 1, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
""";
            await connection.ExecuteAsync(
                new CommandDefinition(insertSql, entity, cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            return;
        }

        string updateSql = $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSettings}
SET classgroupid = COALESCE(@ClassGroupId, classgroupid),
    teacherid = @TeacherId,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = versionno + 1
WHERE id = @Id AND isactive = true
""";
        await connection.ExecuteAsync(
            new CommandDefinition(
                updateSql,
                new
                {
                    existing.Id,
                    ClassGroupId = classGroupId,
                    TeacherId = teacherId,
                    Actor = actor,
                    Now = utcNow
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
