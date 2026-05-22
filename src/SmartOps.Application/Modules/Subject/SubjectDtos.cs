using System.Text.Json;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;

namespace SmartOps.Application.Modules.Subject;

public sealed class CreateSubjectDto
{
    public string SubjectName { get; set; } = null!;
    public string SubjectCode { get; set; } = null!;
    public string? SubjectType { get; set; }
    public string? SubjectCategory { get; set; }
    public string? Medium { get; set; }
    public string[] AssignedClasses { get; set; } = [];
    public int PeriodsPerWeek { get; set; } = 1;
    public string? PeriodDuration { get; set; }
    public string[] TeachingDays { get; set; } = [];
    public int MaxTheory { get; set; } = 80;
    public int MaxPractical { get; set; } = 20;
    public int PassingMarks { get; set; } = 33;
    public string? GradeSystem { get; set; }
    public string? SyllabusTextbook { get; set; }
    public string? Curriculum { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class SubjectMappingExtensions
{
    public static SubjectEntity ToEntity(this CreateSubjectDto dto)
    {
        return new SubjectEntity
        {
            SubjectName = dto.SubjectName.Trim(),
            SubjectCode = dto.SubjectCode.Trim(),
            SubjectType = ParseSubjectTypeOrNull(dto.SubjectType),
            SubjectCategory = ParseSubjectCategoryOrNull(dto.SubjectCategory),
            Medium = MapMediumOrNull(dto.Medium),
            AssignedClasses = JsonSerializer.Serialize(dto.AssignedClasses ?? []),
            PeriodsPerWeek = dto.PeriodsPerWeek > 0 ? dto.PeriodsPerWeek : 1,
            PeriodDuration = string.IsNullOrWhiteSpace(dto.PeriodDuration) ? "45" : dto.PeriodDuration.Trim(),
            TeachingDays = JsonSerializer.Serialize(dto.TeachingDays ?? []),
            MaxTheory = dto.MaxTheory,
            MaxPractical = dto.MaxPractical,
            PassingMarks = dto.PassingMarks,
            GradeSystem = ParseGradeSystem(dto.GradeSystem),
            SyllabusTextbook = dto.SyllabusTextbook,
            Curriculum = ParseCurriculum(dto.Curriculum),
            Description = dto.Description,
            IsActive = dto.IsActive
        };
    }

    private static SubjectType? ParseSubjectTypeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<SubjectType>(value.Trim(), true, out var type) ? type : null;
    }

    private static SubjectCategory? ParseSubjectCategoryOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace("-", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return Enum.TryParse<SubjectCategory>(normalized, true, out var category) ? category : null;
    }

    private static GradeSystem ParseGradeSystem(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Enum.TryParse<GradeSystem>(value.Trim(), true, out var gs)
            ? gs
            : GradeSystem.Marks;

    private static Curriculum ParseCurriculum(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Enum.TryParse<Curriculum>(value.Trim(), true, out var curr)
            ? curr
            : Curriculum.CBSE;

    private static int? MapMediumOrNull(string? medium)
    {
        return medium?.Trim() switch
        {
            "English" => 1,
            "Hindi" => 2,
            "Gujarati" => 3,
            _ => null
        };
    }
}

public sealed record CreateSubjectResponse(string Message, Guid SubjectId);
