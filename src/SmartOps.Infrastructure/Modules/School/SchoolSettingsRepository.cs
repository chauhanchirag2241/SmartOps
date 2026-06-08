using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.School;

public sealed class SchoolSettingsRepository : BaseRepository, ISchoolSettingsRepository
{
    public SchoolSettingsRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    private static string G => DatabaseConfig.Schema_Global;

    public async Task<IReadOnlyList<SchoolSettingRow>> GetByPrefixAsync(
        Guid schoolId,
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT settingkey AS Key, settingvalue AS Value
FROM {G}.{DatabaseConfig.TableSchoolSettings}
WHERE schoolid = @SchoolId AND isactive = true AND settingkey LIKE @Prefix
ORDER BY settingkey;
""";
        IEnumerable<SchoolSettingRow> rows = await connection.QueryAsync<SchoolSettingRow>(
            new CommandDefinition(
                sql,
                new { SchoolId = schoolId, Prefix = $"{keyPrefix}%" },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task UpsertAsync(
        Guid schoolId,
        IReadOnlyList<SchoolSettingUpsert> settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid actor = ResolveUpdateActor();
        DateTime utcNow = DateTime.UtcNow;

        foreach (SchoolSettingUpsert setting in settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                continue;
            }

            string updateSql = $"""
UPDATE {G}.{DatabaseConfig.TableSchoolSettings}
SET settingvalue = @Value, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
WHERE schoolid = @SchoolId AND settingkey = @Key AND isactive = true;
""";
            int rows = await connection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        SchoolId = schoolId,
                        Key = setting.Key.Trim(),
                        Value = setting.Value ?? string.Empty,
                        Actor = actor,
                        Now = utcNow,
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (rows > 0)
            {
                continue;
            }

            string insertSql = $"""
INSERT INTO {G}.{DatabaseConfig.TableSchoolSettings}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @SchoolId, @Key, @Value, true, 1, @Actor, @Now, @Actor, @Now);
""";
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = schoolId,
                        Key = setting.Key.Trim(),
                        Value = setting.Value ?? string.Empty,
                        Actor = actor,
                        Now = utcNow,
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public Task SeedLeaveDefaultsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        SchoolSettingUpsert[] defaults =
        [
            new() { Key = LeaveSettingKeys.StaffApprovalMode, Value = LeaveApprovalModes.AnyOne },
            new() { Key = LeaveSettingKeys.StaffApproverUserTypes, Value = UserTypeCodes.SchoolAdmin },
            new() { Key = LeaveSettingKeys.StudentApprovalMode, Value = LeaveApprovalModes.AnyOne },
            new() { Key = LeaveSettingKeys.StudentDefaultApprover, Value = LeaveApproverTokens.ClassTeacher },
            new() { Key = LeaveSettingKeys.StudentLongLeaveMinDays, Value = "4" },
            new() { Key = LeaveSettingKeys.StudentLongLeaveApproverUserTypes, Value = UserTypeCodes.Principal },
            new() { Key = LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, Value = "true" },
        ];

        return UpsertAsync(schoolId, defaults, cancellationToken);
    }
}
