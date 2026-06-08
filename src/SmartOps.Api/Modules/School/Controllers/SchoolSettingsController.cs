using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.School;
using SmartOps.Application.Modules.School.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.School.Controllers;

[ApiController]
[Route("api/schools/{schoolId:guid}/settings")]
[Authorize]
public sealed class SchoolSettingsController(ISchoolSettingsRepository settingsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = MenuPolicies.Settings.View)]
    public async Task<ActionResult<IReadOnlyList<SchoolSettingDto>>> GetSettings(
        Guid schoolId,
        [FromQuery] string? prefix,
        CancellationToken cancellationToken)
    {
        string keyPrefix = string.IsNullOrWhiteSpace(prefix) ? LeaveSettingKeys.Prefix : prefix.Trim();
        IReadOnlyList<SchoolSettingRow> rows = await settingsRepository
            .GetByPrefixAsync(schoolId, keyPrefix, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<SchoolSettingDto> result = rows
            .Select(r => new SchoolSettingDto { Key = r.Key, Value = r.Value })
            .ToList();

        return Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = MenuPolicies.Settings.Edit)]
    public async Task<IActionResult> UpsertSettings(
        Guid schoolId,
        [FromBody] UpsertSchoolSettingsDto request,
        CancellationToken cancellationToken)
    {
        if (request.Settings is null || request.Settings.Count == 0)
        {
            return BadRequest("At least one setting is required.");
        }

        IReadOnlyList<SchoolSettingUpsert> upserts = request.Settings
            .Where(s => !string.IsNullOrWhiteSpace(s.Key))
            .Select(s => new SchoolSettingUpsert { Key = s.Key.Trim(), Value = s.Value ?? string.Empty })
            .ToList();

        await settingsRepository.UpsertAsync(schoolId, upserts, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
