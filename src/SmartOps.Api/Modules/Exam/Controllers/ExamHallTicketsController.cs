using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-hall-tickets")]
[Authorize]
public sealed class ExamHallTicketsController : ControllerBase
{
    private readonly IExamResultService _service;

    public ExamHallTicketsController(IExamResultService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.ExamHallTickets.View)]
    [ProducesResponseType(typeof(IList<HallTicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] Guid examId, [FromQuery] Guid classId, CancellationToken ct)
    {
        var result = await _service.GetHallTicketsAsync(examId, classId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("generate")]
    [Authorize(Policy = MenuPolicies.ExamHallTickets.Add)]
    [ProducesResponseType(typeof(IList<HallTicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromBody] GenerateHallTicketsRequestDto request, CancellationToken ct)
    {
        var result = await _service.GenerateHallTicketsAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
