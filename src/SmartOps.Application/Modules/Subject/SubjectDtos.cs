using System.Text.Json;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;

namespace SmartOps.Application.Modules.Subject;

public sealed class CreateSubjectDto
{
    public string SubjectName { get; set; } = null!;
    public string SubjectCode { get; set; } = null!;
    public string SubjectType { get; set; } = null!;
    public string SubjectCategory { get; set; } = null!;
    public string Medium { get; set; } = null!;
    public string[] AssignedClasses { get; set; } = [];
    public int PeriodsPerWeek { get; set; }
    public string PeriodDuration { get; set; } = null!;
    public string[] TeachingDays { get; set; } = [];
    public int MaxTheory { get; set; }
    public int MaxPractical { get; set; }
    public int PassingMarks { get; set; }
    public string GradeSystem { get; set; } = null!;
    public string? SyllabusTextbook { get; set; }
    public string? Curriculum { get; set; }
    public string? Description { get; set; }
}

public static class SubjectMappingExtensions
{
    public static SubjectEntity ToEntity(this CreateSubjectDto dto)
    {
        return new SubjectEntity
        {
            SubjectName = dto.SubjectName,
            SubjectCode = dto.SubjectCode,
            SubjectType = Enum.TryParse<SubjectType>(dto.SubjectType, true, out var type) ? type : SubjectType.Theory,
            SubjectCategory = Enum.TryParse<SubjectCategory>(dto.SubjectCategory.Replace("-", ""), true, out var cat) ? cat : SubjectCategory.Core,
            Medium = MapMedium(dto.Medium),
            AssignedClasses = JsonSerializer.Serialize(dto.AssignedClasses),
            PeriodsPerWeek = dto.PeriodsPerWeek,
            PeriodDuration = dto.PeriodDuration,
            TeachingDays = JsonSerializer.Serialize(dto.TeachingDays),
            MaxTheory = dto.MaxTheory,
            MaxPractical = dto.MaxPractical,
            PassingMarks = dto.PassingMarks,
            GradeSystem = Enum.TryParse<GradeSystem>(dto.GradeSystem, true, out var gs) ? gs : GradeSystem.Marks,
            SyllabusTextbook = dto.SyllabusTextbook,
            Curriculum = Enum.TryParse<Curriculum>(dto.Curriculum, true, out var curr) ? curr : Curriculum.CBSE,
            Description = dto.Description,
            IsActive = true
        };
    }

    private static int MapMedium(string medium)
    {
        return medium switch
        {
            "English" => 1,
            "Hindi" => 2,
            "Gujarati" => 3,
            _ => 1
        };
    }
}

public sealed record CreateSubjectResponse(string Message, Guid SubjectId);
