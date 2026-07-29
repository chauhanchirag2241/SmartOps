using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.AcademicPeriod;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.AcademicPeriod;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Modules.Class;

namespace SmartOps.Api.Modules.AcademicPeriod.Controllers;

[ApiController]
[Route("api/academic-periods")]
[Authorize]
public sealed class AcademicPeriodsController(
    IAcademicPeriodRepository periodRepository,
    IClassRepository classRepository,
    IAcademicYearRepository academicYearRepository) : ControllerBase
{
    [HttpGet("classes")]
    [Authorize(Policy = MenuPolicies.AcademicPeriods.View)]
    public async Task<ActionResult<IReadOnlyList<AcademicPeriodClassSummaryDto>>> GetClasses(
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        if (academicYearId == Guid.Empty)
        {
            return BadRequest("Academic year is required.");
        }

        IReadOnlyList<AcademicPeriodClassSummary> rows =
            await periodRepository.GetClassesAsync(academicYearId, cancellationToken).ConfigureAwait(false);
        return Ok(rows.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("classes/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicPeriods.View)]
    public async Task<ActionResult<ClassAcademicPeriodSetupDto>> GetByClass(
        Guid classId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        if (academicYearId == Guid.Empty)
        {
            return BadRequest("Academic year is required.");
        }

        // classId is a class group id (Class 1 / Class 9), not a section row.
        var classGroup = await classRepository.GetClassGroupByIdAsync(classId, cancellationToken).ConfigureAwait(false);
        if (classGroup is null)
        {
            return NotFound();
        }

        IReadOnlyList<ClassAcademicPeriodEntity> periods =
            await periodRepository.GetByClassAsync(classId, academicYearId, cancellationToken).ConfigureAwait(false);

        return Ok(new ClassAcademicPeriodSetupDto(
            classId,
            academicYearId,
            periods.FirstOrDefault()?.PeriodType,
            periods.Select(x => x.ToDto()).ToList()));
    }

    [HttpPut("classes/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicPeriods.Edit)]
    public async Task<ActionResult<ClassAcademicPeriodSetupDto>> Save(
        Guid classId,
        [FromBody] SaveClassAcademicPeriodsRequest request,
        CancellationToken cancellationToken)
    {
        var classGroup = await classRepository.GetClassGroupByIdAsync(classId, cancellationToken).ConfigureAwait(false);
        if (classGroup is null)
        {
            return NotFound("Class not found.");
        }

        if (request.AcademicYearId == Guid.Empty)
        {
            return BadRequest("Academic year is required.");
        }

        var year = await academicYearRepository
            .GetAcademicYearByIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);
        if (year is null)
        {
            return BadRequest("Academic year not found.");
        }

        string? validationError = AcademicPeriodValidation.Validate(
            year.StartDate,
            year.EndDate,
            request.PeriodType,
            request.Periods);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        if (await periodRepository
                .HasPaidInstallmentsAsync(classId, request.AcademicYearId, cancellationToken)
                .ConfigureAwait(false))
        {
            return BadRequest("Academic periods cannot be changed after fee payments exist for this class.");
        }

        List<ClassAcademicPeriodEntity> entities = request.Periods
            .Select(p => new ClassAcademicPeriodEntity
            {
                ClassGroupId = classId,
                AcademicYearId = request.AcademicYearId,
                PeriodType = request.PeriodType,
                PeriodIndex = p.PeriodIndex,
                Name = p.Name.Trim(),
                StartDate = p.StartDate,
                EndDate = p.EndDate,
            })
            .ToList();

        await periodRepository
            .SaveAsync(classId, request.AcademicYearId, entities, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ClassAcademicPeriodEntity> saved =
            await periodRepository.GetByClassAsync(classId, request.AcademicYearId, cancellationToken)
                .ConfigureAwait(false);
        return Ok(new ClassAcademicPeriodSetupDto(
            classId,
            request.AcademicYearId,
            request.PeriodType,
            saved.Select(x => x.ToDto()).ToList()));
    }
}
