using SmartOps.Domain.Modules.Class.Entities;

namespace SmartOps.Application.Modules.Class;

public class CreateClassDto
{
    public string ClassName { get; set; } = null!;
    public int Section { get; set; }
    public int StreamGroup { get; set; }
    public Guid AcademicYearId { get; set; }
    public int Capacity { get; set; }
    public string? ClassTeacher { get; set; }
    public string? RoomNumber { get; set; }
    public int Shift { get; set; }
    public int Medium { get; set; }
    public string? Description { get; set; }
}

public static class ClassMappingExtensions
{
    public static ClassEntity ToEntity(this CreateClassDto dto)
    {
        return new ClassEntity
        {
            ClassName = dto.ClassName,
            Section = dto.Section,
            StreamGroup = dto.StreamGroup,
            AcademicYearId = dto.AcademicYearId,
            Capacity = dto.Capacity,
            ClassTeacher = dto.ClassTeacher,
            RoomNumber = dto.RoomNumber,
            Shift = dto.Shift,
            Medium = dto.Medium,
            Description = dto.Description,
        };
    }
}

/// <summary>Standard API payload after creating a class record.</summary>
public sealed record CreateClassResponse(string Message, Guid ClassId);
