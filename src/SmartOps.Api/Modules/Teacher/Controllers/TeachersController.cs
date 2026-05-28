using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Modules.Teacher;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Teacher.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TeachersController(
    ITeacherRepository teacherRepository,
    IUserProvisioningService userProvisioning,
    IUserScopeService userScopeService,
    IResourceAuthorizationService resourceAuthorization,
    ITenantProvider tenantProvider,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Teachers.Add)]
    [ProducesResponseType(typeof(CreateTeacherResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateTeacherResponse>> CreateTeacher(
        [FromBody] CreateTeacherDto request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest("Teacher data is required.");

        var entity = request.ToEntity();
        var teacherId = await teacherRepository.CreateTeacherAsync(entity, cancellationToken).ConfigureAwait(false);

        if (TryGetSchoolId(out Guid schoolId))
        {
            Guid? provisionedUserId = await userProvisioning
                .ProvisionTeacherUserAsync(entity, schoolId, cancellationToken)
                .ConfigureAwait(false);

            if (provisionedUserId.HasValue)
            {
                await teacherRepository
                    .SetTeacherUserIdAsync(teacherId, provisionedUserId.Value, cancellationToken)
                    .ConfigureAwait(false);

                await userScopeService
                    .BumpScopeVersionAsync(provisionedUserId.Value, schoolId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return Ok(new CreateTeacherResponse("Teacher created successfully", teacherId));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    public async Task<IActionResult> GetAllTeachers(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchQuery = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] StaffFilter filter = StaffFilter.All,
        CancellationToken cancellationToken = default)
    {
        var result = await teacherRepository
            .GetAllTeachersAsync(pageIndex, pageSize, searchQuery, sortColumn, sortDirection, filter, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("/api/teacher/class-teacher-dropdown")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    public async Task<IActionResult> GetClassTeacherDropdown(CancellationToken cancellationToken)
    {
        var result = await teacherRepository.GetClassTeacherDropdownAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    public async Task<ActionResult<TeacherEntity>> GetTeacherById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorization.CanAccessTeacherAsync(id, AccessLevel.View, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        var teacher = await teacherRepository.GetTeacherByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return teacher is null ? NotFound() : Ok(teacher);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    public async Task<IActionResult> UpdateTeacher(Guid id, [FromBody] TeacherEntity teacher, CancellationToken cancellationToken)
    {
        if (id != teacher.Id) return BadRequest("Route id and payload id must match.");

        if (!await resourceAuthorization.CanAccessTeacherAsync(id, AccessLevel.Edit, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        await teacherRepository.UpdateTeacherAsync(teacher, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    public async Task<IActionResult> DeleteTeacher(Guid id, CancellationToken cancellationToken)
    {
        await teacherRepository.DeleteTeacherAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableTeachers, id, page, pageSize, cancellationToken);

        return Ok(result);
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
