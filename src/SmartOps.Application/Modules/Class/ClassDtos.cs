using SmartOps.Domain.Modules.Class.Entities;

namespace SmartOps.Application.Modules.Class;

public class CreateClassDto
{
    public Guid ClassGroupId { get; set; }
    public string Section { get; set; } = null!;
    /// <summary>Ignored — class groups are timeless; kept for API compatibility.</summary>
    public Guid AcademicYearId { get; set; }
    public int Capacity { get; set; }
    public string? RoomNumber { get; set; }
    public Guid? ShiftId { get; set; }
}

public class CreateClassGroupDto
{
    public Guid BranchId { get; set; }
    public string ClassName { get; set; } = null!;
    public string? Description { get; set; }
}

public class UpdateClassGroupDto
{
    public Guid BranchId { get; set; }
    public string ClassName { get; set; } = null!;
    public string? Description { get; set; }
}

public class AddClassGroupSubjectDto
{
    public Guid SubjectId { get; set; }
}

public static class ClassMappingExtensions
{
    public static ClassEntity ToEntity(this CreateClassDto dto)
    {
        return new ClassEntity
        {
            ClassGroupId = dto.ClassGroupId,
            Section = dto.Section,
            Capacity = dto.Capacity,
            RoomNumber = dto.RoomNumber,
            ShiftId = dto.ShiftId,
        };
    }

    public static ClassGroupEntity ToEntity(this CreateClassGroupDto dto)
    {
        return new ClassGroupEntity
        {
            BranchId = dto.BranchId,
            ClassName = dto.ClassName,
            Description = dto.Description,
        };
    }
}

/// <summary>Standard API payload after creating a class record.</summary>
public sealed record CreateClassResponse(string Message, Guid ClassId);

/// <summary>Standard API payload after creating a class group.</summary>
public sealed record CreateClassGroupResponse(string Message, Guid ClassGroupId);

/// <summary>Standard API payload after assigning a subject to a class group.</summary>
public sealed record AddClassGroupSubjectResponse(string Message, Guid Id);
