using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-grade-scales")]
[Authorize]
public sealed class ExamGradeScalesController : ControllerBase
{
    private readonly IExamService _service;

    public ExamGradeScalesController(IExamService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.ExamGradeSetup.View)]
    [ProducesResponseType(typeof(IList<ExamGradeScaleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var result = await _service.GetGradeScalesAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamGradeSetup.View)]
    [ProducesResponseType(typeof(ExamGradeScaleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetGradeScaleAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.ExamGradeSetup.Add)]
    [ProducesResponseType(typeof(ExamGradeScaleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] SaveExamGradeScaleRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateGradeScaleAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamGradeSetup.Edit)]
    [ProducesResponseType(typeof(ExamGradeScaleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExamGradeScaleRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateGradeScaleAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamGradeSetup.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteGradeScaleAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
