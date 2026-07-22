using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Application.Modules.Timetable;

public sealed class PeriodLineDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public int PeriodOrder { get; set; }
    public string StartTime { get; set; } = null!;
    public string EndTime { get; set; } = null!;
    public bool IsBreak { get; set; }
}

public sealed class CreatePeriodTemplateDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<PeriodLineDto> Periods { get; set; } = [];
}

public sealed record CreatePeriodTemplateResponse(string Message, Guid TemplateId);

public sealed class PeriodTemplateDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<PeriodLineDto> Periods { get; set; } = [];
}

public static class PeriodTemplateMappingExtensions
{
    public static PeriodTemplateEntity ToEntity(this CreatePeriodTemplateDto dto) => new()
    {
        Name = dto.Name.Trim(),
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
        IsActive = dto.IsActive,
    };

    public static List<PeriodEntity> ToPeriodEntities(this CreatePeriodTemplateDto dto, Guid templateId) =>
        (dto.Periods ?? [])
            .Select((p, index) => new PeriodEntity
            {
                Id = p.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
                TemplateId = templateId,
                Name = p.Name.Trim(),
                ShortName = p.ShortName.Trim(),
                PeriodOrder = p.PeriodOrder > 0 ? p.PeriodOrder : index + 1,
                StartTime = p.StartTime.Trim(),
                EndTime = p.EndTime.Trim(),
                IsBreak = p.IsBreak,
            })
            .ToList();
}
