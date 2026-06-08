using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;

namespace SmartOps.Api.Modules.School.Controllers;

[ApiController]
[Route("api/schools")]
[Authorize]
public sealed class SchoolMigrationController : ControllerBase
{
    private readonly ISchoolDataMigrationService _migrationService;

    public SchoolMigrationController(ISchoolDataMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    /// <summary>
    /// Migrates an existing schema-tenanted school to a dedicated PostgreSQL database.
    /// </summary>
    [HttpPost("{schoolId:guid}/migrate-database")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MigrateToDedicatedDatabase(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _migrationService
                .MigrateSchoolToDedicatedDatabaseAsync(schoolId, cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
