namespace SmartOps.Application.Modules.School;

public sealed class SchoolSettingDto
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class UpsertSchoolSettingsDto
{
    public IReadOnlyList<SchoolSettingDto> Settings { get; set; } = Array.Empty<SchoolSettingDto>();
}
