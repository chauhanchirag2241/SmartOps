using System.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Class;
using SmartOps.Application.Modules.Class.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Api.Modules.Class.Controllers;

[ApiController]
[Route("api/class-settings")]
[Authorize]
public sealed class ClassSettingsController(
    IClassSettingRepository classSettings,
    IUserScopeService userScopeService,
    ITenantProvider tenantProvider,
    DapperContext context) : ControllerBase
{
    [HttpGet("by-teacher/{employeeId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ClassTeacherAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassTeacherAssignmentDto>>> GetByTeacher(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ClassTeacherAssignmentDto> rows = await classSettings
            .GetAssignmentsForTeacherAsync(employeeId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpPost("class-teacher")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(ClassTeacherAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClassTeacherAssignmentDto>> AssignClassTeacher(
        [FromBody] AssignClassTeacherRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.EmployeeId == Guid.Empty || request.ClassId == Guid.Empty)
        {
            return BadRequest("Employee and class are required.");
        }

        Guid? classGroupId = await ResolveClassGroupIdAsync(request.ClassId, cancellationToken)
            .ConfigureAwait(false);
        if (!classGroupId.HasValue)
        {
            return BadRequest("Class was not found.");
        }

        await classSettings
            .UpsertClassTeacherAsync(request.ClassId, classGroupId, request.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        await BumpEmployeeScopeIfLinkedAsync(request.EmployeeId, cancellationToken).ConfigureAwait(false);

        ClassTeacherAssignmentDto? row = (await classSettings
                .GetAssignmentsForTeacherAsync(request.EmployeeId, cancellationToken)
                .ConfigureAwait(false))
            .FirstOrDefault(r => r.ClassId == request.ClassId);

        return row is null
            ? Ok(new ClassTeacherAssignmentDto
            {
                ClassId = request.ClassId,
                ClassGroupId = classGroupId,
                TeacherId = request.EmployeeId,
            })
            : Ok(row);
    }

    [HttpDelete("class-teacher/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearClassTeacher(
        Guid classId,
        [FromQuery] Guid employeeId,
        CancellationToken cancellationToken)
    {
        if (classId == Guid.Empty || employeeId == Guid.Empty)
        {
            return BadRequest("Class and employee are required.");
        }

        Guid? currentTeacherId = await classSettings
            .GetClassTeacherEmployeeIdAsync(classId, cancellationToken)
            .ConfigureAwait(false);

        if (!currentTeacherId.HasValue || currentTeacherId.Value != employeeId)
        {
            return BadRequest("This teacher is not the class teacher for the selected class.");
        }

        Guid? classGroupId = await ResolveClassGroupIdAsync(classId, cancellationToken).ConfigureAwait(false);
        await classSettings
            .UpsertClassTeacherAsync(classId, classGroupId, null, cancellationToken)
            .ConfigureAwait(false);

        await BumpEmployeeScopeIfLinkedAsync(employeeId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<Guid?> ResolveClassGroupIdAsync(Guid classId, CancellationToken cancellationToken)
    {
        IDbConnection connection = await context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT classgroupid
FROM {context.OperationalSchema}.{DatabaseConfig.TableClasses}
WHERE id = @ClassId AND isactive = true
LIMIT 1
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task BumpEmployeeScopeIfLinkedAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        string? rawSchoolId = tenantProvider.GetCurrentSchoolId();
        if (!Guid.TryParse(rawSchoolId, out Guid schoolId) || schoolId == Guid.Empty)
        {
            return;
        }

        IDbConnection connection = await context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT userid FROM {context.OperationalSchema}.{DatabaseConfig.TableEmployees}
WHERE id = @EmployeeId AND userid IS NOT NULL AND isactive = true
LIMIT 1
""";
        Guid? userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (userId.HasValue)
        {
            await userScopeService
                .BumpScopeVersionAsync(userId.Value, schoolId, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
