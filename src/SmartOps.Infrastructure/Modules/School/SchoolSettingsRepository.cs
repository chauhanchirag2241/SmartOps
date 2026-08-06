using System.Data;
using Dapper;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.School;

/// <summary>
/// School settings SoT is <c>man.schoolsettings</c> on each school database (not platform <c>global</c>).
/// </summary>
public sealed class SchoolSettingsRepository : BaseRepository, ISchoolSettingsRepository
{
    private readonly ISchoolDbConnectionFactory _schoolDb;

    public SchoolSettingsRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ISchoolDbConnectionFactory schoolDb)
        : base(context, currentUser)
    {
        _schoolDb = schoolDb;
    }

    public async Task<IReadOnlyList<SchoolSettingRow>> GetByPrefixAsync(
        Guid schoolId,
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = (NpgsqlConnection)await _schoolDb
            .OpenBySchoolIdAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        string sql = $"""
SELECT settingkey AS Key, settingvalue AS Value
FROM {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolSettings}
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

        await using NpgsqlConnection connection = (NpgsqlConnection)await _schoolDb
            .OpenBySchoolIdAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        Guid actor = ResolveUpdateActor();
        DateTime now = SchoolLocalTime.NowDateTime();

        foreach (SchoolSettingUpsert setting in settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                continue;
            }

            string updateSql = $"""
UPDATE {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolSettings}
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
                        Now = now,
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (rows > 0)
            {
                continue;
            }

            string insertSql = $"""
INSERT INTO {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolSettings}
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
                        Now = now,
                    },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public Task SeedLeaveDefaultsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        SchoolSettingUpsert[] defaults =
        [
            new() { Key = LeaveSettingKeys.StaffApprovalMode, Value = LeaveApprovalModes.AnyOne },
            new() { Key = LeaveSettingKeys.StaffApproverUserTypes, Value = UserTypeCodes.Principal },
            new() { Key = LeaveSettingKeys.StudentApprovalMode, Value = LeaveApprovalModes.AnyOne },
            new() { Key = LeaveSettingKeys.StudentDefaultApprover, Value = LeaveApproverTokens.ClassTeacher },
            new() { Key = LeaveSettingKeys.StudentLongLeaveMinDays, Value = "4" },
            new() { Key = LeaveSettingKeys.StudentLongLeaveApproverUserTypes, Value = UserTypeCodes.OfficeStaff },
            new() { Key = LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, Value = "true" },
            new() { Key = EmployeeAttendanceSettingKeys.EmployeeType, Value = EmployeeAttendanceTypes.Both },
            new()
            {
                Key = EmployeeAttendanceSettingKeys.DefaultWorkingHours,
                Value = EmployeeAttendanceSettingKeys.DefaultWorkingHoursValue,
            },
        ];

        return UpsertAsync(schoolId, defaults, cancellationToken);
    }
}
