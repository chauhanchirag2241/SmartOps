using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;

namespace SmartOps.Application.Modules.Subject;

public sealed class CreateSubjectDto
{
    public Guid ClassGroupId { get; set; }
    public string SubjectName { get; set; } = null!;
    public string SubjectCode { get; set; } = null!;
    public string? SubjectType { get; set; }
    public string? SubjectCategory { get; set; }
    public string? Medium { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class SubjectMappingExtensions
{
    public static SubjectEntity ToEntity(this CreateSubjectDto dto)
    {
        return new SubjectEntity
        {
            ClassGroupId = dto.ClassGroupId == Guid.Empty ? null : dto.ClassGroupId,
            SubjectName = dto.SubjectName.Trim(),
            SubjectCode = dto.SubjectCode.Trim(),
            SubjectType = ParseSubjectTypeOrNull(dto.SubjectType),
            SubjectCategory = ParseSubjectCategoryOrNull(dto.SubjectCategory),
            Medium = MapMediumOrNull(dto.Medium),
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
