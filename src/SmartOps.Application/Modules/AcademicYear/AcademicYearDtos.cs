using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Modules.AcademicYear.Entities;

namespace SmartOps.Application.Modules.AcademicYear;

public class CreateAcademicYearDto
{
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class UpdateAcademicYearDto
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
            Title = dto.Title.Trim(),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsActive = true,
            IsCurrent = false,
            Status = AcademicYearStatus.Draft,
        };
    }
}

public sealed record CreateAcademicYearResponse(string Message, Guid AcademicYearId);

public sealed record CurrentAcademicYearDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent);

public sealed record AcademicYearDropdownDto(Guid Id, string Name, bool IsCurrent, DateOnly StartDate);

public static class AcademicYearDtoMapping
{
    public static CurrentAcademicYearDto ToCurrentDto(this AcademicYearEntity entity) =>
        new(entity.Id, entity.Title, entity.StartDate, entity.EndDate, entity.IsCurrent);

    public static AcademicYearDropdownDto ToDropdownDto(this AcademicYearDropdownItem item) =>
        new(item.Id, item.Name, item.IsCurrent, item.StartDate);
}

public static class AcademicYearValidation
{
    public static string? ValidateYearDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            return "End date cannot be earlier than start date.";
        }

        return null;
    }
}
