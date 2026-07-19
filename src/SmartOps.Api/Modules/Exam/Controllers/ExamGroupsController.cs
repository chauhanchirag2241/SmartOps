using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-groups")]
[Authorize]
public sealed class ExamGroupsController : ControllerBase
{
    private readonly IExamService _service;

    public ExamGroupsController(IExamService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.ExamGroups.View)]
    [ProducesResponseType(typeof(IList<ExamGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var result = await _service.GetGroupsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.ExamGroups.Add)]
    [ProducesResponseType(typeof(ExamGroupDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] SaveExamGroupRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateGroupAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamGroups.Edit)]
    [ProducesResponseType(typeof(ExamGroupDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExamGroupRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateGroupAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamGroups.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteGroupAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
