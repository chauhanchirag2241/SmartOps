using SmartOps.Domain.Modules.AcademicYear.Entities;

namespace SmartOps.Application.Modules.AcademicYear.DTOs;

public class CreateAcademicYearDto
{
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public static class AcademicYearMappingExtensions
{
    public static AcademicYearEntity ToEntity(this CreateAcademicYearDto dto)
    {
        return new AcademicYearEntity
        {
            Title = dto.Title,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = true
        };
    }
}

public sealed record CreateAcademicYearResponse(string Message, Guid AcademicYearId);
