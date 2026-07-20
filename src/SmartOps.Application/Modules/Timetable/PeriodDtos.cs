using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Application.Modules.Timetable;

public sealed class CreatePeriodDto
{
    public string Name { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public int PeriodOrder { get; set; }
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
    public bool IsBreak { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record CreatePeriodResponse(string Message, Guid PeriodId);

public static class PeriodMappingExtensions
{
    public static PeriodEntity ToEntity(this CreatePeriodDto dto)
    {
        return new PeriodEntity
        {
            Name = dto.Name.Trim(),
            ShortName = dto.ShortName.Trim(),
            PeriodOrder = dto.PeriodOrder,
            StartTime = dto.StartTime.Trim(),
            EndTime = dto.EndTime.Trim(),
            IsBreak = dto.IsBreak,
            IsActive = dto.IsActive,
        };
    }
}
