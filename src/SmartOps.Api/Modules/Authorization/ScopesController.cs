using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Authorization.DTOs;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Authorization;

[ApiController]
[Route("api/scopes")]
[Authorize]
public sealed class ScopesController(
    IScopeMappingRepository scopeMapping,
    IUserProvisioningService userProvisioning,
    IUserScopeService userScopeService,
    ITenantProvider tenantProvider,
    DapperContext dapperContext) : ControllerBase
{
    [HttpPost("teacher-classes")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    public async Task<IActionResult> AssignTeacherClass(
        [FromBody] AssignTeacherClassDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        Guid? academicYearId = request.AcademicYearId
            ?? await scopeMapping.GetActiveAcademicYearIdAsync(dapperContext.OperationalSchema, cancellationToken)
                .ConfigureAwait(false);

        if (!academicYearId.HasValue)
        {
            return BadRequest("No active academic year found.");
        }

        await scopeMapping.UpsertTeacherClassAssignmentAsync(
            dapperContext.OperationalSchema,
            request.TeacherId,
            request.ClassId,
            academicYearId.Value,
            request.IsClassTeacher,
            cancellationToken).ConfigureAwait(false);

        await InvalidateScopeForSchoolUsersAsync(schoolId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("parent-students")]
    [Authorize(Policy = MenuPolicies.Students.Edit)]
    public async Task<IActionResult> AssignParentStudent(
        [FromBody] AssignParentStudentDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        Guid? parentUserId = await userProvisioning
            .ProvisionParentUserAsync(request.ParentEmail, request.ParentUsername, schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (!parentUserId.HasValue)
        {
            return BadRequest("Parent email is required.");
        }

        await scopeMapping.UpsertParentStudentMappingAsync(
            dapperContext.OperationalSchema,
            parentUserId.Value,
            request.StudentId,
            request.RelationType,
            cancellationToken).ConfigureAwait(false);

        await userScopeService.BumpScopeVersionAsync(parentUserId.Value, schoolId, cancellationToken).ConfigureAwait(false);
        return Ok(new { parentUserId = parentUserId.Value });
    }

    [HttpPost("hod-departments")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    public async Task<IActionResult> AssignHodDepartment(
        [FromBody] AssignHodDepartmentDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        await scopeMapping.UpsertHodDepartmentAssignmentAsync(
            dapperContext.OperationalSchema,
            request.UserId,
            request.DepartmentId,
            request.AcademicYearId,
            cancellationToken).ConfigureAwait(false);

        await userScopeService.BumpScopeVersionAsync(request.UserId, schoolId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task InvalidateScopeForSchoolUsersAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        _ = schoolId;
        _ = cancellationToken;
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
