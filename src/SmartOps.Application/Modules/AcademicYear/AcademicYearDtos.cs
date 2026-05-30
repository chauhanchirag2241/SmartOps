using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Modules.AcademicYear.Entities;

namespace SmartOps.Application.Modules.AcademicYear;

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
            IsActive = true,
            IsCurrent = false
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

public sealed record AcademicYearSemesterDto(
    Guid Id,
    Guid AcademicYearId,
    int SemesterIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record UpsertAcademicYearSemesterDto(
    int SemesterIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record SaveAcademicYearSemestersRequest(IList<UpsertAcademicYearSemesterDto> Semesters);

public static class AcademicYearSemesterMapping
{
    public static AcademicYearSemesterDto ToDto(this AcademicYearSemesterEntity entity) =>
        new(entity.Id, entity.AcademicYearId, entity.SemesterIndex, entity.Name, entity.StartDate, entity.EndDate);

    public static AcademicYearSemesterInput ToInput(this UpsertAcademicYearSemesterDto dto) =>
        new(dto.SemesterIndex, dto.Name, dto.StartDate, dto.EndDate);
}
