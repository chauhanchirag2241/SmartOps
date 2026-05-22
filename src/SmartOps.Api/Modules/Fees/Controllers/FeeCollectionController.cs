using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Fees.Controllers;

[ApiController]
[Route("api/fees/collection")]
[Authorize]
public sealed class FeeCollectionController : ControllerBase
{
    private readonly IFeeCollectionService _service;

    public FeeCollectionController(IFeeCollectionService service) => _service = service;

    [HttpGet("students")]
    [Authorize(Policy = MenuPolicies.FeesCollection.View)]
    [ProducesResponseType(typeof(IList<FeeCollectionStudentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(
        [FromQuery] Guid? classId,
        [FromQuery] Guid? academicYearId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _service.GetStudentsAsync(classId, academicYearId, search, status, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("students/{studentId:guid}")]
    [Authorize(Policy = MenuPolicies.FeesCollection.View)]
    [ProducesResponseType(typeof(FeeCollectionStudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentDetail(
        Guid studentId,
        [FromQuery] Guid? academicYearId,
        CancellationToken ct)
    {
        var result = await _service.GetStudentDetailAsync(studentId, academicYearId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("collect")]
    [Authorize(Policy = MenuPolicies.FeesCollection.Add)]
    [ProducesResponseType(typeof(CollectFeeResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CollectFee([FromBody] CollectFeeRequestDto request, CancellationToken ct)
    {
        var result = await _service.CollectFeeAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
