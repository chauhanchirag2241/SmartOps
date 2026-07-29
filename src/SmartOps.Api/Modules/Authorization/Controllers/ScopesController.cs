using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Authorization.Controllers;

[ApiController]
[Route("api/scopes")]
[Authorize]
public sealed class ScopesController(
    IScopeMappingRepository scopeMapping,
    IUserScopeService userScopeService,
    ITenantProvider tenantProvider,
    DapperContext dapperContext) : ControllerBase
{
    [HttpPost("parent-students")]
    [Authorize(Policy = MenuPolicies.Students.Edit)]
    public IActionResult AssignParentStudent()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            message = "Parent portal users are not supported. Only student and employee users are provisioned."
        });
    }

    [HttpPost("hod-departments")]
    [Authorize(Policy = MenuPolicies.Employees.Edit)]
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

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
