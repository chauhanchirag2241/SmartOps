using SmartOps.Domain.Modules.Shift.Entities;

namespace SmartOps.Application.Modules.Shift;

public sealed class CreateShiftDto
{
    public string ShiftName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public static class ShiftMappingExtensions
{
    public static ShiftEntity ToEntity(this CreateShiftDto dto) => new()
    {
        ShiftName = dto.ShiftName.Trim(),
        StartTime = NormalizeTime(dto.StartTime),
        EndTime = NormalizeTime(dto.EndTime),
        DisplayOrder = dto.DisplayOrder,
    };

    private static string NormalizeTime(string value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length >= 5)
        {
            return trimmed[..5];
        }

        return trimmed;
    }
}

public sealed record CreateShiftResponse(string Message, Guid ShiftId);
