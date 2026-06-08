using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveSettingsService : ILeaveSettingsService
{
    private readonly ISchoolSettingsRepository _settings;

    public LeaveSettingsService(ISchoolSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<StaffLeaveApprovalSettings> GetStaffSettingsAsync(Guid schoolId, CancellationToken ct = default)
    {
        IReadOnlyList<SchoolSettingRow> rows = await _settings
            .GetByPrefixAsync(schoolId, LeaveSettingKeys.Prefix, ct)
            .ConfigureAwait(false);

        Dictionary<string, string> map = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);

        return new StaffLeaveApprovalSettings
        {
            ApprovalMode = GetValue(map, LeaveSettingKeys.StaffApprovalMode, LeaveApprovalModes.AnyOne),
            ApproverUserTypeCodes = ParseCsv(GetValue(map, LeaveSettingKeys.StaffApproverUserTypes, UserTypeCodes.SchoolAdmin)),
        };
    }

    public async Task<StudentLeaveApprovalSettings> GetStudentSettingsAsync(Guid schoolId, CancellationToken ct = default)
    {
        IReadOnlyList<SchoolSettingRow> rows = await _settings
            .GetByPrefixAsync(schoolId, LeaveSettingKeys.Prefix, ct)
            .ConfigureAwait(false);

        Dictionary<string, string> map = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);

        return new StudentLeaveApprovalSettings
        {
            ApprovalMode = GetValue(map, LeaveSettingKeys.StudentApprovalMode, LeaveApprovalModes.AnyOne),
            DefaultApprover = GetValue(map, LeaveSettingKeys.StudentDefaultApprover, LeaveApproverTokens.ClassTeacher),
            LongLeaveMinDays = int.TryParse(GetValue(map, LeaveSettingKeys.StudentLongLeaveMinDays, "4"), out int days) ? days : 4,
            LongLeaveApproverUserTypeCodes = ParseCsv(
                GetValue(map, LeaveSettingKeys.StudentLongLeaveApproverUserTypes, UserTypeCodes.Principal)),
            LongLeaveTransferToPrincipal = ParseBool(
                GetValue(map, LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, "true")),
        };
    }

    private static string GetValue(IReadOnlyDictionary<string, string> map, string key, string fallback) =>
        map.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static IReadOnlyList<string> ParseCsv(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

    private static bool ParseBool(string raw) =>
        bool.TryParse(raw, out bool value) && value;
}
