using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.StaffAttendance.Services;

public sealed class EmployeeAttendanceSettingsService : IEmployeeAttendanceSettingsService
{
    private readonly ISchoolSettingsRepository _settings;

    public EmployeeAttendanceSettingsService(ISchoolSettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<EmployeeAttendanceTypeSettingDto> GetTypeAsync(Guid schoolId, CancellationToken ct = default)
    {
        IReadOnlyList<SchoolSettingRow> rows = await _settings
            .GetByPrefixAsync(schoolId, EmployeeAttendanceSettingKeys.Prefix, ct)
            .ConfigureAwait(false);

        Dictionary<string, string> map = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        string type = EmployeeAttendanceTypes.Both;
        if (map.TryGetValue(EmployeeAttendanceSettingKeys.EmployeeType, out string? raw)
            && !string.IsNullOrWhiteSpace(raw))
        {
            type = EmployeeAttendanceTypes.Normalize(raw);
        }

        return new EmployeeAttendanceTypeSettingDto(
            type,
            EmployeeAttendanceTypes.AllowsManual(type),
            EmployeeAttendanceTypes.AllowsFace(type));
    }
}
