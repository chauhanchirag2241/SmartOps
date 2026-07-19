using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-results")]
[Authorize]
public sealed class ExamResultsController : ControllerBase
{
    private readonly IExamResultService _service;
    private readonly ILogger<ExamResultsController> _logger;

    public ExamResultsController(IExamResultService service, ILogger<ExamResultsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("sheet")]
    [Authorize(Policy = MenuPolicies.ExamResults.View)]
    [ProducesResponseType(typeof(ExamResultSheetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSheet([FromQuery] Guid examId, [FromQuery] Guid classId, CancellationToken ct)
    {
        var result = await _service.GetSheetAsync(examId, classId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("calculate")]
    [Authorize(Policy = MenuPolicies.ExamResults.Add)]
    [ProducesResponseType(typeof(ExamResultSheetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Calculate([FromBody] CalculateExamResultRequestDto request, CancellationToken ct)
    {
        var result = await _service.CalculateAsync(request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Calculate exam result failed for exam {ExamId}: {Error}", request.ExamId, result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("declare")]
    [Authorize(Policy = MenuPolicies.ExamResults.Edit)]
    [ProducesResponseType(typeof(ExamResultSheetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Declare([FromBody] DeclareExamResultRequestDto request, CancellationToken ct)
    {
        var result = await _service.DeclareAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("report-card")]
    [Authorize(Policy = MenuPolicies.ExamResults.View)]
    [ProducesResponseType(typeof(ReportCardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportCard([FromQuery] Guid examId, [FromQuery] Guid studentId, CancellationToken ct)
    {
        var result = await _service.GetReportCardAsync(examId, studentId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
