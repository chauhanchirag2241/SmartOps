using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.AcademicPeriod;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.AcademicPeriod;
using SmartOps.Domain.Modules.Class;

namespace SmartOps.Api.Modules.AcademicPeriod.Controllers;

[ApiController]
[Route("api/academic-periods")]
[Authorize]
public sealed class AcademicPeriodsController(
    IAcademicPeriodRepository periodRepository,
    IClassRepository classRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpGet("classes/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.View)]
    public async Task<ActionResult<ClassAcademicPeriodSetupDto>> GetByClass(
        Guid classId,
        CancellationToken cancellationToken)
    {
        // classId is a class group id (Class 1 / Class 9), not a section row.
        var classGroup = await classRepository.GetClassGroupByIdAsync(classId, cancellationToken).ConfigureAwait(false);
        if (classGroup is null)
        {
            return NotFound();
        }

        IReadOnlyList<ClassAcademicPeriodEntity> periods =
            await periodRepository.GetByClassAsync(classId, cancellationToken).ConfigureAwait(false);

        return Ok(AcademicPeriodMapping.ToSetupDto(classId, periods));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.View)]
    public async Task<ActionResult<ClassAcademicPeriodDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        ClassAcademicPeriodEntity? period =
            await periodRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (period is null)
        {
            return NotFound();
        }

        return Ok(period.ToDto());
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Classes.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableClassAcademicPeriods, id, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpPut("classes/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.Edit)]
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

        string? validationError = AcademicPeriodValidation.Validate(request.Periods);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        List<ClassAcademicPeriodEntity> entities = request.Periods
            .Select(p => new ClassAcademicPeriodEntity
            {
                Id = p.Id.GetValueOrDefault(),
                ClassGroupId = classId,
                PeriodIndex = p.PeriodIndex,
                Name = p.Name.Trim(),
            })
            .ToList();

        await periodRepository
            .SaveAsync(classId, entities, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ClassAcademicPeriodEntity> saved =
            await periodRepository.GetByClassAsync(classId, cancellationToken)
                .ConfigureAwait(false);
        return Ok(AcademicPeriodMapping.ToSetupDto(classId, saved));
    }
}
