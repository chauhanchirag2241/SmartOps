using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.StaffAttendance.Services;

public sealed class EmployeeAttendanceSettingsService : IEmployeeAttendanceSettingsService
{
    public const decimal FallbackDefaultWorkingHours = 8m;
    public const decimal MinDefaultWorkingHours = 1m;
    public const decimal MaxDefaultWorkingHours = 24m;

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

        decimal defaultHours = FallbackDefaultWorkingHours;
        if (map.TryGetValue(EmployeeAttendanceSettingKeys.DefaultWorkingHours, out string? hoursRaw))
        {
            defaultHours = NormalizeDefaultWorkingHours(hoursRaw);
        }

        return new EmployeeAttendanceTypeSettingDto(
            type,
            EmployeeAttendanceTypes.AllowsManual(type),
            EmployeeAttendanceTypes.AllowsFace(type),
            defaultHours);
    }

    public static decimal NormalizeDefaultWorkingHours(string? raw)
    {
        if (!decimal.TryParse(raw, out decimal hours))
        {
            return FallbackDefaultWorkingHours;
        }

        if (hours < MinDefaultWorkingHours)
        {
            return MinDefaultWorkingHours;
        }

        if (hours > MaxDefaultWorkingHours)
        {
            return MaxDefaultWorkingHours;
        }

        return decimal.Round(hours, 2, MidpointRounding.AwayFromZero);
    }
}
