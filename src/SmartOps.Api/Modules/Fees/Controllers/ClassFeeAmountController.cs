using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Fees.Controllers;

[ApiController]
[Route("api/fees/class-amounts")]
[Authorize]
public sealed class ClassFeeAmountController : ControllerBase
{
    private readonly IClassFeeAmountService _service;

    public ClassFeeAmountController(IClassFeeAmountService service) => _service = service;

    [HttpGet("classes")]
    [Authorize(Policy = MenuPolicies.FeesClassAmounts.View)]
    [ProducesResponseType(typeof(IList<ClassFeeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassSummaries(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid? feeStructureVersionId,
        CancellationToken ct)
    {
        var result = await _service.GetClassSummariesAsync(academicYearId, feeStructureVersionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{classId:guid}")]
    [Authorize(Policy = MenuPolicies.FeesClassAmounts.View)]
    [ProducesResponseType(typeof(ClassFeeAmountsResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassAmounts(
        Guid classId,
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid? feeStructureVersionId,
        CancellationToken ct)
    {
        var result = await _service.GetClassAmountsAsync(classId, academicYearId, feeStructureVersionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{classId:guid}")]
    [Authorize(Policy = MenuPolicies.FeesClassAmounts.Edit)]
    [ProducesResponseType(typeof(ClassFeeAmountsResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveClassAmounts(
        Guid classId,
        [FromBody] SaveClassFeeAmountsRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SaveClassAmountsAsync(classId, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
